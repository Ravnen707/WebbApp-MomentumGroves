using MomentumGroves.Models;
using MomentumGroves.Data.TrainingImages;
using System.Text.RegularExpressions;

namespace MomentumGroves.Service;

/// <summary>
/// Initializes exercise picture management for the local gallery or cloud store.
/// </summary>
/// <param name="environment">The environment that supplies the application content root.</param>
/// <param name="cloud">The configured exercise image store.</param>
public sealed class TrainingImageService(IWebHostEnvironment environment, ITrainingImageStore? cloud = null)
{
    public const int MaxUploadBytes = 8_000_000;
    private readonly string _directory = Path.Combine(environment.WebRootPath ?? Path.Combine
        (environment.ContentRootPath, "wwwroot"), "TrainingImages");

    public bool UsesCloud => cloud is not null;

    /// <summary>
    /// Lists exercise pictures in the configured storage mode.
    /// </summary>
    /// <param name="token">The token used to cancel the operation.</param>
    /// <returns>The pictures available from the configured store.</returns>
    public async Task<IReadOnlyList<TrainingPicture>> ListAsync(CancellationToken token)
    {
        var local = List();
        if (cloud is null)
        {
            return local;
        }

        var names = await cloud.ListAsync(token);
        return local.Concat(names.Where(IsImageName).Select(Describe))
            .DistinctBy(image => image.Url).OrderBy(image => image.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// Reads a valid exercise image name from cloud storage when configured.
    /// </summary>
    /// <param name="filename">The requested filename.</param>
    /// <param name="token">The token used to cancel the operation.</param>
    /// <returns>The stored image, or null when absent or cloud storage is unavailable.</returns>
    public Task<StoredTrainingImage?> ReadCloudAsync(string filename, CancellationToken token)
    {
        return cloud is not null && IsImageName(filename) ? cloud.ReadAsync(filename,
            token) : Task.FromResult<StoredTrainingImage?>(null);
    }

    /// <summary>
    /// Checks whether the supplied filename is safe and has a supported image extension.
    /// </summary>
    /// <param name="name">The name used to identify the item.</param>
    /// <returns>True when the filename is valid and uses a supported extension.</returns>
    internal static bool IsImageName(string name)
    {
        return name.Length is > 0 and <= 255 && name.IndexOfAny(['/', '\\', '?', '#',
            ':']) == -1 && !name.Any(char.IsControl) &&
            Path.GetExtension(name).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp";
    }

    /// <summary>
    /// Lists supported picture files in the local gallery.
    /// </summary>
    /// <returns>The pictures available in the local gallery.</returns>
    public IReadOnlyList<TrainingPicture> List()
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(_directory)
            .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp")
            .Select(path => Describe(Path.GetFileName(path)))
            .OrderBy(image => image.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Validates image bytes and saves a uniquely named exercise picture.
    /// </summary>
    /// <param name="originalName">The filename supplied with the upload.</param>
    /// <param name="data">The bytes of the uploaded image.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The saved picture label and URL.</returns>
    public async Task<TrainingPicture> SaveAsync(string originalName, byte[] data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var extension = GetExtension(data);
        if (data.Length > MaxUploadBytes || extension is null)
        {
            throw new ArgumentException("Choose a JPG, PNG or WebP image no larger than 8 MB.");
        }

        var name = Regex.Replace(Path.GetFileNameWithoutExtension(originalName),
            "[^a-zA-Z0-9]+", "-").Trim('-');
        
        name = name[..Math.Min(name.Length, 80)].TrimEnd('-');
        if (name.Length == 0)
        {
            name = "exercise";
        }

        var prefix = cloud is null ? "" : "cloud-";
        var filename = $"{prefix}{Guid.NewGuid():N}_{name}{extension}";
        if (cloud is not null)
        {
            await cloud.PutAsync(filename, data, MediaType(extension), cancellationToken);
            return Describe(filename);
        }

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

    /// <summary>
    /// Maps a supported image extension to its HTTP media type.
    /// </summary>
    /// <param name="extension">The supported filename extension.</param>
    /// <returns>The HTTP media type for the supplied extension.</returns>
    internal static string MediaType(string extension)
    {
        return extension switch
        {
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => throw new ArgumentException("Unsupported image type.")
        };
    }

    /// <summary>
    /// Identifies the supported image format from its signature bytes.
    /// </summary>
    /// <param name="data">The image bytes whose signature is inspected.</param>
    /// <returns>The recognized image extension, or null for unsupported bytes.</returns>
    internal static string? GetExtension(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith(new byte[] { 0xff, 0xd8, 0xff }))
        {
            return ".jpg";
        }
        if (data.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
        {
            return ".png";
        }
        if (data.Length >= 12 && data.StartsWith("RIFF"u8) && data[8..].StartsWith("WEBP"u8))
        {
            return ".webp";
        }

        return null;
    }

    /// <summary>
    /// Builds the display label and browser URL for a picture.
    /// </summary>
    /// <param name="filename">The requested filename.</param>
    /// <returns>The picture label and URL used by the browser.</returns>
    private static TrainingPicture Describe(string filename)
    {
        var name = Path.GetFileNameWithoutExtension(filename);
        if (name.StartsWith("cloud-", StringComparison.Ordinal))
        {
            name = name[6..];
        }
        if (name.Length > 33 && name[32] == '_' && Guid.TryParseExact(name[..32], "N", out _))
        {
            name = name[33..];
        }

        return new TrainingPicture(name.Replace('-', ' '), $"/TrainingImages/{Uri.EscapeDataString(filename)}");
    }
}
