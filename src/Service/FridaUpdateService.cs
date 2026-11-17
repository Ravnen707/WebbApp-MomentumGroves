using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ExcelDataReader;
using Microsoft.Data.Sqlite;
using MomentumGroves.Models;

namespace MomentumGroves.Service;

public sealed class FridaUpdateService
{
    private const long KnownArticleId = 32312844;
    private const string ApiBase = "https://api.figshare.com/v2/articles";
    private readonly HttpClient _http;
    private readonly string _databasePath;
    private readonly ILogger<FridaUpdateService> _logger;
    private static readonly SemaphoreSlim InstallLock = new(1, 1);

    public FridaUpdateService(HttpClient http, IWebHostEnvironment environment, ILogger<FridaUpdateService> logger)
    {
        _http = http;
        _databasePath = Path.Combine(environment.ContentRootPath, "src", "Data", "frida.db");
        _logger = logger;
    }

    public async Task<FridaCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        (string Version, int Count) installed = ("", 0);
        try
        {
            installed = await GetInstalledAsync(cancellationToken);
            var releases = await FindReleasesAsync(cancellationToken);
            var latest = releases.OrderByDescending(r => ParseVersion(r.Version)).FirstOrDefault();
            if (latest is null)
            {
                _logger.LogWarning("Frida release check found no valid Excel releases");
                return new FridaCheckResult(false, "No valid Frida Excel files were found at DTU.",
                    installed.Version, installed.Count, null, false);
            }

            var newer = ParseVersion(latest.Version) > ParseVersion(installed.Version);
            return new FridaCheckResult(true,
                newer
                    ? $"New DTU version found: {latest.Version} (installed: {installed.Version}). You can install the new Excel file."
                    : $"The latest DTU version found is {latest.Version}. Your installed version is {installed.Version}; no newer version was found.",
                installed.Version, installed.Count, latest, newer);
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(exception, "DTU Frida version check failed");
            return new FridaCheckResult(false, "Could not check DTU right now. Please try again later.",
                installed.Version, installed.Count, null, false);
        }
    }

    public async Task<FridaInstallResult> InstallAsync(FridaInstallRequest request, CancellationToken cancellationToken)
    {
        if (!await InstallLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogInformation("Frida installation skipped because another update is running");
            return new FridaInstallResult(false, "An update is already running.", "", 0);
        }

        string? temporaryFile = null;
        try
        {
            // Never trust a download URL or version supplied by the browser.
            var releases = await FindReleasesAsync(cancellationToken);
            var latest = releases.OrderByDescending(r => ParseVersion(r.Version)).FirstOrDefault();
            if (latest is null || latest.ArticleId != request.ArticleId || latest.Version != request.Version)
            {
                _logger.LogWarning("Frida installation rejected because the requested release is no longer current");
                return new FridaInstallResult(false, "The selected version is no longer the latest. Please check again.",
                    "", 0);
            }

            var installed = await GetInstalledAsync(cancellationToken);
            if (ParseVersion(latest.Version) <= ParseVersion(installed.Version))
            {
                return new FridaInstallResult(true, "No newer version is available. The database is unchanged.", installed.Version,
                    installed.Count);
            }

            temporaryFile = Path.Combine(Path.GetTempPath(), $"frida-{Guid.NewGuid():N}.xlsx");
            using (var response = await _http.GetAsync(latest.DownloadUrl, HttpCompletionOption.ResponseHeadersRead,
                       cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > 100_000_000)
                {
                    throw new InvalidDataException("The Excel file exceeds the 100 MB size limit.");
                }

                await using var destination = File.Create(temporaryFile);
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                await source.CopyToAsync(destination, cancellationToken);
                if (destination.Length > 100_000_000)
                {
                    throw new InvalidDataException("The Excel file exceeds the 100 MB size limit.");
                }
            }
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            
            var foods = ReadFoods(temporaryFile);
            if (foods.Count < 500)
            {
                throw new InvalidDataException(
                    "The dataset contains too few valid foods. The database was not changed.");
            }

            // Build a completely new SQLite file, then atomically replace the old database.
            Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
            var stagedPath = _databasePath + ".staging";
            try
            {
                if (File.Exists(stagedPath))
                {
                    File.Delete(stagedPath);
                }

                await CreateDatabaseAsync(stagedPath, foods, latest, cancellationToken);
                // On first installation there is no database to replace.
                // Later updates retain the existing database as a backup.
                if (File.Exists(_databasePath))
                {
                    File.Replace(stagedPath, _databasePath, _databasePath + ".backup", true);
                }
                else
                {
                    File.Move(stagedPath, _databasePath);
                }
            }
            finally
            {
                if (File.Exists(stagedPath))
                {
                    TryDeleteTemporaryFile(stagedPath);
                }
            }

            _logger.LogInformation("Installed Frida version {Version} with {FoodCount} foods", latest.Version, foods.Count);
            return new FridaInstallResult(true,
                $"Updated to version {latest.Version}. Imported {foods.Count} foods.", latest.Version,
                foods.Count);
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _logger.LogError(exception, "DTU Frida import failed");
            return new FridaInstallResult(false,
                "The update failed. The existing database has been preserved. Please try again later.", "", 0);
        }
        finally
        {
            if (temporaryFile is not null && File.Exists(temporaryFile))
            {
                TryDeleteTemporaryFile(temporaryFile);
            }

            InstallLock.Release();
        }
    }

    private void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not remove a temporary Frida file");
        }
    }

    public async Task<(string Version, int Count)> GetInstalledAsync(CancellationToken cancellationToken)
    {
        // An absent database means Frida has not been installed yet.
        // Do not create an empty file: installation creates the complete schema and data.
        if (!File.Exists(_databasePath))
        {
            return ("", 0);
        }

        // This file is replaced during installation; idle pooled handles would keep it locked on Windows.
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Foods";
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='FridaMetadata'";
        var hasMetadata = await command.ExecuteScalarAsync(cancellationToken) is not null;
        var version = "6.1"; // Existing database was imported from Frida 6.1 before metadata existed.
        if (hasMetadata)
        {
            command.CommandText = "SELECT Version FROM FridaMetadata LIMIT 1";
            version = (string?)await command.ExecuteScalarAsync(cancellationToken) ?? version;
        }

        return (version, count);
    }

    private async Task<List<FridaRelease>> FindReleasesAsync(CancellationToken cancellationToken)
    {
        var articleIds = new HashSet<long> { KnownArticleId, 29500682 };
        // Figshare searches titles, not fixed URL slugs, so renamed releases can be found.
        foreach (var phrase in new[] { "The Danish Food Composition Database", "Frida version" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiBase + "/search")
            {
                Content = JsonContent.Create(new { search_for = phrase, page_size = 100 })
            };
            using var response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            foreach (var article in json.RootElement.EnumerateArray())
            {
                if (article.TryGetProperty("id", out var id))
                {
                    articleIds.Add(id.GetInt64());
                }
            }
        }

        var releases = new List<FridaRelease>();
        foreach (var articleId in articleIds)
        {
            try
            {
                using var response = await _http.GetAsync($"{ApiBase}/{articleId}", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Skipping Frida article {ArticleId}: HTTP {StatusCode}", articleId,
                        (int)response.StatusCode);
                    continue;
                }

                using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
                var root = json.RootElement;
                var title = root.GetProperty("title").GetString() ?? "";
                if (!title.Contains("Food Composition Database", StringComparison.OrdinalIgnoreCase) &&
                    !title.Contains("Frida", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var file in root.GetProperty("files").EnumerateArray())
                {
                    var name = file.GetProperty("name").GetString() ?? "";
                    if (!name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var versionMatch = Regex.Match(name + " " + title, @"(?:FCDB[_\s-]*|version\s*)(\d+(?:[._]\d+)+)",
                        RegexOptions.IgnoreCase);
                    if (!versionMatch.Success)
                    {
                        continue;
                    }

                    var version = versionMatch.Groups[1].Value.Replace('_', '.');
                    var url = file.GetProperty("download_url").GetString() ?? "";
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
                        !(uri.Host.EndsWith("figshare.com", StringComparison.OrdinalIgnoreCase) ||
                          uri.Host.EndsWith("dtu.dk", StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    releases.Add(new FridaRelease(version, articleId, title, name, url));
                }
            }
            catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                _logger.LogWarning(exception, "Could not read Frida article {ArticleId}; continuing with other articles", articleId);
            }
        }

        return releases;
    }

    private static Version ParseVersion(string value)
    {
        return Version.TryParse(value, out var parsed) ? parsed : new Version(0, 0);
    }

    private static List<FoodRow> ReadFoods(string path)
    {
        var foods = new List<FoodRow>();
        using var stream = File.OpenRead(path);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        do
        {
            if (reader.Name != "Data_Table")
            {
                continue;
            }

            var rowNumber = 0;
            while (reader.Read())
            {
                rowNumber++;
                if (rowNumber <= 4)
                {
                    continue;
                }

                var name = reader.GetValue(0)?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var english = reader.GetValue(1)?.ToString();
                foods.Add(new FoodRow(name, english, Number(reader.GetValue(5)), Number(reader.GetValue(7)),
                    Number(reader.GetValue(11)), Number(reader.GetValue(14))));
            }

            break;
        } while (reader.NextResult());

        return foods;
    }

    private static double? Number(object? value)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return null;
        }

        return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static async Task CreateDatabaseAsync(string path, List<FoodRow> foods, FridaRelease release,
        CancellationToken cancellationToken)
    {
        // Closing a pooled connection retains its native file handle, preventing the staging file
        // from being replaced or deleted. Dispose this connection before returning to InstallAsync.
        var builder = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var schema = connection.CreateCommand())
        {
            schema.Transaction = transaction;
            schema.CommandText =
                "CREATE TABLE Foods (Id INTEGER PRIMARY KEY, NameDanish TEXT NOT NULL, NameEnglish TEXT, Kcal REAL, Protein REAL, Carbs REAL, Fat REAL); CREATE TABLE FridaMetadata (Version TEXT NOT NULL, ArticleId INTEGER NOT NULL, FileName TEXT NOT NULL, UpdatedUtc TEXT NOT NULL);";
            await schema.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                "INSERT INTO Foods (NameDanish, NameEnglish, Kcal, Protein, Carbs, Fat) VALUES ($dk, $en, $kcal, $protein, $carbs, $fat)";
            var dk = insert.Parameters.Add("$dk", SqliteType.Text);
            var en = insert.Parameters.Add("$en", SqliteType.Text);
            var kcal = insert.Parameters.Add("$kcal", SqliteType.Real);
            var protein = insert.Parameters.Add("$protein", SqliteType.Real);
            var carbs = insert.Parameters.Add("$carbs", SqliteType.Real);
            var fat = insert.Parameters.Add("$fat", SqliteType.Real);
            foreach (var food in foods)
            {
                dk.Value = food.Danish;
                en.Value = (object?)food.English ?? DBNull.Value;
                kcal.Value = (object?)food.Kcal ?? DBNull.Value;
                protein.Value = (object?)food.Protein ?? DBNull.Value;
                carbs.Value = (object?)food.Carbs ?? DBNull.Value;
                fat.Value = (object?)food.Fat ?? DBNull.Value;
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await using (var metadata = connection.CreateCommand())
        {
            metadata.Transaction = transaction;
            metadata.CommandText = "INSERT INTO FridaMetadata VALUES ($version, $article, $file, $updated)";
            metadata.Parameters.AddWithValue("$version", release.Version);
            metadata.Parameters.AddWithValue("$article", release.ArticleId);
            metadata.Parameters.AddWithValue("$file", release.FileName);
            metadata.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
            await metadata.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await connection.CloseAsync();
    }

    private sealed record FoodRow(
        string Danish,
        string? English,
        double? Kcal,
        double? Protein,
        double? Carbs,
        double? Fat);
}
