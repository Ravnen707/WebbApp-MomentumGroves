namespace MomentumGroves.Models;

public sealed record FridaCheckResult(
    bool Success,
    string Message,
    string InstalledVersion,
    int FoodCount,
    FridaRelease? Latest,
    bool UpdateAvailable);
