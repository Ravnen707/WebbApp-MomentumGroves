namespace MomentumGroves.Models;

/// <summary>
/// Describes the missing local databases and the separately managed Frida dataset.
/// </summary>
public sealed record DatabaseInitializationStatus(bool MomentumMissing, bool FoodsMissing, bool FridaMissing)
{
    public bool RequiresLocalSetup => MomentumMissing || FoodsMissing;
}