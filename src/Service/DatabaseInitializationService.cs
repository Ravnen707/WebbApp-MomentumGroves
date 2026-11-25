using MomentumGroves.Data;
using MomentumGroves.Models;

namespace MomentumGroves.Service;

/// <summary>
/// Initializes the service that checks and prepares local databases.
/// </summary>
/// <param name="database">The configured personal database.</param>
public sealed class DatabaseInitializationService(PersonalDatabase database)
{
    private readonly object _setupLock = new();

    /// <summary>
    /// Checks whether the data files required for local setup exist.
    /// </summary>
    /// <returns>The missing database flags for the current configuration.</returns>
    public DatabaseInitializationStatus GetStatus()
    {
        return new(
        !database.UsesPostgres && !File.Exists(Path.Combine(database.DirectoryPath, PersonalDatabase.StateFile)),
        !database.UsesPostgres && !File.Exists(Path.Combine(database.DirectoryPath, PersonalDatabase.LibraryFile)),
        !File.Exists(Path.Combine(database.DirectoryPath, "frida.db")));
    }

    /// <summary>
    /// Creates missing local databases while serializing concurrent setup requests.
    /// </summary>
    public void InitializeLocalDatabases()
    {
        lock (_setupLock)
        {
            if (GetStatus().RequiresLocalSetup)
            {
                database.CreateMissingSqliteDatabases();
            }
        }
    }
}
