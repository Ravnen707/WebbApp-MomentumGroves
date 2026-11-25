using MomentumGroves.Data;
using MomentumGroves.Models;

namespace MomentumGroves.Service;

public sealed class DatabaseInitializationService(PersonalDatabase database)
{
    private readonly object _setupLock = new();

    public DatabaseInitializationStatus GetStatus() => new(
        !database.UsesPostgres && !File.Exists(Path.Combine(database.DirectoryPath, PersonalDatabase.StateFile)),
        !database.UsesPostgres && !File.Exists(Path.Combine(database.DirectoryPath, PersonalDatabase.LibraryFile)),
        !File.Exists(Path.Combine(database.DirectoryPath, "frida.db")));

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