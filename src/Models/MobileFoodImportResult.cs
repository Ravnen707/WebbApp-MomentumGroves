namespace MomentumGroves.Models;

/// <summary>Summary of foods accepted or skipped by the desktop database.</summary>
public sealed record MobileFoodImportResult(int Imported, int Skipped, int Received, List<Guid> AcknowledgedIds);