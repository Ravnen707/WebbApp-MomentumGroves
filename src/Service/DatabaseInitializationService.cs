using MomentumGroves.Data;
using MomentumGroves.Models;

namespace MomentumGroves.Service;

/// <summary>
/// Tracks missing local databases at startup and coordinates their creation on request.
/// Frida remains exclusively managed by the existing Admin installer.
/// </summary>
public sealed class DatabaseInitializationService
{
    private readonly string _dataDirectory;
    private readonly bool _usesPostgres;
    private readonly bool _momentumMissingAtStartup;
    private readonly bool _foodsMissingAtStartup;
    private bool _localInitializationConfirmed;

    /// <summary>
    /// Captures the existence of the database files before normal application services can open them.
    /// This check only accesses filesystem metadata and does not log on successful startup.
    /// </summary>
    /// <param name="database">The configured personal database provider.</param>
    public DatabaseInitializationService(PersonalDatabase database)
    {
        _dataDirectory = database.DirectoryPath;
        _usesPostgres = database.UsesPostgres;
        _momentumMissingAtStartup = !_usesPostgres && !File.Exists(Path.Combine(_dataDirectory, "momentum.db"));
        _foodsMissingAtStartup = !_usesPostgres && !File.Exists(Path.Combine(_dataDirectory, "food_and_recipes.db"));
    }

    /// <summary>
    /// Returns the initial setup state without opening any database or triggering initialization.
    /// A missing Frida file is reported separately and never included in local initialization.
    /// </summary>
    /// <returns>The current database setup status.</returns>
    public DatabaseInitializationStatus GetStatus()
    {
        var momentumMissing = !_localInitializationConfirmed && _momentumMissingAtStartup;
        var foodsMissing = !_localInitializationConfirmed && _foodsMissingAtStartup;
        var fridaMissing = !File.Exists(Path.Combine(_dataDirectory, "frida.db"));
        return new DatabaseInitializationStatus(momentumMissing, foodsMissing, fridaMissing);
    }

    /// <summary>
    /// Creates only missing local databases through their existing application services.
    /// Their constructors also establish the expected tables without changing existing data.
    /// </summary>
    /// <param name="services">Request services used to initialize the existing database services.</param>
    /// <returns>The updated setup status.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the database files could not be created.</exception>
    public DatabaseInitializationStatus InitializeLocalDatabases(IServiceProvider services)
    {
        if (_usesPostgres)
            return GetStatus();

        lock (this)
        {
            Directory.CreateDirectory(_dataDirectory);
            _ = services.GetRequiredService<MomentumDataService>();
            _ = services.GetRequiredService<FoodLibraryService>();
            if (!File.Exists(Path.Combine(_dataDirectory, "momentum.db")) ||
                !File.Exists(Path.Combine(_dataDirectory, "food_and_recipes.db")))
            {
                throw new InvalidOperationException("Local database initialization did not create the required files.");
            }

            _localInitializationConfirmed = true;
            return GetStatus();
        }
    }
}