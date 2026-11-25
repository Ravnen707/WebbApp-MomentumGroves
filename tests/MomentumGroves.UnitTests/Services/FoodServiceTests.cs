using MomentumGroves.Models;
using Microsoft.Extensions.Logging;
using MomentumGroves.Service;

namespace MomentumGroves.Tests.Services;

[TestFixture]
public class FoodServiceTests : DatabaseServiceTests
{
    private TestLogger<FoodService> _logger = null!;

    /// <summary>
    /// Creates the test Frida database and logger.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _logger = new();
        CreateFrida();
        File.WriteAllText(DataPath("frida-fiber.json"), "{\"Apple\":2.4}");
    }

    /// <summary>
    /// Creates a Frida search service rooted in the temporary workspace.
    /// </summary>
    /// <returns>The isolated food search service.</returns>
    private FoodService Create()
    {
        return new(Environment, _logger);
    }

    /// <summary>
    /// Adds a food row to the temporary Frida database.
    /// </summary>
    /// <param name="danish">The Danish food name to insert.</param>
    /// <param name="english">The English food name to insert, if any.</param>
    /// <param name="nullNutrition">Whether to insert database nulls for nutrient values.</param>
    private void Add(string danish, string? english = null, bool nullNutrition = false)
    {
        Sql("frida.db",
            "INSERT INTO Foods(NameDanish,NameEnglish,Kcal,Protein,Carbs,Fat) VALUES($da,$en,$kcal,$p,$c,$f)",
            ("$da", danish), ("$en", english), ("$kcal", nullNutrition ? null : 52d),
            ("$p", nullNutrition ? null : 0.3), ("$c", nullNutrition ? null : 14d), ("$f", nullNutrition ? null : 0.2));
    }

    /// <summary>
    /// Checks that blank Frida searches succeed with no results.
    /// </summary>
    /// <param name="query">The search text supplied to the service.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("")]
    [TestCase("  ")]
    public async Task Search_BlankQuery_ReturnsSuccessfulEmptyResult(string query)
    {
        var result = await Create().SearchAsync(query);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Results, Is.Empty);
    }

    /// <summary>
    /// Checks that Danish and English names match regardless of case and surrounding whitespace.
    /// </summary>
    /// <param name="query">The search text supplied to the service.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("apple")]
    [TestCase(" APPLE ")]
    [TestCase("fruit")]
    public async Task Search_MatchesDanishOrEnglish_IgnoringCaseAndWhitespace(string query)
    {
        Add("Apple", "Fruit");
        var result = await Create().SearchAsync(query);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Results.Single(), Is.EqualTo(new FoodResult("Apple", "DTU Frida 6.1", 52, 0.3, 14, 0.2,
            2.4)));
    }

    /// <summary>
    /// Checks that unavailable nutrient and fiber values are represented as unknown.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Search_NullNutritionAndMissingFiber_RemainUnknown()
    {
        Add("Water", nullNutrition: true);
        var result = (await Create().SearchAsync("Water")).Results.Single();
        Assert.That(new[] { result.Kcal, result.Protein, result.Carbs, result.Fat, result.Fiber }, Is.All.Null);
    }

    /// <summary>
    /// Checks that ranking prefers exact names, then Danish prefixes, substrings, and English matches.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Search_Ranking_ExactThenDanishPrefixThenSubstringThenEnglish()
    {
        Add("Pineapple", "");
        Add("Apple pie", "");
        Add("Apple", "");
        Add("Æble", "Apple fruit");
        Add("Frugt", "Red apple");
        var result = await Create().SearchAsync("Apple");
        Assert.That(result.Results.Select(f => f.Name),
            Is.EqualTo(new[] { "Apple", "Apple pie", "Pineapple", "Æble", "Frugt" }));
    }

    /// <summary>
    /// Checks that special characters are matched literally in food names.
    /// </summary>
    /// <param name="query">The search text supplied to the service.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("%")]
    [TestCase("_")]
    [TestCase("\\")]
    [TestCase("' OR 1=1 --")]
    public async Task Search_SpecialCharacters_AreLiteral(string query)
    {
        Add("Apple");
        Add(query);
        var result = await Create().SearchAsync(query);
        Assert.That(result.Results.Select(f => f.Name), Is.EqualTo(new[] { query }));
    }

    /// <summary>
    /// Checks that Frida search returns at most twenty matching foods.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Search_LimitsResultsTo20()
    {
        for (var i = 0; i < 25; i++)
        {
            Add("Apple " + i);
        }
        Assert.That((await Create().SearchAsync("Apple")).Results, Has.Count.EqualTo(20));
    }

    /// <summary>
    /// Checks that a search with no matching food succeeds with an empty list.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Search_NoMatches_IsSuccessfulEmptyResult()
    {
        Assert.That((await Create().SearchAsync("missing")).Success, Is.True);
        Assert.That((await Create().SearchAsync("missing")).Results, Is.Empty);
    }

    /// <summary>
    /// Checks that a storage failure is logged and returned as a safe search error.
    /// </summary>
    /// <param name="failure">The failure mode simulated by the test fixture.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("missing")]
    [TestCase("corrupt")]
    [TestCase("schema")]
    public async Task Search_StorageFailure_ReturnsSafeErrorAndLogs(string failure)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (failure == "missing")
        {
            File.Delete(DataPath("frida.db"));
        }
        else if (failure == "corrupt")
        {
            File.WriteAllText(DataPath("frida.db"), "broken");
        }
        else
        {
            Sql("frida.db", "DROP TABLE Foods");
        }
        var result = await Create().SearchAsync("private-query");
        Assert.That(result.Success, Is.False);
        Assert.That(result.Results, Is.Empty);
        Assert.That(result.Error, Does.Not.Contain(Environment.ContentRootPath));
        Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(_logger.Entries.Single().Exception, Is.Not.Null);
    }

    /// <summary>
    /// Checks that caller cancellation propagates without a storage failure log.
    /// </summary>
    [Test]
    public void Search_CallerCancellation_PropagatesWithoutFailureLogging()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.CatchAsync<OperationCanceledException>(() => Create().SearchAsync("Apple", cts.Token));
        Assert.That(_logger.Entries, Is.Empty);
    }

    /// <summary>
    /// Checks that food search still returns results with unknown fiber when the supplement cannot be loaded.
    /// </summary>
    /// <param name="failure">The failure mode simulated by the test fixture.</param>
    /// <returns>A task that completes after the assertions finish.</returns>
    [TestCase("missing")]
    [TestCase("invalid")]
    [TestCase("null")]
    [TestCase("locked")]
    public async Task Constructor_UnavailableFiber_SearchStillWorksWithUnknownFiber(string failure)
    {
        Add("Apple");
        if (failure == "missing")
        {
            File.Delete(DataPath("frida-fiber.json"));
        }
        else if (failure == "invalid")
        {
            File.WriteAllText(DataPath("frida-fiber.json"), "broken");
        }
        else if (failure == "null")
        {
            File.WriteAllText(DataPath("frida-fiber.json"), "null");
        }
        using var locked = failure == "locked"
            ? File.Open(DataPath("frida-fiber.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)
            : null;
        var result = await Create().SearchAsync("Apple");
        Assert.That(result.Success, Is.True);
        Assert.That(result.Results.Single().Fiber, Is.Null);
        if (failure != "null")
        {
            Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Warning));
        }
    }
}
