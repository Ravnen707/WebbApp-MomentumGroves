using System.Net;
using System.Text.Json;
using MomentumGroves.Models;
using MomentumGroves.Service;

namespace MomentumGroves.Tests.Services;

[TestFixture, NonParallelizable]
public class RouteServiceTests
{
    private const string Geometry = "{\"type\":\"LineString\",\"coordinates\":[[12.5,55.5],[12.6,55.6]]}";
    private TestHttp _http = null!;
    private TestLogger<RouteService> _logger = null!;
    private string? _previousKey;
    private static RouteGenerateRequest Request => new(55.5, 12.5, 5, "outside", "walk");

    private static string Route(double metres = 5012) =>
        "{\"features\":[{\"properties\":{\"summary\":{\"distance\":" +
        metres.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}},\"geometry\":" + Geometry + "}]}";

    [SetUp]
    public void SetUp()
    {
        _previousKey = System.Environment.GetEnvironmentVariable("ORS_API_KEY");
        System.Environment.SetEnvironmentVariable("ORS_API_KEY", null);
        _logger = new();
        _http = new(_ => TestHttp.Response(Route()));
    }

    [TearDown]
    public void TearDown()
    {
        _http.Dispose();
        System.Environment.SetEnvironmentVariable("ORS_API_KEY", _previousKey);
    }

    private RouteService Create(string? key = "secret-key", string? search = "https://search.test/search",
        string? routes = "https://routes.test/") => new(_http,
        TestHttp.Configuration(("OpenRouteService:ApiKey", key), ("Routes:NominatimSearchUrl", search),
            ("Routes:OrsBaseUrl", routes)), _logger);

    [TestCase("Copenhagen")]
    [TestCase("æ ø & ? + # / 東京")]
    [TestCase("")]
    public async Task Search_EncodesQueryAndSetsProviderOptionsAndUserAgent(string query)
    {
        var response = "[{\"display_name\":\"Copenhagen\"}]";
        _http = new(_ => TestHttp.Response(response));
        Assert.That(await Create().SearchAsync(query, default), Is.EqualTo(response));
        var sent = _http.Requests.Single();
        Assert.That(sent.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(sent.Uri.AbsoluteUri,
            Is.EqualTo(
                "https://search.test/search?format=json&limit=5&countrycodes=dk&q=" + Uri.EscapeDataString(query)));
        Assert.That(sent.Headers["User-Agent"], Does.Contain("MomentumGroves/1.0"));
        Assert.That(_logger.Entries, Is.Empty);
    }

    [TestCase(400)]
    [TestCase(401)]
    [TestCase(429)]
    [TestCase(500)]
    [TestCase(503)]
    public void Search_UnsuccessfulHttp_LogsAndRethrows(int status)
    {
        _http = new(_ => TestHttp.Response("private-provider-response", (HttpStatusCode)status));
        var exception =
            Assert.ThrowsAsync<HttpRequestException>(() => Create().SearchAsync("private-address", default));
        Assert.That((int)exception!.StatusCode!, Is.EqualTo(status));
        _logger.AssertError(exception, "SearchAsync");
        Assert.That(_logger.Entries.Single().Message, Does.Not.Contain("private-address"));
    }

    [Test]
    public void Search_MissingUrl_LogsConfigurationFailure()
    {
        var exception =
            Assert.ThrowsAsync<InvalidOperationException>(() => Create(search: null).SearchAsync("town", default));
        _logger.AssertError(exception!, "SearchAsync");
        Assert.That(_http.Requests, Is.Empty);
    }

    [TestCase("not-a-url")]
    [TestCase("/relative")]
    public void Search_InvalidUrl_FailsAndLogs(string url)
    {
        Assert.CatchAsync<Exception>(() => Create(search: url).SearchAsync("town", default));
        Assert.That(_logger.Entries.Single().Message, Does.Contain("SearchAsync"));
    }

    [Test]
    public async Task Generate_RequestsThreeDifferentSeeds_AndMapsDistanceAndGeometry()
    {
        var results = await Create().GenerateAsync(Request, default);
        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results.Select(r => r.DistanceKm), Is.All.EqualTo(5.01));
        Assert.That(results.Select(r => r.GeoJson), Is.All.EqualTo(Geometry));
        Assert.That(_http.Requests.Select(r => JsonDocument.Parse(r.Body).RootElement
                .GetProperty("options").GetProperty("round_trip").GetProperty("seed").GetInt32()),
            Is.EqualTo(new[] { 11, 29, 47 }));
        foreach (var sent in _http.Requests)
        {
            Assert.That(sent.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(sent.Uri.AbsoluteUri, Is.EqualTo("https://routes.test/v2/directions/foot-walking/geojson"));
            Assert.That(sent.Headers["Authorization"], Is.EqualTo("secret-key"));
            using var body = JsonDocument.Parse(sent.Body);
            Assert.That(body.RootElement.GetProperty("coordinates")[0][0].GetDouble(), Is.EqualTo(12.5));
            Assert.That(body.RootElement.GetProperty("coordinates")[0][1].GetDouble(), Is.EqualTo(55.5));
            Assert.That(body.RootElement.GetProperty("instructions").GetBoolean(), Is.False);
            Assert.That(
                body.RootElement.GetProperty("options").GetProperty("round_trip").GetProperty("points").GetInt32(),
                Is.EqualTo(5));
        }
    }

    [TestCase("outside")]
    [TestCase("inside")]
    [TestCase(null)]
    public async Task Generate_EnvironmentOptions_UseRecommendedPreference(string? environment)
    {
        await Create().GenerateAsync(Request with { Environment = environment! }, default);
        using var json = JsonDocument.Parse(_http.Requests[0].Body);
        Assert.That(json.RootElement.GetProperty("preference").GetString(), Is.EqualTo("recommended"));
    }

    [TestCase(0.5, 500)]
    [TestCase(40, 40000)]
    [TestCase(1.2345, 1234)]
    [TestCase(1.2355, 1236)]
    public async Task Generate_TargetDistance_ConvertsToRoundedMetres(double km, int metres)
    {
        await Create().GenerateAsync(Request with { DistanceKm = km }, default);
        using var json = JsonDocument.Parse(_http.Requests[0].Body);
        Assert.That(json.RootElement.GetProperty("options").GetProperty("round_trip").GetProperty("length").GetInt32(),
            Is.EqualTo(metres));
    }

    [TestCase(0, 0)]
    [TestCase(1234, 1.23)]
    [TestCase(1236, 1.24)]
    public async Task Generate_ResponseDistance_ConvertsAndRounds(double metres, double expectedKm)
    {
        _http = new(_ => TestHttp.Response(Route(metres)));
        Assert.That((await Create().GenerateAsync(Request, default))[0].DistanceKm, Is.EqualTo(expectedKm));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void Generate_MissingKey_RejectsBeforeHttp(string? key)
    {
        var exception =
            Assert.ThrowsAsync<InvalidOperationException>(() => Create(key).GenerateAsync(Request, default));
        _logger.AssertError(exception!, "GenerateAsync");
        Assert.That(_http.Requests, Is.Empty);
    }

    [Test]
    public async Task Generate_MissingConfiguredKey_UsesEnvironmentKey()
    {
        System.Environment.SetEnvironmentVariable("ORS_API_KEY", "environment-key");
        await Create(key: null).GenerateAsync(Request, default);
        Assert.That(_http.Requests[0].Headers["Authorization"], Is.EqualTo("environment-key"));
    }

    [Test]
    public async Task Generate_ConfiguredKey_TakesPrecedenceOverEnvironment()
    {
        System.Environment.SetEnvironmentVariable("ORS_API_KEY", "environment-key");
        await Create().GenerateAsync(Request, default);
        Assert.That(_http.Requests[0].Headers["Authorization"], Is.EqualTo("secret-key"));
    }

    [Test]
    public void Generate_MissingBaseUrl_IsLogged()
    {
        var exception =
            Assert.ThrowsAsync<InvalidOperationException>(() => Create(routes: null).GenerateAsync(Request, default));
        _logger.AssertError(exception!, "GenerateAsync");
    }

    [TestCase(400)]
    [TestCase(401)]
    [TestCase(429)]
    [TestCase(500)]
    public void Generate_FirstProviderFailure_ReturnsSafeException(int status)
    {
        _http = new(_ => TestHttp.Response("private-provider-response", (HttpStatusCode)status));
        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => Create().GenerateAsync(Request, default));
        Assert.That(exception!.Message, Does.Contain(status.ToString()));
        Assert.That(exception.Message, Does.Not.Contain("private-provider-response"));
        _logger.AssertError(exception, "GenerateAsync");
        Assert.That(_http.Requests, Has.Count.EqualTo(1));
    }

    [TestCase(1)]
    [TestCase(2)]
    public async Task Generate_LaterHttpFailure_ReturnsCompletedRoutesAndWarning(int successes)
    {
        var calls = 0;
        _http = new(_ =>
            ++calls <= successes
                ? TestHttp.Response(Route())
                : TestHttp.Response("failure", HttpStatusCode.TooManyRequests));
        Assert.That(await Create().GenerateAsync(Request, default), Has.Count.EqualTo(successes));
        Assert.That(_http.Requests, Has.Count.EqualTo(successes + 1));
        Assert.That(_logger.Entries.Single().Level, Is.EqualTo(Microsoft.Extensions.Logging.LogLevel.Warning));
    }

    [TestCase("broken")]
    [TestCase("{}")]
    [TestCase("{\"features\":[]}")]
    [TestCase("{\"features\":[{}]}")]
    [TestCase("{\"features\":[{\"properties\":{\"summary\":{\"distance\":\"bad\"}}}]}")]
    [TestCase("{\"features\":[{\"properties\":{\"summary\":{\"distance\":1000}}}]}")]
    public void Generate_MalformedProviderData_IsLoggedAndPropagated(string content)
    {
        _http = new(_ => TestHttp.Response(content));
        var exception = Assert.CatchAsync<Exception>(() => Create().GenerateAsync(Request, default));
        _logger.AssertError(exception!, "GenerateAsync");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TransportFailure_OriginalExceptionIsPreserved(bool generate)
    {
        var original = new HttpRequestException("network offline");
        _http = new((_, _) => Task.FromException<HttpResponseMessage>(original));
        var exception = Assert.ThrowsAsync<HttpRequestException>(() => Execute(generate));
        Assert.That(exception, Is.SameAs(original));
        _logger.AssertError(original, generate ? "GenerateAsync" : "SearchAsync");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TimeoutWithoutCallerCancellation_IsLogged(bool generate)
    {
        _http = new((_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException("provider timeout")));
        var exception = Assert.CatchAsync<OperationCanceledException>(() => Execute(generate));
        _logger.AssertError(exception!, generate ? "GenerateAsync" : "SearchAsync");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CallerCancellation_PropagatesWithoutErrorLogging(bool generate)
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.CatchAsync<OperationCanceledException>(() => Execute(generate, cts.Token));
        Assert.That(_logger.Entries, Is.Empty);
    }

    private Task Execute(bool generate, CancellationToken token = default) => generate
        ? Create().GenerateAsync(Request, token)
        : Create().SearchAsync("town", token);
}