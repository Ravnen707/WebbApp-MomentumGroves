using System.Text.RegularExpressions;

namespace MomentumGroves.Service;

public sealed class TrainingImageService(IWebHostEnvironment environment)
{
    public const int MaxUploadBytes = 8_000_000;
    private readonly string _directory = Path.Combine(
        environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "TrainingImages");

    public IReadOnlyList<TrainingPicture> List()
    {
        if (!Directory.Exists(_directory))
            return [];

        return Directory.EnumerateFiles(_directory)
            .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp")
            .Select(path => Describe(Path.GetFileName(path)))
            .OrderBy(image => image.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<TrainingPicture> SaveAsync(string originalName, byte[] data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var extension = GetExtension(data);
        if (data.Length > MaxUploadBytes || extension is null)
            throw new ArgumentException("Choose a JPG, PNG or WebP image no larger than 8 MB.");

        var name = Regex.Replace(Path.GetFileNameWithoutExtension(originalName), "[^a-zA-Z0-9]+", "-").Trim('-');
        name = name[..Math.Min(name.Length, 80)].TrimEnd('-');
        if (name.Length == 0)
            name = "exercise";

        var filename = $"{Guid.NewGuid():N}_{name}{extension}";
        Directory.CreateDirectory(_directory);
        var temporaryPath = Path.Combine(_directory, filename + ".tmp");
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, data, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, Path.Combine(_directory, filename));
        }
        finally
        {
            File.Delete(temporaryPath);
        }

        return Describe(filename);
    }

    private static string? GetExtension(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith(new byte[] { 0xff, 0xd8, 0xff }))
            return ".jpg";
        if (data.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
            return ".png";
        if (data.Length >= 12 && data.StartsWith("RIFF"u8) && data[8..].StartsWith("WEBP"u8))
            return ".webp";

        return null;
    }

    private static TrainingPicture Describe(string filename)
    {
        var name = Path.GetFileNameWithoutExtension(filename);
        if (name.Length > 33 && name[32] == '_' && Guid.TryParseExact(name[..32], "N", out _))
            name = name[33..];

        return new TrainingPicture(name.Replace('-', ' '), $"/TrainingImages/{Uri.EscapeDataString(filename)}");
    }
}

public sealed record TrainingPicture(string Name, string Url);
