using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ExcelDataReader;
using MomentumGroves.Repositories;
using MomentumGroves.Models;

namespace MomentumGroves.Service;

public sealed class FridaUpdateService
{
    private const long KnownArticleId = 32312844;
    private const string ApiBase = "https://api.figshare.com/v2/articles";
    private readonly HttpClient _http;
    private readonly string _databasePath;
    private readonly ILogger<FridaUpdateService> _logger;
    private readonly FridaRepository _repository;
    private static readonly SemaphoreSlim InstallLock = new(1, 1);

    /// <summary>
    /// Initializes the service that checks for and installs verified Frida releases.
    /// </summary>
    /// <param name="http">The HTTP client used for provider requests.</param>
    /// <param name="environment">The environment that supplies the application content root.</param>
    /// <param name="logger">The logger for diagnostics and operation failures.</param>
    public FridaUpdateService(HttpClient http, IWebHostEnvironment environment, ILogger<FridaUpdateService> logger)
    {
        _http = http;
        _databasePath = Path.Combine(environment.ContentRootPath, "src", "Data", "frida.db");
        _logger = logger;
        _repository = new FridaRepository(_databasePath);
    }

    /// <summary>
    /// Compares the installed Frida version with the latest available release.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The installed version, latest release, and update availability.</returns>
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

    /// <summary>
    /// Downloads and installs the latest validated Frida release.
    /// </summary>
    /// <param name="request">The request containing the selected Frida release.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>Whether the installation succeeded and the resulting release details.</returns>
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

                await FridaRepository.CreateDatabaseAsync(stagedPath, foods, latest, cancellationToken);
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

    /// <summary>
    /// Removes an installation artifact and logs file access failures.
    /// </summary>
    /// <param name="path">The path to the file to process.</param>
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

    /// <summary>
    /// Reads the version and row count from the installed Frida dataset.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the dataset lookup.</param>
    /// <returns>The installed version and food count, or an empty version and zero before installation.</returns>
    public Task<(string Version, int Count)> GetInstalledAsync(CancellationToken cancellationToken)
    {
        return _repository.GetInstalledAsync(cancellationToken);
    }

    /// <summary>
    /// Finds DTU Frida Excel releases through the Figshare API.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The eligible Frida releases and their download metadata.</returns>
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

    /// <summary>
    /// Converts the release version text into a comparable version.
    /// </summary>
    /// <param name="value">The version string returned by the provider.</param>
    /// <returns>The normalized version used for release comparison.</returns>
    private static Version ParseVersion(string value)
    {
        return Version.TryParse(value, out var parsed) ? parsed : new Version(0, 0);
    }

    /// <summary>
    /// Extracts valid food rows from a Frida workbook.
    /// </summary>
    /// <param name="path">The path to the file to process.</param>
    /// <returns>The parsed food rows with available nutrient values.</returns>
    private static List<FridaFoodRow> ReadFoods(string path)
    {
        var foods = new List<FridaFoodRow>();
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
                foods.Add(new FridaFoodRow(name, english, Number(reader.GetValue(5)), Number(reader.GetValue(7)),
                    Number(reader.GetValue(11)), Number(reader.GetValue(14))));
            }

            break;
        } while (reader.NextResult());

        return foods;
    }

    /// <summary>
    /// Reads a numeric nutrient cell from the Frida workbook.
    /// </summary>
    /// <param name="value">The raw workbook cell value.</param>
    /// <returns>The parsed numeric cell value, or null if unavailable.</returns>
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
}
