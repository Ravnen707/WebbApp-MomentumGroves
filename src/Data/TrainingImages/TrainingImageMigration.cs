using System.Text.Json;
using MomentumGroves.Service;

namespace MomentumGroves.Data.TrainingImages;

public sealed class TrainingImageMigration(ITrainingImageStore cloud)
{
    public async Task<string> CopyAsync(string file, bool apply, CancellationToken token)
    {
        var name = Path.GetFileName(file);
        var info = new FileInfo(file);
        if (!TrainingImageService.IsImageName(name) || info.Length is 0 or > TrainingImageService.MaxUploadBytes)
            return "invalid";

        var bytes = await File.ReadAllBytesAsync(file, token);
        var extension = TrainingImageService.GetExtension(bytes);
        if (extension is null || bytes.Length > TrainingImageService.MaxUploadBytes)
            return "invalid";

        var existing = await cloud.ReadAsync(name, token);
        if (existing is not null)
            return bytes.AsSpan().SequenceEqual(existing.Bytes) ? "already copied" : "conflict";

        if (!apply)
            return "would copy";

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
                return bytes.AsSpan().SequenceEqual(existing.Bytes) ? "already copied" : "conflict";
            throw;
        }
    }

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
                Console.WriteLine($"missing reference: {image}");

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

    private static async Task<HashSet<string>> MissingReferencesAsync(PersonalDatabase database,
        HashSet<string> imageNames, CancellationToken token)
    {
        var missing = new HashSet<string>(StringComparer.Ordinal);
        await using var connection = database.CreateConnection("momentum.db");
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT jsonvalue FROM appstate
            WHERE key IN ('sunhed.training-programs.v3', 'sunhed.training-programs.v2')
            ORDER BY key DESC LIMIT 1
            """;
        if (await command.ExecuteScalarAsync(token) is string json)
        {
            using var document = JsonDocument.Parse(json);
            Collect(document.RootElement);
        }
        return missing;

        void Collect(JsonElement node)
        {
            if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in node.EnumerateArray())
                    Collect(item);
            }
            else if (node.ValueKind == JsonValueKind.Object)
            {
                const string prefix = "/TrainingImages/";
                foreach (var property in node.EnumerateObject())
                {
                    if (property.NameEquals("image") && property.Value.ValueKind == JsonValueKind.String &&
                        property.Value.GetString() is { } path && path.StartsWith(prefix, StringComparison.Ordinal) &&
                        !imageNames.Contains(Uri.UnescapeDataString(path[prefix.Length..])))
                        missing.Add(path);
                    else
                        Collect(property.Value);
                }
            }
        }
    }
}
