using Microsoft.Extensions.Logging;
using MomentumGroves.Service;

namespace MomentumGroves.Tests.Services;

[TestFixture]
public class FoodServiceTests : DatabaseServiceTests
{
    private TestLogger<FoodService> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _logger = new();
        CreateFrida();
        File.WriteAllText(DataPath("frida-fiber.json"), "{\"Apple\":2.4}");
    }

    private FoodService Create() => new(Environment, _logger);

    private void Add(string danish, string? english = null, bool nullNutrition = false) =>
        Sql("frida.db",
            "INSERT INTO Foods(NameDanish,NameEnglish,Kcal,Protein,Carbs,Fat) VALUES($da,$en,$kcal,$p,$c,$f)",
            ("$da", danish), ("$en", english), ("$kcal", nullNutrition ? null : 52d),
            ("$p", nullNutrition ? null : 0.3), ("$c", nullNutrition ? null : 14d), ("$f", nullNutrition ? null : 0.2));

    [TestCase("")]
    [TestCase("  ")]
    public async Task Search_BlankQuery_ReturnsSuccessfulEmptyResult(string query)
    {
        var result = await Create().SearchAsync(query);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Results, Is.Empty);
    }

    [TestCase("apple")]
    [TestCase(" APPLE ")]
    [TestCase("fruit")]
    public async Task Search_MatchesDanishOrEnglish_IgnoringCaseAndWhitespace(string query)
    {
        Add("Apple", "Fruit");
        var result = await Create().SearchAsync(query);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Results.Single(),
            Is.EqualTo(new FoodResult("Apple", "DTU Frida 6.1", 52, 0.3, 14, 0.2, 2.4)));
    }

    [Test]
    public async Task Search_NullNutritionAndMissingFiber_RemainUnknown()
    {
        Add("Water", nullNutrition: true);
        var result = (await Create().SearchAsync("Water")).Results.Single();
        Assert.That(new[] { result.Kcal, result.Protein, result.Carbs, result.Fat, result.Fiber }, Is.All.Null);
    }

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

    [Test]
    public async Task Search_LimitsResultsTo20()
    {
        for (var i = 0; i < 25; i++) Add("Apple " + i);
        Assert.That((await Create().SearchAsync("Apple")).Results, Has.Count.EqualTo(20));
    }

    [Test]
    public async Task Search_NoMatches_IsSuccessfulEmptyResult()
    {
        Assert.That((await Create().SearchAsync("missing")).Success, Is.True);
        Assert.That((await Create().SearchAsync("missing")).Results, Is.Empty);
    }

    [TestCase("missing")]
    [TestCase("corrupt")]
    [TestCase("schema")]
    public async Task Search_StorageFailure_ReturnsSafeErrorAndLogs(string failure)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (failure == "missing") File.Delete(DataPath("frida.db"));
        else if (failure == "corrupt") File.WriteAllText(DataPath("frida.db"), "broken");
        else Sql("frida.db", "DROP TABLE Foods");
        var result = await Create().SearchAsync("private-query");
        Assert.That(result.Success, Is.False);
        Assert.That(result.Results, Is.Empty);
        Assert.That(result.Error, Does.Not.Contain(Environment.ContentRootPath));
        Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(_logger.Entries.Single().Exception, Is.Not.Null);
    }

    [Test]
    public void Search_CallerCancellation_PropagatesWithoutFailureLogging()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.CatchAsync<OperationCanceledException>(() => Create().SearchAsync("Apple", cts.Token));
        Assert.That(_logger.Entries, Is.Empty);
    }

    [TestCase("missing")]
    [TestCase("invalid")]
    [TestCase("null")]
    [TestCase("locked")]
    public async Task Constructor_UnavailableFiber_SearchStillWorksWithUnknownFiber(string failure)
    {
        Add("Apple");
        if (failure == "missing") File.Delete(DataPath("frida-fiber.json"));
        else if (failure == "invalid") File.WriteAllText(DataPath("frida-fiber.json"), "broken");
        else if (failure == "null") File.WriteAllText(DataPath("frida-fiber.json"), "null");
        using var locked = failure == "locked"
            ? File.Open(DataPath("frida-fiber.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)
            : null;
        var result = await Create().SearchAsync("Apple");
        Assert.That(result.Success, Is.True);
        Assert.That(result.Results.Single().Fiber, Is.Null);
        if (failure != "null") Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Warning));
    }
}