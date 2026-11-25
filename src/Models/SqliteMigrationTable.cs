using System.Data;

namespace MomentumGroves.Models;

internal sealed record SqliteMigrationTable(
    string Name,
    string Columns,
    DbType[] Types,
    bool Optional = false);
