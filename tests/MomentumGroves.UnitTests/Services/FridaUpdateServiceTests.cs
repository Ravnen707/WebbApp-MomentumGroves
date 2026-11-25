using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MomentumGroves.Models;
using MomentumGroves.Service;

namespace MomentumGroves.Tests.Services;

[TestFixture, NonParallelizable]
public class FridaUpdateServiceTests : DatabaseServiceTests
{
    private const long ArticleId = 32312844;
    private const string DownloadUrl = "https://files.figshare.com/frida.xlsx";
    private TestLogger<FridaUpdateService> _logger = null!;
    private TestHttp _http = null!;
    private HttpClient _client = null!;
    private object _article = null!;
    private Func<HttpResponseMessage> _download = null!;
    private static FridaInstallRequest InstallRequest => new(ArticleId, "7.0");

    /// <summary>
    /// Prepares the release fixtures and isolated Frida environment.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        // ExcelDataReader initializes its legacy fallback encoding even when reading XLSX.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _logger = new();
        CreateFrida();
        Sql("frida.db", "INSERT INTO Foods(NameDanish,Kcal) VALUES ('Original',42)");
        _article = Article();
        _download = () => new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new ByteArrayContent(FridaWorkbook.Create(500)) };
        UseHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri == DownloadUrl)
            {
                return _download();
            }
            if (request.Method == HttpMethod.Post)
            {
                return TestHttp.Response("[{\"id\":32312844},{}]");
            }
            return request.RequestUri.AbsolutePath.EndsWith(ArticleId.ToString())
                ? TestHttp.Response(JsonSerializer.Serialize(_article))
                : TestHttp.Response("", HttpStatusCode.NotFound);
        });
    }

    /// <summary>
    /// Disposes the fixture HTTP client after the update test.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _http.Dispose();
    }

    /// <summary>
    /// Replaces the HTTP handler used for Frida requests.
    /// </summary>
    /// <param name="handler">The test handler used to return scripted HTTP responses.</param>
    private void UseHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _client?.Dispose();
        _http?.Dispose();
        _http = new(handler);
        _client = _http.CreateClient("");
    }

    /// <summary>
    /// Creates the update service with the isolated database and fixture HTTP client.
    /// </summary>
    /// <returns>The isolated Frida update service.</returns>
    private FridaUpdateService Create()
    {
        return new(_client, Environment, _logger);
    }

    /// <summary>
    /// Builds a Figshare article response with one downloadable workbook.
    /// </summary>
    /// <param name="version">The release version used by the case.</param>
    /// <param name="title">The article title supplied to the provider fixture.</param>
    /// <param name="fileName">The workbook filename advertised by the provider.</param>
    /// <param name="downloadUrl">The workbook download URL advertised by the provider.</param>
    /// <returns>The serialized Figshare article fixture.</returns>
    private static object Article(string version = "7.0", string title = "Frida version", string? fileName = null,
        string? downloadUrl = DownloadUrl)
    {
        return new
    {
        title,
        files = new[]
            { new { name = fileName ?? "FCDB_" + version.Replace('.', '_') + ".xlsx", download_url = downloadUrl } }
    };
    }

    /// <summary>
    /// Checks that a legacy Frida database reports the original version and the stored food count.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task GetInstalled_LegacyDatabase_ReturnsDefaultVersionAndCount()
    {
        var installed = await Create().GetInstalledAsync(default);
        Assert.That(installed.Version, Is.EqualTo("6.1"));
        Assert.That(installed.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Checks that the installed version comes from Frida release metadata when present.
    /// </summary>
    /// <param name="version">The release version used by the case.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("7.2")]
    [TestCase("invalid-version")]
    public async Task GetInstalled_MetadataVersion_IsReturned(string version)
    {
        Sql("frida.db", "CREATE TABLE FridaMetadata(Version TEXT); INSERT INTO FridaMetadata VALUES($version)",
            ("$version", version));
        Assert.That((await Create().GetInstalledAsync(default)).Version, Is.EqualTo(version));
    }

    /// <summary>
    /// Checks that empty release metadata retains the legacy version.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task GetInstalled_EmptyMetadata_FallsBackToLegacyVersion()
    {
        Sql("frida.db", "CREATE TABLE FridaMetadata(Version TEXT)");
        Assert.That((await Create().GetInstalledAsync(default)).Version, Is.EqualTo("6.1"));
    }

    /// <summary>
    /// Checks that an absent Frida database is reported without creating an empty file.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task GetInstalled_MissingDatabase_ReturnsNotInstalledWithoutCreatingFile()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(DataPath("frida.db"));
        Assert.That(await Create().GetInstalledAsync(default), Is.EqualTo(("", 0)));
        Assert.That(File.Exists(DataPath("frida.db")), Is.False);
    }

    /// <summary>
    /// Checks that the release check offers first-time installation when the database is absent.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Check_MissingDatabase_ReportsAvailableInstallation()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(DataPath("frida.db"));
        var result = await Create().CheckAsync(default);
        Assert.That(result.Success, Is.True);
        Assert.That(result.UpdateAvailable, Is.True);
        Assert.That(result.FoodCount, Is.Zero);
        Assert.That(File.Exists(DataPath("frida.db")), Is.False);
    }

    /// <summary>
    /// Checks that first-time installation creates a populated Frida database.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Install_MissingDatabase_CreatesPopulatedDatabase()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(DataPath("frida.db"));
        var result = await Create().InstallAsync(InstallRequest, default);
        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(File.Exists(DataPath("frida.db")), Is.True);
        Assert.That(await Create().GetInstalledAsync(default), Is.EqualTo(("7.0", 500)));
        Assert.That(File.Exists(DataPath("frida.db.backup")), Is.False);
    }

    /// <summary>
    /// Checks whether the provider release is newer than the installed version.
    /// </summary>
    /// <param name="version">The release version used by the case.</param>
    /// <param name="newer">Whether the fixture release should be reported as newer.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("7.0", true)]
    [TestCase("6.1", false)]
    [TestCase("5.0", false)]
    public async Task Check_ReportsWhetherReleaseIsNewer(string version, bool newer)
    {
        _article = Article(version);
        var result = await Create().CheckAsync(default);
        Assert.That(result.Success, Is.True);
        Assert.That(result.UpdateAvailable, Is.EqualTo(newer));
        Assert.That(result.Latest!.Version, Is.EqualTo(version));
        Assert.That(result.FoodCount, Is.EqualTo(1));
        Assert.That(result.InstalledVersion, Is.EqualTo("6.1"));
        var searches = _http.Requests.Where(r => r.Method == HttpMethod.Post).ToList();
        Assert.That(searches, Has.Count.EqualTo(2));
        Assert.That(
            searches.All(r => JsonDocument.Parse(r.Body).RootElement.GetProperty("page_size").GetInt32() == 100),
            Is.True);
    }

    /// <summary>
    /// Checks that an invalid installed version compares as zero.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Check_InvalidInstalledVersion_IsTreatedAsZero()
    {
        Sql("frida.db", "CREATE TABLE FridaMetadata(Version TEXT); INSERT INTO FridaMetadata VALUES('unknown')");
        Assert.That((await Create().CheckAsync(default)).UpdateAvailable, Is.True);
    }

    /// <summary>
    /// Checks that release discovery interprets underscores and selects the highest numeric version.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Check_SelectsNewestReleaseNumericallyAndNormalizesUnderscores()
    {
        _article = new
        {
            title = "The Danish Food Composition Database",
            files = new[]
            {
                new { name = "FCDB_7_2.xlsx", download_url = DownloadUrl },
                new { name = "FCDB_7_10.xlsx", download_url = DownloadUrl }
            }
        };
        Assert.That((await Create().CheckAsync(default)).Latest!.Version, Is.EqualTo("7.10"));
    }

    /// <summary>
    /// Checks that release discovery ignores invalid or untrusted download URLs.
    /// </summary>
    /// <param name="url">The provider URL to validate.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("http://files.figshare.com/file.xlsx")]
    [TestCase("https://untrusted.test/file.xlsx")]
    [TestCase("not-a-url")]
    [TestCase("")]
    public async Task Check_RejectsUntrustedOrInvalidDownloadUrls(string url)
    {
        _article = Article(downloadUrl: url);
        var result = await Create().CheckAsync(default);
        Assert.That(result.Success, Is.False);
        Assert.That(result.Latest, Is.Null);
    }

    /// <summary>
    /// Checks that release discovery accepts trusted HTTPS download URLs.
    /// </summary>
    /// <param name="url">The provider URL to validate.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("https://files.figshare.com/file.xlsx")]
    [TestCase("https://data.dtu.dk/file.xlsx")]
    public async Task Check_AcceptsHttpsTrustedHosts(string url)
    {
        _article = Article(downloadUrl: url);
        Assert.That((await Create().CheckAsync(default)).Latest!.DownloadUrl, Is.EqualTo(url));
    }

    /// <summary>
    /// Checks that discovery ignores files missing the Excel extension or release version.
    /// </summary>
    /// <param name="name">The name supplied by the case.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("notes.txt")]
    [TestCase("unknown.xlsx")]
    public async Task Check_IgnoresFilesWithoutExcelExtensionOrVersion(string name)
    {
        _article = new { title = "Frida", files = new[] { new { name, download_url = DownloadUrl } } };
        Assert.That((await Create().CheckAsync(default)).Success, Is.False);
    }

    /// <summary>
    /// Checks that discovery ignores an unrelated article title.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Check_UnrelatedArticleTitle_IsIgnored()
    {
        _article = Article(title: "An unrelated dataset");
        Assert.That((await Create().CheckAsync(default)).Success, Is.False);
    }

    /// <summary>
    /// Checks that a malformed article is logged and does not prevent another release from being found.
    /// </summary>
    /// <param name="article">The malformed article payload to test.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("broken")]
    [TestCase("{}")]
    public async Task Check_MalformedArticle_LogsWarningAndContinues(string article)
    {
        UseHandler(request => request.Method == HttpMethod.Post ? TestHttp.Response("[]") : TestHttp.Response(article));
        var result = await Create().CheckAsync(default);
        Assert.That(result.Success, Is.False);
        Assert.That(_logger.Entries.Any(e => e.Exception is not null && e.Level == LogLevel.Warning), Is.True);
    }

    /// <summary>
    /// Checks that a failed HTTP search is reported using a safe error message.
    /// </summary>
    /// <param name="status">The HTTP status used in the fixture response.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase(429)]
    [TestCase(500)]
    public async Task Check_SearchHttpFailure_ReturnsSafeError(int status)
    {
        UseHandler(_ => TestHttp.Response("internal-provider-error", (HttpStatusCode)status));
        var result = await Create().CheckAsync(default);
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Not.Contain("internal-provider-error"));
        Assert.That(_logger.Entries.Single().Exception, Is.TypeOf<HttpRequestException>());
    }

    /// <summary>
    /// Checks that an unreadable database yields a release check failure response.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Check_LocalDatabaseFailure_ReturnsFailureInsteadOfThrowing()
    {
        Sql("frida.db", "DROP TABLE Foods");
        var result = await Create().CheckAsync(default);
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Not.Contain("SQLite"));
        Assert.That(_logger.Entries.Single().Exception, Is.TypeOf<SqliteException>());
    }

    /// <summary>
    /// Checks that a stale or unknown release cannot start a download.
    /// </summary>
    /// <param name="id">The submitted Figshare article ID.</param>
    /// <param name="version">The release version used by the case.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase(999, "7.0")]
    [TestCase(32312844, "6.0")]
    public async Task Install_StaleOrUntrustedSelection_IsRejectedWithoutDownload(long id, string version)
    {
        Assert.That((await Create().InstallAsync(new(id, version), default)).Success, Is.False);
        Assert.That(_http.Requests.Any(r => r.Uri.AbsoluteUri == DownloadUrl), Is.False);
        Assert.That((await Create().GetInstalledAsync(default)).Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Checks that installation is rejected when no eligible releases exist.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Install_NoReleases_IsRejected()
    {
        _article = Article(fileName: "notes.txt");
        Assert.That((await Create().InstallAsync(InstallRequest, default)).Success, Is.False);
    }

    /// <summary>
    /// Checks that installing an equal or older release preserves the database without downloading.
    /// </summary>
    /// <param name="version">The release version used by the case.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("6.1")]
    [TestCase("5.0")]
    public async Task Install_NotNewer_PreservesDatabaseAndDoesNotDownload(string version)
    {
        _article = Article(version);
        var result = await Create().InstallAsync(new(ArticleId, version), default);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Version, Is.EqualTo("6.1"));
        Assert.That(_http.Requests.Any(r => r.Uri.AbsoluteUri == DownloadUrl), Is.False);
    }

    /// <summary>
    /// Checks that installing a valid workbook replaces the database and retains its previous contents as a backup.
    /// </summary>
    /// <param name="count">The number of food rows to install.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase(500)]
    [TestCase(501)]
    public async Task Install_ValidWorkbook_AtomicallyReplacesDatabaseAndKeepsBackup(int count)
    {
        // A completed food search must not leave the destination database locked either.
        var foodService = new FoodService(Environment, new TestLogger<FoodService>());
        Assert.That((await foodService.SearchAsync("Original")).Success, Is.True);
        _download = () => new(HttpStatusCode.OK) { Content = new ByteArrayContent(FridaWorkbook.Create(count)) };
        // A stale staging file must be removed before a fresh database is built.
        File.WriteAllText(DataPath("frida.db.staging"), "stale");
        var result = await Create().InstallAsync(InstallRequest, default);
        Assert.That(result.Success, Is.True, () => result.Message + System.Environment.NewLine +
                                                   string.Join(System.Environment.NewLine, _logger.Entries
                                                       .Where(entry => entry.Exception is not null)
                                                       .Select(entry => entry.Exception!.ToString())));
        Assert.That(result.FoodCount, Is.EqualTo(count));
        Assert.That(result.Version, Is.EqualTo("7.0"));
        Assert.That(File.Exists(DataPath("frida.db.backup")), Is.True);
        Assert.That(File.Exists(DataPath("frida.db.staging")), Is.False);
        // The production services must release handles themselves; no test-side pool clearing.
        Assert.That(await Create().GetInstalledAsync(default), Is.EqualTo(("7.0", count)));
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = DataPath("frida.db"), Pooling = false }.ToString());
        connection.Open();
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT NameDanish FROM Foods ORDER BY Id LIMIT 1";
        Assert.That(query.ExecuteScalar(), Is.EqualTo("Food 1"));
        query.CommandText = "SELECT Kcal FROM Foods ORDER BY Id LIMIT 1";
        Assert.That(query.ExecuteScalar(), Is.EqualTo(100.5));
        query.CommandText = "SELECT Kcal FROM Foods ORDER BY Id LIMIT 1 OFFSET 1";
        Assert.That(query.ExecuteScalar(), Is.EqualTo(DBNull.Value));
    }

    /// <summary>
    /// Checks that a workbook with too few valid foods preserves the installed database.
    /// </summary>
    /// <param name="count">The number of food rows in the undersized workbook.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase(0)]
    [TestCase(499)]
    public async Task Install_TooFewValidFoods_PreservesOriginal(int count)
    {
        _download = () => new(HttpStatusCode.OK) { Content = new ByteArrayContent(FridaWorkbook.Create(count)) };
        var result = await Create().InstallAsync(InstallRequest, default);
        Assert.That(result.Success, Is.False);
        Assert.That((await Create().GetInstalledAsync(default)).Count, Is.EqualTo(1));
        Assert.That(_logger.Entries.Any(e => e.Exception is InvalidDataException), Is.True);
    }

    /// <summary>
    /// Checks that a workbook without the nutrition worksheet preserves the installed database.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Install_WorkbookWithoutDataTable_PreservesOriginal()
    {
        _download = () => new(HttpStatusCode.OK) { Content = new ByteArrayContent(FridaWorkbook.Create(500, false)) };
        Assert.That((await Create().InstallAsync(InstallRequest, default)).Success, Is.False);
        Assert.That((await Create().GetInstalledAsync(default)).Count, Is.EqualTo(1));
    }

    /// <summary>
    /// Checks that a failed workbook download reports a safe error and permits a retry.
    /// </summary>
    /// <param name="failure">The failure mode simulated by the test fixture.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("http")]
    [TestCase("invalid-excel")]
    [TestCase("oversized-header")]
    public async Task Install_DownloadFailure_IsSafeAndReleasesLockForRetry(string failure)
    {
        _download = () =>
        {
            var response = TestHttp.Response("private-download-error",
                failure == "http" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
            if (failure == "oversized-header")
            {
                response.Content.Headers.ContentLength = 100_000_001;
            }
            return response;
        };
        var result = await Create().InstallAsync(InstallRequest, default);
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Not.Contain("private-download-error"));
        Assert.That((await Create().GetInstalledAsync(default)).Count, Is.EqualTo(1));
        // A stale selection exits quickly, proving the failed install released the process-wide lock.
        var retry = await Create().InstallAsync(new(0, "stale"), default);
        Assert.That(retry.Message, Does.Not.Contain("already running"));
    }

    /// <summary>
    /// Checks that an oversized download without a declared length leaves the installed database intact.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Install_StreamWithoutContentLength_ExceedingSizeLimit_PreservesDatabase()
    {
        var originalDatabase = File.ReadAllBytes(DataPath("frida.db"));
        using var source = new OversizedDownloadStream();
        _download = () =>
        {
            var content = new StreamContent(source);
            Assert.That(content.Headers.ContentLength, Is.Null,
                "This test must exercise the post-download size check, not the Content-Length check.");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        };

        var result = await Create().InstallAsync(InstallRequest, default);

        Assert.That(result.Success, Is.False);
        Assert.That(source.BytesRead, Is.EqualTo(100_000_001));
        var error = _logger.Entries.Single(entry => entry.Level == LogLevel.Error);
        Assert.That(error.Exception, Is.TypeOf<InvalidDataException>());
        Assert.That(error.Exception!.Message, Is.EqualTo("The Excel file exceeds the 100 MB size limit."));
        Assert.That(File.ReadAllBytes(DataPath("frida.db")), Is.EqualTo(originalDatabase));
        Assert.That(File.Exists(DataPath("frida.db.staging")), Is.False);
        Assert.That(File.Exists(DataPath("frida.db.backup")), Is.False);
    }

    /// <summary>
    /// Checks that a concurrent install reports busy and cancellation makes the lock available again.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Install_ConcurrentRequest_ReturnsBusy_ThenCancellationReleasesLock()
    {
        using var entered = new SemaphoreSlim(0);
        using var cts = new CancellationTokenSource();
        _client.Dispose();
        _http.Dispose();
        _http = new(async (_, token) =>
        {
            entered.Release();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return TestHttp.Response("[]");
        });
        _client = _http.CreateClient("");
        var service = Create();
        var first = service.InstallAsync(InstallRequest, cts.Token);
        try
        {
            Assert.That(await entered.WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
            var second = await service.InstallAsync(InstallRequest, default);
            Assert.That(second.Success, Is.False);
            Assert.That(second.Message, Does.Contain("already running"));
        }
        finally
        {
            cts.Cancel();
            try
            {
                await first;
            }
            catch (OperationCanceledException)
            {
            }
        }

        Assert.That(_logger.Entries.All(e => e.Level != LogLevel.Error), Is.True);
    }

    /// <summary>
    /// Checks that cancellation propagates without being logged as an operation error.
    /// </summary>
    /// <param name="operation">The operation exercised by the case.</param>
    [TestCase("check")]
    [TestCase("install")]
    [TestCase("installed")]
    public void CallerCancellation_PropagatesWithoutErrorLogging(string operation)
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var service = Create();
        Assert.CatchAsync<OperationCanceledException>(() => operation switch
        {
            "check" => (Task)service.CheckAsync(cts.Token),
            "install" => service.InstallAsync(InstallRequest, cts.Token),
            _ => service.GetInstalledAsync(cts.Token)
        });
        Assert.That(_logger.Entries, Is.Empty);
    }

    // Generates bytes on demand with bounded memory. Running the test writes approximately
    // 100 MB to the service's temporary download file, never to an application database.
    private sealed class OversizedDownloadStream : Stream
    {
        public long BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => BytesRead;
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// Produces synthetic bytes up to the stream's configured length.
        /// </summary>
        /// <param name="buffer">The buffer that would receive or provide bytes.</param>
        /// <param name="offset">The byte offset within the buffer or the requested seek distance.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <returns>The number of bytes produced, or zero at the end.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            var available = (int)Math.Min(count, 100_000_001 - BytesRead);
            Array.Clear(buffer, offset, available);
            BytesRead += available;
            return available;
        }

        /// <summary>
        /// Rejects flush requests on the read-only test stream.
        /// </summary>
        public override void Flush()
        {
        }

        /// <summary>
        /// Rejects seeking in the sequential test stream.
        /// </summary>
        /// <param name="offset">The byte offset within the buffer or the requested seek distance.</param>
        /// <param name="origin">The reference point for the attempted seek.</param>
        /// <returns>This operation always throws.</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }
        /// <summary>
        /// Rejects length changes on the read-only test stream.
        /// </summary>
        /// <param name="value">The requested new stream length.</param>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
        /// <summary>
        /// Rejects writes to the read-only test stream.
        /// </summary>
        /// <param name="buffer">The buffer that would receive or provide bytes.</param>
        /// <param name="offset">The byte offset within the buffer or the requested seek distance.</param>
        /// <param name="count">The number of bytes in the attempted write.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
