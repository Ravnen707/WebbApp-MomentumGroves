using System.Globalization;
using Microsoft.Data.Sqlite;
using MomentumGroves.Models;

namespace MomentumGroves.Repositories;

/// <summary>
/// Reads and creates local SQLite datasets for DTU Frida nutrition information.
/// </summary>
/// <param name="databasePath">The path to the installed Frida database.</param>
internal sealed class FridaRepository(string databasePath)
{
    /// <summary>
    /// Looks up up to twenty foods by their Danish or English names.
    /// </summary>
    /// <param name="query">The food name fragment to match.</param>
    /// <param name="fiberByName">The optional fiber values indexed by Danish food name.</param>
    /// <param name="cancellationToken">The token used to cancel the database read.</param>
    /// <returns>Ranked Frida foods with available nutrition values.</returns>
    internal async Task<List<FoodResult>> SearchAsync(string query, IReadOnlyDictionary<string, double> fiberByName,
        CancellationToken cancellationToken)
    {
        var results = new List<FoodResult>();
        await using var connection = OpenReadOnlyConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT NameDanish, Kcal, Protein, Carbs, Fat
                              FROM Foods
                              WHERE instr(lower(NameDanish), lower($query)) > 0
                                 OR instr(lower(NameEnglish), lower($query)) > 0
                              ORDER BY
                                  CASE
                                      WHEN lower(NameDanish) = lower($query) THEN 0
                                      WHEN lower(NameDanish) LIKE lower($prefix) ESCAPE '\' THEN 1
                                      WHEN instr(lower(NameDanish), lower($query)) > 0 THEN 2
                                      WHEN lower(NameEnglish) LIKE lower($prefix) ESCAPE '\' THEN 3
                                      ELSE 4
                                  END,
                                  length(NameDanish), NameDanish
                              LIMIT 20;
                              """;
        var trimmedQuery = query.Trim();
        command.Parameters.AddWithValue("$query", trimmedQuery);
        var escapedPrefix = trimmedQuery.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        command.Parameters.AddWithValue("$prefix", escapedPrefix);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new FoodResult(
                reader.GetString(0),
                "DTU Frida 6.1",
                NullableDouble(reader, 1),
                NullableDouble(reader, 2),
                NullableDouble(reader, 3),
                NullableDouble(reader, 4),
                fiberByName.TryGetValue(reader.GetString(0), out var fiber) ? fiber : null));
        }

        return results;
    }

    /// <summary>
    /// Creates a read-only connection that releases its file handle when disposed.
    /// </summary>
    /// <returns>An unopened connection to the installed dataset.</returns>
    private SqliteConnection OpenReadOnlyConnection()
    {
        // Frida installation replaces this file, so searches must release idle file handles.
        var options = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        return new SqliteConnection(options.ToString());
    }

    /// <summary>
    /// Reads a nullable nutrition value from a database row.
    /// </summary>
    /// <param name="reader">The reader positioned at the food row.</param>
    /// <param name="ordinal">The index of the nutrition column.</param>
    /// <returns>The recorded nutrient amount, or null for a database null.</returns>
    private static double? NullableDouble(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);
    }

    /// <summary>
    /// Reads the installed Frida release and counts the foods in it.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the database read.</param>
    /// <returns>The installed version and food count, or an empty version and zero for a missing file.</returns>
    internal async Task<(string Version, int Count)> GetInstalledAsync(CancellationToken cancellationToken)
    {
        // An absent database means Frida has not been installed yet.
        // Do not create an empty file: installation creates the complete schema and data.
        if (!File.Exists(databasePath))
        {
            return ("", 0);
        }

        await using var connection = OpenReadOnlyConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Foods";
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='FridaMetadata'";
        var hasMetadata = await command.ExecuteScalarAsync(cancellationToken) is not null;
        var version = "6.1"; // Existing database was imported from Frida 6.1 before metadata existed.
        if (hasMetadata)
        {
            command.CommandText = "SELECT Version FROM FridaMetadata LIMIT 1";
            version = (string?)await command.ExecuteScalarAsync(cancellationToken) ?? version;
        }

        return (version, count);
    }

    /// <summary>
    /// Creates a complete staged Frida database in a single transaction.
    /// </summary>
    /// <param name="path">The path to the staging database file.</param>
    /// <param name="foods">The nutrition rows to insert.</param>
    /// <param name="release">The release metadata to store with the rows.</param>
    /// <param name="cancellationToken">The token used to cancel database creation.</param>
    /// <returns>A task that completes after the database has been committed and closed.</returns>
    internal static async Task CreateDatabaseAsync(string path, List<FridaFoodRow> foods, FridaRelease release,
        CancellationToken cancellationToken)
    {
        // Closing a pooled connection retains its native file handle, preventing the staging file
        // from being replaced or deleted. Dispose this connection before returning to InstallAsync.
        var builder = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var schema = connection.CreateCommand())
        {
            schema.Transaction = transaction;
            schema.CommandText =
                "CREATE TABLE Foods (Id INTEGER PRIMARY KEY, NameDanish TEXT NOT NULL, NameEnglish TEXT, Kcal REAL, Protein REAL, Carbs REAL, Fat REAL); CREATE TABLE FridaMetadata (Version TEXT NOT NULL, ArticleId INTEGER NOT NULL, FileName TEXT NOT NULL, UpdatedUtc TEXT NOT NULL);";
            await schema.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                "INSERT INTO Foods (NameDanish, NameEnglish, Kcal, Protein, Carbs, Fat) VALUES ($dk, $en, $kcal, $protein, $carbs, $fat)";
            var dk = insert.Parameters.Add("$dk", SqliteType.Text);
            var en = insert.Parameters.Add("$en", SqliteType.Text);
            var kcal = insert.Parameters.Add("$kcal", SqliteType.Real);
            var protein = insert.Parameters.Add("$protein", SqliteType.Real);
            var carbs = insert.Parameters.Add("$carbs", SqliteType.Real);
            var fat = insert.Parameters.Add("$fat", SqliteType.Real);
            foreach (var food in foods)
            {
                dk.Value = food.Danish;
                en.Value = (object?)food.English ?? DBNull.Value;
                kcal.Value = (object?)food.Kcal ?? DBNull.Value;
                protein.Value = (object?)food.Protein ?? DBNull.Value;
                carbs.Value = (object?)food.Carbs ?? DBNull.Value;
                fat.Value = (object?)food.Fat ?? DBNull.Value;
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await using (var metadata = connection.CreateCommand())
        {
            metadata.Transaction = transaction;
            metadata.CommandText = "INSERT INTO FridaMetadata VALUES ($version, $article, $file, $updated)";
            metadata.Parameters.AddWithValue("$version", release.Version);
            metadata.Parameters.AddWithValue("$article", release.ArticleId);
            metadata.Parameters.AddWithValue("$file", release.FileName);
            metadata.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
            await metadata.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await connection.CloseAsync();
    }
}
