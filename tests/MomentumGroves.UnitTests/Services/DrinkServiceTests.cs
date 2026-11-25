using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MomentumGroves.Service;

namespace MomentumGroves.Tests.Services;

[TestFixture, NonParallelizable]
public class DrinkServiceTests
{
    private const string Product = "{\"product_name\":\"Water\",\"nutriments\":{\"energy-kcal_100ml\":0}}";
    private TestHttp _http = null!;
    private MemoryCache _cache = null!;

    private TestLogger<DrinkService> _logger = null!;

    // Isolate the process-wide rate limiter without adding a production test hook or waiting seven seconds per case.
    private static readonly FieldInfo LastSearch =
        typeof(DrinkService).GetField("_lastSearch", BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Creates the fake provider, cache, and logger for each search test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        LastSearch.SetValue(null, DateTimeOffset.MinValue);
        _http = new(_ => TestHttp.Response("[" + Product + "]"));
        _cache = new(new MemoryCacheOptions());
        _logger = new();
    }

    /// <summary>
    /// Disposes the fixture HTTP client and cache.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        _http.Dispose();
        _cache.Dispose();
        LastSearch.SetValue(null, DateTimeOffset.MinValue);
    }

    /// <summary>
    /// Creates a drink search service with the fixture client, cache, and logger.
    /// </summary>
    /// <returns>The service configured to use the fixture dependencies.</returns>
    private DrinkService Create()
    {
        return new(_http, _cache, _logger);
    }

    /// <summary>
    /// Checks that a blank drink search succeeds without making a provider request.
    /// </summary>
    /// <param name="query">The search text supplied to the service.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("")]
    [TestCase("  ")]
    public async Task Search_BlankQuery_IsSuccessfulWithoutHttp(string query)
    {
        var result = await Create().SearchAsync(query);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Drinks, Is.Empty);
        Assert.That(_http.Requests, Is.Empty);
    }

    /// <summary>
    /// Checks that drink search uses its registered client and URL-encodes the trimmed query.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Search_UsesNamedClient_AndEncodesTrimmedQuery()
    {
        await Create().SearchAsync(" æ & ? 東京 ");
        Assert.That(_http.ClientNames, Is.EqualTo(new[] { "openfoodfacts" }));
        Assert.That(_http.Requests.Single().Uri.AbsoluteUri, Is.EqualTo(
            "https://search.openfoodfacts.org/search?q=" + Uri.EscapeDataString("æ & ? 東京") +
                "&page_size=20&langs=da,en"));
    }

    /// <summary>
    /// Checks that queries differing only in case or surrounding whitespace share a cache entry.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Search_CacheKey_IsTrimmedAndCaseInsensitive()
    {
        var service = Create();
        var first = await service.SearchAsync(" Water ");
        var second = await service.SearchAsync("WATER");
        Assert.That(second.Drinks, Is.SameAs(first.Drinks));
        Assert.That(_http.Requests, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Checks that drink search accepts each supported JSON response container.
    /// </summary>
    /// <param name="template">The JSON container template for a provider response.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("[{0}]")]
    [TestCase("{\"hits\":[{0}]}")]
    [TestCase("{\"hits\":{\"hits\":[{0}]}}")]
    [TestCase("{\"products\":[{0}]}")]
    [TestCase("{\"results\":[{0}]}")]
    public async Task Search_AcceptsSupportedResponseContainers(string template)
    {
        _http = new(_ => TestHttp.Response(template.Replace("{0}", Product)));
        var result = await Create().SearchAsync("water");
        Assert.That(result.Success, Is.True);
        Assert.That(result.Drinks.Single().Name, Is.EqualTo("Water"));
    }

    /// <summary>
    /// Checks that the parser recognizes the supported ways of wrapping a product object.
    /// </summary>
    /// <param name="wrapper">The JSON structure that wraps a provider product.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("_source")]
    [TestCase("source")]
    [TestCase("product")]
    public async Task Search_UnwrapsSupportedProductObjects(string wrapper)
    {
        _http = new(_ => TestHttp.Response("[{\"" + wrapper + "\":" + Product + "}]"));
        Assert.That((await Create().SearchAsync("water")).Drinks.Single().Name,
            Is.EqualTo("Water"));
    }

    /// <summary>
    /// Checks that malformed provider responses produce a safe failure and a log entry.
    /// </summary>
    /// <param name="response">The simulated provider response body.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("null")]
    [TestCase("42")]
    [TestCase("{}")]
    [TestCase("broken")]
    [TestCase("{\"hits\":false}")]
    [TestCase("{\"hits\":{\"hits\":{}}}")]
    [TestCase("{\"products\":{}}")]
    [TestCase("{\"results\":null}")]
    public async Task Search_InvalidResponse_ReturnsSafeFailureAndLogs(string response)
    {
        _http = new(_ => TestHttp.Response(response));
        var result = await Create().SearchAsync("private-query");
        Assert.That(result.Success, Is.False);
        Assert.That(result.Drinks, Is.Empty);
        Assert.That(result.Error, Does.Not.Contain(response));
        Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Error));
        Assert.That(_logger.Entries.Single().Message, Does.Not.Contain("private-query"));
    }

    /// <summary>
    /// Checks that an HTTP error is reported and its status is logged.
    /// </summary>
    /// <param name="status">The HTTP status used in the fixture response.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase(400)]
    [TestCase(429)]
    [TestCase(500)]
    [TestCase(503)]
    public async Task Search_HttpFailure_ReturnsFailureAndLogsStatus(int status)
    {
        _http = new(_ => TestHttp.Response("private-response", (HttpStatusCode)status));
        var result = await Create().SearchAsync("water");
        Assert.That(result.Success, Is.False);
        Assert.That(((HttpRequestException)_logger.Entries.Single().Exception!).StatusCode,
            Is.EqualTo((HttpStatusCode)status));
        Assert.That(result.Error, Does.Not.Contain("private-response"));
    }

    /// <summary>
    /// Checks that failed requests are not cached and that retrying can reach the provider.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Search_FailureIsNotCached_AndGateIsReleasedForRetry()
    {
        var calls = 0;
        _http = new(_ =>
            ++calls == 1 ? TestHttp.Response("failure",
                HttpStatusCode.ServiceUnavailable) : TestHttp.Response("[" + Product + "]"));
        var service = Create();
        Assert.That((await service.SearchAsync("water")).Success, Is.False);
        LastSearch.SetValue(null, DateTimeOffset.MinValue);
        Assert.That((await service.SearchAsync("water")).Success, Is.True);
        Assert.That(calls, Is.EqualTo(2));
    }

    /// <summary>
    /// Checks which product name is used when the preferred provider field is absent.
    /// </summary>
    /// <param name="field">The field set by this case.</param>
    /// <param name="name">The name supplied by the case.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("product_name", "Original")]
    [TestCase("product_name_da", "Dansk")]
    [TestCase("product_name_en", "English")]
    public async Task Search_NameFallbacks(string field, string name)
    {
        var product =
            "{\"product_name\":null,\"" + field + "\":\"" + name + "\",\"nutriments\":{\"energy-kcal_100ml\":1}}";
        _http = new(_ => TestHttp.Response("[" + product + "]"));
        Assert.That((await Create().SearchAsync("name")).Drinks.Single().Name, Is.EqualTo(name));
    }

    /// <summary>
    /// Checks the display name when a product has a brand or a brand already in its name.
    /// </summary>
    /// <param name="brand">The brand supplied by the provider fixture.</param>
    /// <param name="name">The name supplied by the case.</param>
    /// <param name="expected">The expected display name after applying the brand rules.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("\"Brand\"", "Water", "Brand – Water")]
    [TestCase("\"brand\"", "Brand Water", "Brand Water")]
    [TestCase("[null,7,\" \",\"Brand\",\"Other\"]", "Water", "Brand – Water")]
    [TestCase("42", "Water", "Water")]
    [TestCase("null", "Water", "Water")]
    [TestCase("[]", "Water", "Water")]
    public async Task Search_BrandHandling(string brand, string name, string expected)
    {
        _http = new(_ => TestHttp.Response("[{\"product_name\":\"" + name + "\",\"brands\":" + brand +
                                           ",\"nutriments\":{\"energy-kcal_100ml\":1}}]"));
        Assert.That((await Create().SearchAsync("brand")).Drinks.Single().Name, Is.EqualTo(expected));
    }

    /// <summary>
    /// Checks that an unusable product can be skipped while other results are returned.
    /// </summary>
    /// <param name="invalid">The invalid product JSON included in the provider response.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("null")]
    [TestCase("7")]
    [TestCase("{}")]
    [TestCase("{\"product_name\":4}")]
    [TestCase("{\"product_name\":\"Water\"}")]
    [TestCase("{\"product_name\":\"Water\",\"nutriments\":[]}")]
    [TestCase("{\"product_name\":\"Water\",\"nutriments\":{}}")]
    [TestCase("{\"product_name\":\"Solid\",\"nutriments\":{\"energy-kcal_100g\":100}}")]
    public async Task Search_SkipsUnusableProductsWithoutFailingWholeSearch(string invalid)
    {
        _http = new(_ => TestHttp.Response("[" + invalid + "," + Product + "]"));
        var result = await Create().SearchAsync("water");
        Assert.That(result.Success, Is.True);
        Assert.That(result.Drinks, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Checks that an invalid nutrient value remains unknown in the returned drink.
    /// </summary>
    /// <param name="number">The numeric representation supplied by this case.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("-1")]
    [TestCase("\"10\"")]
    [TestCase("null")]
    [TestCase("1e999")]
    [TestCase("{}")]
    public async Task Search_InvalidNutrient_IsUnknownRatherThanUsed(string number)
    {
        _http = new(_ => TestHttp.Response("[{\"product_name\":\"Drink\",\"nutriments\":{\"energy-kcal_100ml\":" +
                                           number + ",\"proteins_100ml\":2}}]"));
        var drink = (await Create().SearchAsync("drink")).Drinks.Single();
        Assert.That(drink.Kcal, Is.Null);
        Assert.That(drink.Protein, Is.EqualTo(2));
    }

    /// <summary>
    /// Checks that using nutrition per 100 g to estimate beverage nutrition is labeled approximate.
    /// </summary>
    /// <param name="nutrient">The nutrient field used by the provider fixture.</param>
    /// <param name="property">The corresponding result property to check.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("energy-kcal", "Kcal")]
    [TestCase("proteins", "Protein")]
    [TestCase("carbohydrates", "Carbs")]
    [TestCase("fat", "Fat")]
    public async Task Search_Beverage100gFallback_IsExplicitlyMarkedApproximate(string nutrient, string property)
    {
        _http = new(_ =>
            TestHttp.Response(
                "[{\"product_name\":\"Drink\",\"categories_tags\":[null,7,\"en:beverages\"],\"nutriments\":{\"" +
                nutrient + "_100g\":5}}]"));
        var drink = (await Create().SearchAsync("drink")).Drinks.Single();
        Assert.That(typeof(MomentumGroves.Models.DrinkResult).GetProperty(property)!.GetValue(drink), Is.EqualTo(5d));
        Assert.That(drink.Source, Does.Contain("100 g"));
    }

    /// <summary>
    /// Checks that explicit nutrition per 100 ml takes priority and does not get an approximation label.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Search_Explicit100ml_FallsBackWithoutApproximationLabel_AndPrefersExplicitValues()
    {
        _http = new(_ =>
            TestHttp.Response(
                "[{\"product_name\":\"Drink\",\"nutrition_data_per\":\"100ML\"," +
                    "\"nutriments\":{\"energy-kcal_100ml\":1,\"energy-kcal_100g\":9,\"proteins_100g\":2," +
                    "\"carbohydrates_100g\":3,\"fat_100g\":4}}]"));
        var drink = (await Create().SearchAsync("drink")).Drinks.Single();
        Assert.That(drink.Kcal, Is.EqualTo(1));
        Assert.That(drink.Protein, Is.EqualTo(2));
        Assert.That(drink.Carbs, Is.EqualTo(3));
        Assert.That(drink.Fat, Is.EqualTo(4));
        Assert.That(drink.Source, Is.EqualTo("Open Food Facts"));
    }

    /// <summary>
    /// Checks that Danish drinks rank first, duplicates are removed, and results are capped at twenty.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Search_SortsDanishFirst_DeduplicatesIgnoringCase_AndLimitsTo20()
    {
        var products = Enumerable.Range(0, 25).Select(i => new
        {
            product_name = "Drink " + i,
            countries_tags = new[] { i == 24 ? "en:denmark" : "en:france" },
            nutriments = new Dictionary<string, double> { ["energy-kcal_100ml"] = i }
        }).Cast<object>().ToList();
        products.Insert(0,
            new
            {
                product_name = "DRINK 24", nutriments = new Dictionary<string, double> { ["energy-kcal_100ml"] = 100 }
            });
        _http = new(_ => TestHttp.Response(JsonSerializer.Serialize(products)));
        var result = await Create().SearchAsync("drinks");
        Assert.That(result.Drinks, Has.Count.EqualTo(20));
        Assert.That(result.Drinks[0].Name, Is.EqualTo("Drink 24"));
        Assert.That(result.Drinks[0].Danish, Is.True);
        Assert.That(result.Drinks[0].Source, Does.Contain("Danmark"));
        Assert.That(result.Drinks.Select(d => d.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            Is.EqualTo(20));
    }

    /// <summary>
    /// Checks that an already canceled token propagates even for a blank query.
    /// </summary>
    [Test]
    public void Search_PreCancelledToken_PropagatesEvenForBlankQuery()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.CatchAsync<OperationCanceledException>(() => Create().SearchAsync("", cts.Token));
        Assert.That(_logger.Entries, Is.Empty);
    }

    /// <summary>
    /// Checks that canceling while throttled avoids the provider request and error log.
    /// </summary>
    [Test]
    public void Search_CancelDuringThrottle_DoesNotCallProviderOrLogError()
    {
        LastSearch.SetValue(null, DateTimeOffset.UtcNow);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        Assert.CatchAsync<OperationCanceledException>(() => Create().SearchAsync("drink", cts.Token));
        Assert.That(_http.Requests, Is.Empty);
        Assert.That(_logger.Entries, Is.Empty);
    }

    /// <summary>
    /// Checks that canceling a provider request releases the search gate without error logging.
    /// </summary>
    [Test]
    public void Search_CancelDuringHttp_ReleasesGateAndDoesNotLogError()
    {
        _http = new(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return TestHttp.Response("[]");
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        Assert.CatchAsync<OperationCanceledException>(() => Create().SearchAsync("drink", cts.Token));
        Assert.That(_logger.Entries, Is.Empty);
    }

    /// <summary>
    /// Checks that a provider timeout returns an unavailable result and is logged.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Search_ProviderTimeout_ReturnsUnavailableAndLogs()
    {
        _http = new((_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException("timeout")));
        Assert.That((await Create().SearchAsync("drink")).Success, Is.False);
        Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Error));
    }
}
