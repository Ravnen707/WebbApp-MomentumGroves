using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace MomentumGroves.Data;

/// <summary>
/// Manages connections, schema initialization, and transaction behavior for personal data.
/// </summary>
public sealed class PersonalDatabase : IDisposable
{
    /// <summary>
    /// The creation timestamp assigned to recipes saved before SQLite tracked that field.
    /// </summary>
    internal const string LegacyRecipeCreatedUtc = "2026-09-30T22:00:00.000Z";

    /// <summary>
    /// The shared PostgreSQL connection pool when Supabase is configured.
    /// </summary>
    private readonly NpgsqlDataSource? _postgres;

    /// <summary>
    /// Runs PostgreSQL schema initialization once per provider instance.
    /// </summary>
    private readonly Lazy<bool> _postgresSchema;

    private readonly bool _allowImplicitSqliteCreation;

    internal const string StateFile = "momentum.db";
    internal const string LibraryFile = "food_and_recipes.db";

    /// <summary>
    /// Selects the storage provider for personal data.
    /// </summary>
    /// <param name="directory">The application's local data directory.</param>
    /// <param name="supabase">The PostgreSQL connection string. An empty or missing value selects SQLite.</param>
    /// <param name="mobileDataApiEnabled">Preserves the manually granted mobile API schema access.</param>
    /// <param name="allowImplicitSqliteCreation">Allows callers outside the web setup flow to create SQLite files.</param>
    public PersonalDatabase(string directory, string? supabase = null, bool mobileDataApiEnabled = false,
        bool allowImplicitSqliteCreation = true)
    {
        DirectoryPath = directory;
        _allowImplicitSqliteCreation = allowImplicitSqliteCreation;
        MobileDataApiEnabled = mobileDataApiEnabled && !string.IsNullOrWhiteSpace(supabase);
        if (!string.IsNullOrWhiteSpace(supabase))
        {
            var settings = new NpgsqlConnectionStringBuilder(supabase)
            {
                SearchPath = "momentum_groves",
                IncludeErrorDetail = false,
                LogParameters = false
            };
            if (!settings.ShouldSerialize("SSL Mode"))
            {
                settings.SslMode = SslMode.Require;
            }
            _postgres = NpgsqlDataSource.Create(settings.ConnectionString);
        }

        _postgresSchema = new Lazy<bool>(() =>
        {
            using var connection = OpenPostgres();
            using var transaction = connection.BeginTransaction();
            InitializePostgres(connection, transaction, MobileDataApiEnabled);
            transaction.Commit();
            return true;
        });
    }


    /// <summary>
    /// The directory containing the application's local data files.
    /// </summary>
    public string DirectoryPath { get; }

    /// <summary>
    /// Whether PostgreSQL is configured for personal data.
    /// </summary>
    public bool UsesPostgres => _postgres is not null;

    internal bool MobileDataApiEnabled { get; }

    /// <summary>
    /// The provider's case-insensitive substring predicate for searching saved food names.
    /// </summary>
    internal string FoodNameMatch => UsesPostgres ? "strpos(lower(Name), lower(@query)) > 0"
        : "instr(lower(Name), lower(@query)) > 0";

    /// <summary>
    /// Creates a SQLite provider using the web app's local data directory.
    /// </summary>
    /// <param name="environment">The web host environment that supplies the content root.</param>
    /// <returns>A provider for the SQLite files under the content root.</returns>
    public static PersonalDatabase Local(IWebHostEnvironment environment) =>
        new(Path.Combine(environment.ContentRootPath, "src", "Data"));

    /// <summary>
    /// Creates an unopened connection to PostgreSQL or to the named SQLite file.
    /// </summary>
    internal DbConnection CreateConnection(string file)
    {
        if (_postgres is not null)
        {
            return _postgres.CreateConnection();
        }

        if (_allowImplicitSqliteCreation)
            Directory.CreateDirectory(DirectoryPath);
        {
            return CreateSqliteConnection(file, _allowImplicitSqliteCreation);
        }
    }

