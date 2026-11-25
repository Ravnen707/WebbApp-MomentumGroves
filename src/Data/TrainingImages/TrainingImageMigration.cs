using System.Text.Json;
using MomentumGroves.Service;
using MomentumGroves.Repositories;

namespace MomentumGroves.Data.TrainingImages;

/// <summary>
/// Initializes the copy workflow for moving local pictures into cloud storage.
/// </summary>
/// <param name="cloud">The configured exercise image store.</param>
public sealed class TrainingImageMigration(ITrainingImageStore cloud)
{
    /// <summary>
    /// Copies a supported local picture unless the cloud object already exists or this is a dry run.
    /// </summary>
    /// <param name="file">The file for this operation.</param>
    /// <param name="apply">Whether to copy the file during this run.</param>
    /// <param name="token">The token used to cancel the operation.</param>
    /// <returns>The copy result for the supplied image.</returns>
    public async Task<string> CopyAsync(string file, bool apply, CancellationToken token)
    {
        var name = Path.GetFileName(file);
        var info = new FileInfo(file);
        if (!TrainingImageService.IsImageName(name) || info.Length is 0 or > TrainingImageService.MaxUploadBytes)
        {
            return "invalid";
        }

        var bytes = await File.ReadAllBytesAsync(file, token);
        var extension = TrainingImageService.GetExtension(bytes);
        if (extension is null || bytes.Length > TrainingImageService.MaxUploadBytes)
        {
            return "invalid";
        }

        var existing = await cloud.ReadAsync(name, token);
        if (existing is not null)
        {
            return bytes.AsSpan().SequenceEqual(existing.Bytes) ? "already copied" : "conflict";
        }

        if (!apply)
        {
            return "would copy";
        }

        try
        {
            await cloud.PutAsync(name, bytes, TrainingImageService.MediaType(extension), token);
            return "copied";
        }
        catch (TrainingImageStorageException)
        {
            // A concurrent copy or a lost response can leave an object at this key.
            existing = await cloud.ReadAsync(name, token);
            if (existing is not null)
            {
                return bytes.AsSpan().SequenceEqual(existing.Bytes) ? "already copied" : "conflict";
            }
            throw;
        }
    }

    /// <summary>
    /// Runs the training picture migration command and reports its outcome.
    /// </summary>
    /// <param name="services">The registered application services for the command.</param>
    /// <param name="args">The command arguments selecting dry-run or apply mode.</param>
    /// <returns>The command exit code.</returns>
    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        if (args.Length != 1 || args[0] is not ("--dry-run" or "--apply"))
        {
            Console.Error.WriteLine("Usage: migrate-training-images --dry-run | --apply");
            return 2;
        }

        var database = services.GetRequiredService<PersonalDatabase>();
        if (!database.UsesPostgres)
        {
            Console.Error.WriteLine("Configure the web project's Supabase database before copying exercise images.");
            return 2;
        }

        var token = CancellationToken.None;
        var apply = args[0] == "--apply";
        var web = services.GetRequiredService<IWebHostEnvironment>();
        var directory = Path.Combine(web.WebRootPath ?? Path.Combine(web.ContentRootPath, "wwwroot"), "TrainingImages");
        var paths = Directory.Exists(directory) ? Directory.EnumerateFiles(directory).ToArray() : [];
        var store = services.GetRequiredService<SupabaseTrainingImageStore>();
        var migration = new TrainingImageMigration(store);
        try
        {
            var keys = (await store.ListAsync(token)).ToHashSet(StringComparer.Ordinal);
            keys.UnionWith(paths.Select(Path.GetFileName).OfType<string>().Where(TrainingImageService.IsImageName));
            var missing = await MissingReferencesAsync(database, keys, token);
            foreach (var image in missing)
            {
                Console.WriteLine($"missing reference: {image}");
            }

            var failed = missing.Count != 0;
            foreach (var path in paths.Where(path => TrainingImageService.IsImageName(Path.GetFileName(path))))
            {
                var status = await migration.CopyAsync(path, apply, token);
                Console.WriteLine($"{status}: {Path.GetFileName(path)}");
                failed |= status is "invalid" or "conflict";
            }
            return failed ? 1 : 0;
        }
        catch (Exception exception) when (exception is TrainingImageStorageException or IOException or
                                           HttpRequestException or System.Data.Common.DbException or JsonException)
        {
            Console.Error.WriteLine($"Image copy stopped ({exception.GetType().Name}). Local files were retained.");
            return 1;
        }
    }

    /// <summary>
    /// Finds picture references in application state that are absent from the available image names.
    /// </summary>
    /// <param name="database">The configured personal database.</param>
    /// <param name="imageNames">The available picture names to compare against training references.</param>
    /// <param name="token">The token used to cancel the operation.</param>
    /// <returns>The image filenames referenced by training but absent from storage.</returns>
    private static async Task<HashSet<string>> MissingReferencesAsync(PersonalDatabase database,
        HashSet<string> imageNames, CancellationToken token)
    {
        var missing = new HashSet<string>(StringComparer.Ordinal);
        var repository = new MomentumDataRepository(database);
        if (await repository.ReadCurrentTrainingJsonAsync(token) is { } json)
        {
            using var document = JsonDocument.Parse(json);
            Collect(document.RootElement);
        }
        return missing;

        /// <summary>
        /// Collects picture references while traversing the training JSON document.
        /// </summary>
        /// <param name="node">The JSON node being inspected for picture references.</param>
        void Collect(JsonElement node)
        {
            if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in node.EnumerateArray())
                {
                    Collect(item);
                }
            }
            else if (node.ValueKind == JsonValueKind.Object)
            {
                const string prefix = "/TrainingImages/";
                foreach (var property in node.EnumerateObject())
                {
                    if (property.NameEquals("image") && property.Value.ValueKind == JsonValueKind.String &&
                        property.Value.GetString() is { } path && path.StartsWith(prefix, StringComparison.Ordinal) &&
                        !imageNames.Contains(Uri.UnescapeDataString(path[prefix.Length..])))
                    {
                        missing.Add(path);
                    }
                    else
                    {
                        Collect(property.Value);
                    }
                }
            }
        }
    }
}
