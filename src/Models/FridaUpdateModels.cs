namespace MomentumGroves.Models;

public sealed record FridaRelease(string Version, long ArticleId, string Title, string FileName, string DownloadUrl);

public sealed record FridaCheckResult(
    bool Success,
    string Message,
    string InstalledVersion,
    int FoodCount,
    FridaRelease? Latest,
    bool UpdateAvailable);

public sealed record FridaInstallRequest(long ArticleId, string Version);

public sealed record FridaInstallResult(bool Success, string Message, string Version, int FoodCount);