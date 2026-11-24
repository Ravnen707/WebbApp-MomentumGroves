using System.Data;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace MomentumGroves.Data;

/// <summary>
/// Copies personal data from read-only SQLite snapshots into an empty PostgreSQL destination.
/// </summary>
internal static class SqliteDataMigration
{
    private sealed record Table(string Name, string Columns, DbType[] Types, bool Optional = false);
    private static readonly Table State = new("AppState", "Key, JsonValue, UpdatedUtc",
        [DbType.String, DbType.String, DbType.String]);
    private static readonly Table[] Library =
    [
        new("CustomFoods", "Id, Name, Unit, Kcal, Protein, Carbs, Fat, Fiber",
            [DbType.Int64, DbType.String, DbType.String, DbType.Double, DbType.Double, DbType.Double, DbType.Double, DbType.Double]),
        new("Recipes", "Id, Name, FinishedWeight, CreatedUtc",
            [DbType.Int64, DbType.String, DbType.Double, DbType.String]),
        new("RecipeIngredients", "RecipeId, FoodId, Amount", [DbType.Int64, DbType.Int64, DbType.Double]),
        new("MobileFoodImports", "MobileId, FoodId", [DbType.String, DbType.Int64], Optional: true)
    ];

    /// <summary>
    /// Runs the migration and rolls back the transaction in dry-run mode.
    /// </summary>
    /// <param name="args">The source directory option and either dry-run or apply mode.</param>
    /// <param name="configuration">The application configuration containing the Supabase connection string.</param>
    /// <returns>Zero on success, one on migration failure, or two for invalid arguments.</returns>
    internal static int Run(string[] args, IConfiguration configuration)
    {
        if (args is not ["--source", _, "--dry-run" or "--apply"])
        {
            Console.Error.WriteLine("Usage: migrate-sqlite --source <SQLite snapshot directory> --dry-run|--apply");
            return 2;
        }

        var apply = args[2] == "--apply";
        try
        {
            using var database = new PersonalDatabase(args[1], configuration.GetConnectionString("Supabase"));
            using var state = OpenSource(args[1], "momentum.db");
            using var library = OpenSource(args[1], "food_and_recipes.db");
            if (state is null && library is null)
                throw new InvalidOperationException("No personal SQLite databases were found in the source directory.");

            using var destination = database.OpenPostgres();
            using var transaction = destination.BeginTransaction();
            PersonalDatabase.InitializePostgres(destination, transaction);
            PersonalDatabase.Execute(destination, transaction, """
                LOCK TABLE AppState, CustomFoods, MobileFoodImports, Recipes, RecipeIngredients IN ACCESS EXCLUSIVE MODE;
                """);

            foreach (var table in Library.Prepend(State))
            {
                if (Count(destination, transaction, table.Name) != 0)
                    throw new InvalidOperationException($"The destination already contains rows in {table.Name}.");
            }

            if (state is not null)
            {
                using var snapshot = state.BeginTransaction(deferred: true);
                Copy(State, snapshot, transaction);
            }

            if (library is not null)
            {
                using var snapshot = library.BeginTransaction(deferred: true);
                foreach (var table in Library)
                    Copy(table, snapshot, transaction);

                RestartIdentity(snapshot, transaction, "CustomFoods");
                RestartIdentity(snapshot, transaction, "Recipes");
            }

            if (apply)
                transaction.Commit();
            else
                transaction.Rollback();

            Console.WriteLine(apply ? "Migration committed." : "Dry run validated and rolled back.");
            return 0;
        }
        catch (PostgresException exception)
        {
            Console.Error.WriteLine($"Migration failed with PostgreSQL SQLSTATE {exception.SqlState}.");
        }
        catch (InvalidOperationException exception) when (exception.Source == typeof(SqliteDataMigration).Assembly.GetName().Name)
        {
            Console.Error.WriteLine(exception.Message);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Migration failed ({exception.GetType().Name}). Check the source snapshots and destination configuration.");
        }

        return 1;
    }