    private SqliteConnection CreateSqliteConnection(string file, bool create, bool pooling = true)
    {
        return new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(DirectoryPath, file),
            ForeignKeys = true,
            Mode = create ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
            Pooling = pooling
        }.ToString());
    }

    /// <summary>
    /// Opens a connection and disposes it if opening fails.
    /// </summary>
    internal DbConnection Open(string file)
    {
        var connection = CreateConnection(file);
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

    /// <summary>
    /// Opens a pooled PostgreSQL connection when Supabase is configured.
    /// </summary>
    internal NpgsqlConnection OpenPostgres() =>
        (_postgres ?? throw new InvalidOperationException("Configure ConnectionStrings:Supabase first."))
        .OpenConnection();

    /// <summary>
    /// Ensures the private PostgreSQL schema is ready when Supabase is configured.
    /// </summary>
    public void InitializePostgres()
    {
        if (UsesPostgres) _ = _postgresSchema.Value;
    }

    /// <summary>
    /// Ensures the application state table exists in the selected database.
    /// </summary>
    internal void InitializeState()
    {
        if (UsesPostgres)
        {
            InitializePostgres();
            {
                return;
            }
        }

        using var connection = Open(StateFile);
        InitializeStateSchema(connection);
    }

    private static void InitializeStateSchema(DbConnection connection)
    {
        Execute(connection, null, """
                                  CREATE TABLE IF NOT EXISTS AppState (
                                      Key TEXT PRIMARY KEY NOT NULL, JsonValue TEXT NOT NULL,
                                      UpdatedUtc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
                                  );
                                  """);
    }

    /// <summary>
    /// Ensures the food library tables exist and upgrades older SQLite recipes with creation timestamps.
    /// </summary>
    internal void InitializeLibrary()
    {
        if (UsesPostgres)
        {
            InitializePostgres();
            {
                return;
            }
        }

        using var connection = Open(LibraryFile);
        InitializeLibrarySchema(connection);
    }

    private static void InitializeLibrarySchema(DbConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        Execute(connection, transaction, """
                                         CREATE TABLE IF NOT EXISTS CustomFoods (
                                             Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                             Name TEXT NOT NULL, Unit TEXT NOT NULL CHECK(Unit IN ('g','ml')),
                                             Kcal REAL NOT NULL, Protein REAL NOT NULL, Carbs REAL NOT NULL,
                                             Fat REAL NOT NULL, Fiber REAL NULL
                                         );
                                         CREATE TABLE IF NOT EXISTS Recipes (
                                             Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                             Name TEXT NOT NULL, FinishedWeight REAL NULL,
                                             CreatedUtc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                                         );
                                         CREATE TABLE IF NOT EXISTS RecipeIngredients (
                                             RecipeId INTEGER NOT NULL REFERENCES Recipes(Id) ON DELETE CASCADE,
                                             FoodId INTEGER NOT NULL REFERENCES CustomFoods(Id) ON DELETE RESTRICT,
                                             Amount REAL NOT NULL CHECK(Amount > 0),
                                             PRIMARY KEY (RecipeId, FoodId)
                                         );
                                         """);
        using var column = connection.CreateCommand();
        column.Transaction = transaction;
        column.CommandText = "SELECT 1 FROM pragma_table_info('Recipes') WHERE name = 'CreatedUtc'";
        if (column.ExecuteScalar() is null)
        {
            Execute(connection, transaction,
                $"ALTER TABLE Recipes ADD COLUMN CreatedUtc TEXT NOT NULL DEFAULT '{LegacyRecipeCreatedUtc}'");
        }

        transaction.Commit();
    }

    internal void CreateMissingSqliteDatabases()
    {
        CreateMissingSqliteDatabase(StateFile, InitializeStateSchema);
        CreateMissingSqliteDatabase(LibraryFile, InitializeLibrarySchema);
    }

    private void CreateMissingSqliteDatabase(string file, Action<DbConnection> initialize)
    {
        var destination = Path.Combine(DirectoryPath, file);
        if (File.Exists(destination))
        {
            return;
        }

        Directory.CreateDirectory(DirectoryPath);
        var temporaryFile = $"{file}.{Guid.NewGuid():N}.tmp";
        var temporaryPath = Path.Combine(DirectoryPath, temporaryFile);
        try
        {
            using (var connection = CreateSqliteConnection(temporaryFile, create: true, pooling: false))
            {
                connection.Open();
                initialize(connection);
            }

            File.Move(temporaryPath, destination);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// Creates the personal data schema in the supplied transaction and restricts access by Data API roles.
    /// </summary>
    internal static void InitializePostgres(NpgsqlConnection connection, NpgsqlTransaction transaction,
        bool mobileDataApiEnabled = false)
    {
        Execute(connection, transaction, """
                                         SELECT pg_advisory_xact_lock(10672924, 1);
                                         CREATE SCHEMA IF NOT EXISTS momentum_groves;
                                         CREATE TABLE IF NOT EXISTS AppState (
                                             Key TEXT PRIMARY KEY NOT NULL, JsonValue TEXT NOT NULL, UpdatedUtc TEXT NOT NULL
                                         );
                                         CREATE TABLE IF NOT EXISTS CustomFoods (
                                             Id BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                                             Name TEXT NOT NULL, Unit TEXT NOT NULL CHECK(Unit IN ('g','ml')),
                                             Kcal DOUBLE PRECISION NOT NULL, Protein DOUBLE PRECISION NOT NULL,
                                             Carbs DOUBLE PRECISION NOT NULL, Fat DOUBLE PRECISION NOT NULL, Fiber DOUBLE PRECISION
                                         );
                                         CREATE TABLE IF NOT EXISTS Recipes (
                                             Id BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                                             Name TEXT NOT NULL, FinishedWeight DOUBLE PRECISION, CreatedUtc TEXT NOT NULL
                                         );
                                         CREATE TABLE IF NOT EXISTS RecipeIngredients (
                                             RecipeId BIGINT NOT NULL REFERENCES Recipes(Id) ON DELETE CASCADE,
                                             FoodId BIGINT NOT NULL REFERENCES CustomFoods(Id) ON DELETE RESTRICT,
                                             Amount DOUBLE PRECISION NOT NULL CHECK(Amount > 0),
                                             PRIMARY KEY (RecipeId, FoodId)
                                         );
                                         CREATE TABLE IF NOT EXISTS MobileFoodImports (
                                             MobileId TEXT PRIMARY KEY, FoodId BIGINT NOT NULL REFERENCES CustomFoods(Id)
                                         );
                                         ALTER TABLE AppState ENABLE ROW LEVEL SECURITY;
                                         ALTER TABLE CustomFoods ENABLE ROW LEVEL SECURITY;
                                         ALTER TABLE Recipes ENABLE ROW LEVEL SECURITY;
                                         ALTER TABLE RecipeIngredients ENABLE ROW LEVEL SECURITY;
                                         ALTER TABLE MobileFoodImports ENABLE ROW LEVEL SECURITY;
                                         """);
        var preserveMobileSchemaGrant = mobileDataApiEnabled ? "TRUE" : "FALSE";
        Execute(connection, transaction, $"""
                                          REVOKE ALL ON SCHEMA momentum_groves FROM PUBLIC;
                                          REVOKE ALL ON ALL TABLES IN SCHEMA momentum_groves FROM PUBLIC;
                                          REVOKE ALL ON ALL SEQUENCES IN SCHEMA momentum_groves FROM PUBLIC;
                                          DO $private$
                                          DECLARE role_name text;
                                          BEGIN
                                              FOR role_name IN SELECT rolname FROM pg_roles
                                                  WHERE rolname IN ('anon', 'authenticated', 'service_role', 'authenticator')
                                              LOOP
                                                  EXECUTE format('REVOKE ALL ON ALL TABLES IN SCHEMA momentum_groves FROM %I', role_name);
                                                  EXECUTE format('REVOKE ALL ON ALL SEQUENCES IN SCHEMA momentum_groves FROM %I', role_name);
                                                  IF role_name <> 'authenticated' OR NOT {preserveMobileSchemaGrant} THEN
                                                      EXECUTE format('REVOKE ALL ON SCHEMA momentum_groves FROM %I', role_name);
                                                      IF has_schema_privilege(role_name, 'momentum_groves', 'USAGE') THEN
                                                          RAISE EXCEPTION 'A Data API role still has access to the private schema';
                                                      END IF;
                                                  END IF;
                                              END LOOP;
                                          END;
                                          $private$;
                                          """);
    }

    /// <summary>
    /// Creates the SQLite import tracking table on demand or serializes PostgreSQL food imports.
    /// </summary>
    internal void EnsureMobileImports(DbConnection connection, DbTransaction transaction)
    {
        if (UsesPostgres)
        {
            LockFoodWrites(connection, transaction);
        }
        else
        {
            Execute(connection, transaction,
                "CREATE TABLE IF NOT EXISTS MobileFoodImports (MobileId TEXT PRIMARY KEY, FoodId INTEGER NOT NULL REFERENCES CustomFoods(Id))");
        }
    }

    /// <summary>
    /// Serializes PostgreSQL writes to foods and import mappings during duplicate checks.
    /// </summary>
    internal void LockFoodWrites(DbConnection connection, DbTransaction transaction)
    {
        if (UsesPostgres)
        {
            Execute(connection, transaction, "LOCK TABLE CustomFoods, MobileFoodImports IN SHARE ROW EXCLUSIVE MODE");
        }
    }

    /// <summary>
    /// Prevents concurrent PostgreSQL writes from changing import mappings or recipe ingredients during a food deletion.
    /// </summary>
    internal void LockFoodDeletion(DbConnection connection, DbTransaction transaction)
    {
        if (UsesPostgres)
        {
            LockFoodWrites(connection, transaction);
            Execute(connection, transaction, "LOCK TABLE RecipeIngredients IN SHARE ROW EXCLUSIVE MODE");
        }
    }

    /// <summary>
    /// Checks whether import tracking is available, including in older SQLite databases.
    /// </summary>
    internal bool HasMobileImports(DbConnection connection, DbTransaction transaction)
    {
        if (UsesPostgres)
        {
            return true;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name='MobileFoodImports'";
        return command.ExecuteScalar() is not null;
    }

    /// <summary>
    /// Starts a state transaction and serializes writes so updates can safely read and modify existing data.
    /// </summary>
    internal static async Task<DbTransaction> BeginStateAsync(DbConnection connection, bool write,
        CancellationToken cancellationToken)
    {
        if (connection is SqliteConnection sqlite)
        {
            return sqlite.BeginTransaction(deferred: !write);
        }

        var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (write)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "LOCK TABLE AppState IN SHARE ROW EXCLUSIVE MODE";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Identifies integrity constraint failures reported by either database provider.
    /// </summary>
    internal static bool IsConstraintViolation(DbException exception) =>
        exception is SqliteException { SqliteErrorCode: 19 } ||
        exception is PostgresException
        {
            SqlState: PostgresErrorCodes.ForeignKeyViolation or
            PostgresErrorCodes.CheckViolation or PostgresErrorCodes.NotNullViolation or
            PostgresErrorCodes.UniqueViolation
        };

    /// <summary>
    /// Returns the current UTC time in the millisecond format used for stored timestamps.
    /// </summary>
    internal static string UtcNow() =>
        DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Executes a SQL statement without reading a result, using the supplied transaction when present.
    /// </summary>
    internal static void Execute(DbConnection connection, DbTransaction? transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Disposes the PostgreSQL connection pool when one was created.
    /// </summary>
    public void Dispose() => _postgres?.Dispose();
}

/// <summary>
/// Provides parameter binding for commands from either database provider.
/// </summary>
internal static class DatabaseParameters
{
    /// <summary>
    /// Adds a command parameter and converts null to a database null, using an explicit type when supplied.
    /// </summary>
    public static void AddParameter(this DbCommand command, string name, object? value,
        DbType? type = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        if (type.HasValue)
        {
            parameter.DbType = type.Value;
        }
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}