    /// <summary>
    /// Opens an existing source file in read-only mode and returns null if the file is absent.
    /// </summary>
    private static SqliteConnection? OpenSource(string directory, string filename)
    {
        var path = Path.Combine(directory, filename);
        if (!File.Exists(path))
        {
            Console.WriteLine($"Source {filename} is absent.");
            return null;
        }

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static bool HasTable(SqliteTransaction snapshot, string table)
    {
        using var command = snapshot.Connection!.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@name COLLATE NOCASE";
        command.Parameters.AddWithValue("@name", table);
        return command.ExecuteScalar() is not null;
    }

    /// <summary>
    /// Copies a table with its original IDs and verifies the destination row count.
    /// </summary>
    private static void Copy(Table table, SqliteTransaction snapshot, NpgsqlTransaction transaction)
    {
        if (table.Optional && !HasTable(snapshot, table.Name))
        {
            Console.WriteLine($"Optional source table {table.Name} is absent.");
            return;
        }

        using var select = snapshot.Connection!.CreateCommand();
        select.Transaction = snapshot;
        var columns = table.Columns;
        if (table.Name == "Recipes")
        {
            select.CommandText = "SELECT 1 FROM pragma_table_info('Recipes') WHERE name = 'CreatedUtc' COLLATE NOCASE";
            if (select.ExecuteScalar() is null)
                columns = $"Id, Name, FinishedWeight, '{PersonalDatabase.LegacyRecipeCreatedUtc}'";
        }

        select.CommandText = $"SELECT {columns} FROM {table.Name}";
        var expected = Count(snapshot.Connection, snapshot, table.Name);
        using var rows = select.ExecuteReader();

        using var insert = transaction.Connection!.CreateCommand();
        insert.Transaction = transaction;
        var placeholders = Enumerable.Range(1, table.Types.Length).Select(index => "$" + index);
        insert.CommandText = $"INSERT INTO {table.Name} ({table.Columns}) VALUES ({string.Join(", ", placeholders)})";
        foreach (var type in table.Types)
            insert.Parameters.Add(new NpgsqlParameter { DbType = type });

        long copied = 0;
        while (rows.Read())
        {
            for (var index = 0; index < table.Types.Length; index++)
            {
                insert.Parameters[index].Value = rows.IsDBNull(index) ? DBNull.Value : table.Types[index] switch
                {
                    DbType.Int64 => rows.GetInt64(index),
                    DbType.Double => rows.GetDouble(index),
                    _ => rows.GetString(index)
                };
            }

            copied += insert.ExecuteNonQuery();
        }

        if (copied != expected || Count(transaction.Connection, transaction, table.Name) != expected)
            throw new InvalidOperationException($"Row count verification failed for {table.Name}.");

        Console.WriteLine($"{table.Name}: {copied} rows verified.");
    }

    private static long Count(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction,
        string table)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)command.ExecuteScalar()!;
    }

    /// <summary>
    /// Sets the next PostgreSQL identity value above both the highest copied ID and SQLite's saved sequence.
    /// </summary>
    private static void RestartIdentity(SqliteTransaction snapshot, NpgsqlTransaction transaction, string table)
    {
        using var maximum = snapshot.Connection!.CreateCommand();
        maximum.Transaction = snapshot;
        maximum.CommandText = $"SELECT COALESCE(MAX(Id), 0) FROM {table}";
        var highWaterMark = Math.Max(0, (long)maximum.ExecuteScalar()!);
        if (HasTable(snapshot, "sqlite_sequence"))
        {
            maximum.CommandText = "SELECT seq FROM sqlite_sequence WHERE name=@name COLLATE NOCASE";
            maximum.Parameters.AddWithValue("@name", table);
            if (maximum.ExecuteScalar() is long sequence)
                highWaterMark = Math.Max(highWaterMark, sequence);
        }

        var next = checked(highWaterMark + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        PersonalDatabase.Execute(transaction.Connection!, transaction, $"ALTER TABLE {table} ALTER COLUMN Id RESTART WITH {next}");
    }
}
