using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MomentumGroves.Repository;
using MomentumGroves.Service;
using MomentumGroves.DTO;

namespace MomentumGroves.Tests.Services;

[TestFixture]
public class MobileFoodSyncServiceTests : DatabaseServiceTests
{
    private const string Key = "private-sync-key-123456";
    private TestLogger<MobileFoodSyncService> _logger = null!;
    private MobileFoodSyncService _service = null!;
    private FoodLibraryService _library = null!;

    [SetUp]
    public void SetUp()
    {
        _logger = new();
        _library = new(Environment, new TestLogger<FoodLibraryService>());
        _service = Create(Key);
    }

    private MobileFoodSyncService Create(string? key) => new(
        TestHttp.Configuration(("MobileSync:Key", key)), new MobileFoodSyncRepository(Environment), _logger);

    private static FoodSyncDto Food() => new()
        { Id = Guid.NewGuid(), Name = "Apple", Unit = "g", Kcal = 50, Protein = 1, Carbs = 12, Fat = 0.2, Fiber = 3 };

    [Test]
    public void IsAuthorized_ExactKey_AcceptedWithoutLogging() => Assert.That(_service.IsAuthorized(Key), Is.True);

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("short")]
    [TestCase("123456789012345")]
    public void IsAuthorized_MissingOrShortConfiguredKey_Denies(string key)
    {
        Assert.That(Create(key).IsAuthorized(Key), Is.False);
        Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Warning));
    }

    [TestCase("")]
    [TestCase("wrong")]
    [TestCase("PRIVATE-SYNC-KEY-123456")]
    [TestCase("private-sync-key-123457")]
    [TestCase(" private-sync-key-123456")]
    public void IsAuthorized_InvalidSuppliedKey_DeniesWithoutLoggingSecrets(string key)
    {
        Assert.That(_service.IsAuthorized(key), Is.False);
        Assert.That(_logger.Entries.Single().Message, Does.Not.Contain(Key));
        if (!string.IsNullOrWhiteSpace(key)) Assert.That(_logger.Entries.Single().Message, Does.Not.Contain(key));
    }

    [TestCase("1234567890123456")]
    [TestCase("æøå-秘密-1234567890123456")]
    public void IsAuthorized_BoundaryAndUnicodeKeys_Work(string key) =>
        Assert.That(Create(key).IsAuthorized(key), Is.True);

    [TestCase("12.5")]
    [TestCase("\"12.5\"")]
    [TestCase("\"12,5\"")]
    [TestCase("\" +12,5 \"")]
    public void TryDeserializeBatch_NumericAndDecimalStrings_Accepted(string number)
    {
        var payload = Json("[{\"id\":\"" + Guid.NewGuid() + "\",\"name\":\"Apple\",\"kcal\":" + number + "}]");
        Assert.That(_service.TryDeserializeBatch(payload, out var foods), Is.True);
        Assert.That(foods!.Single().Kcal, Is.EqualTo(12.5));
    }

    [TestCase("{}")]
    [TestCase("42")]
    [TestCase("\"food\"")]
    [TestCase("[{\"id\":\"not-a-guid\"}]")]
    [TestCase("[{\"kcal\":\"1.234,5\"}]")]
    [TestCase("[{\"kcal\":\"NaN\"}]")]
    [TestCase("[{\"kcal\":\"\"}]")]
    [TestCase("[{\"kcal\":true}]")]
    [TestCase("[{\"kcal\":null}]")]
    [TestCase("[{\"kcal\":\"1e3\"}]")]
    public void TryDeserializeBatch_InvalidShapeOrNumbers_ReturnsFalse(string json)
    {
        Assert.That(_service.TryDeserializeBatch(Json(json), out var foods), Is.False);
        Assert.That(foods, Is.Null);
        Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Warning));
    }

    [Test]
    public void TryDeserializeBatch_NullJson_IsDeserializedButFailsValidation()
    {
        Assert.That(_service.TryDeserializeBatch(Json("null"), out var foods), Is.True);
        Assert.That(foods, Is.Null);
        Assert.That(_service.IsValidBatch(foods), Is.False);
    }

    [Test]
    public void TryDeserializeBatch_CaseInsensitivePropertiesAndNullableFiber()
    {
        Assert.That(_service.TryDeserializeBatch(Json("[{\"NAME\":\"Apple\",\"fiber\":null}]"), out var foods),
            Is.True);
        Assert.That(foods!.Single().Name, Is.EqualTo("Apple"));
        Assert.That(foods!.Single().Fiber, Is.Null);
    }

    [TestCase(0, true)]
    [TestCase(1, true)]
    [TestCase(500, true)]
    [TestCase(501, false)]
    public void IsValidBatch_CountBoundaries(int count, bool expected) =>
        Assert.That(_service.IsValidBatch(Enumerable.Range(0, count).Select(_ => Food()).ToList()),
            Is.EqualTo(expected));

    [Test]
    public void IsValidBatch_NullBatch_Rejects() => Assert.That(_service.IsValidBatch(null), Is.False);

    [Test]
    public void IsValidBatch_NullItem_Rejects() => Assert.That(_service.IsValidBatch(new() { null! }), Is.False);

    [Test]
    public void IsValidBatch_EmptyId_Rejects()
    {
        var food = Food();
        food.Id = Guid.Empty;
        Assert.That(_service.IsValidBatch(new() { food }), Is.False);
    }

    [TestCase("", false)]
    [TestCase("  ", false)]
    [TestCase("Apple", true)]
    public void IsValidBatch_NameValidation(string name, bool expected)
    {
        var food = Food();
        food.Name = name;
        Assert.That(_service.IsValidBatch(new() { food }), Is.EqualTo(expected));
    }

    [TestCase(200, true)]
    [TestCase(201, false)]
    public void IsValidBatch_TrimmedNameLengthBoundary(int length, bool expected)
    {
        var food = Food();
        food.Name = " " + new string('n', length) + " ";
        Assert.That(_service.IsValidBatch(new() { food }), Is.EqualTo(expected));
    }

    [TestCase("g", true)]
    [TestCase("ml", true)]
    [TestCase("G", false)]
    [TestCase("kg", false)]
    public void IsValidBatch_UnitValidation(string unit, bool expected)
    {
        var food = Food();
        food.Unit = unit;
        Assert.That(_service.IsValidBatch(new() { food }), Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(NutritionCases))]
    public void IsValidBatch_NutritionBoundaries(string field, double value, bool expected)
    {
        var food = Food();
        typeof(FoodSyncDto).GetProperty(field)!.SetValue(food, value);
        Assert.That(_service.IsValidBatch(new() { food }), Is.EqualTo(expected));
    }

    private static IEnumerable<TestCaseData> NutritionCases()
    {
        foreach (var field in new[] { "Kcal", "Protein", "Carbs", "Fat", "Fiber" })
        foreach (var value in new[] { 0d, -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            yield return new TestCaseData(field, value, value == 0);
    }

    [Test]
    public void IsValidBatch_NullFiber_Accepted()
    {
        var food = Food();
        food.Fiber = null;
        Assert.That(_service.IsValidBatch(new() { food }), Is.True);
    }

    [Test]
    public void Import_NewFood_RoundTripsAndAcknowledgesId()
    {
        var food = Food();
        food.Name = " Apple ";
        var result = _service.Import(new() { food });
        Assert.That(result.Imported, Is.EqualTo(1));
        Assert.That(result.Skipped, Is.Zero);
        Assert.That(result.Received, Is.EqualTo(1));
        Assert.That(result.AcknowledgedIds, Is.EqualTo(new[] { food.Id }));
        var saved = _library.Foods().Single();
        Assert.That(saved.Name, Is.EqualTo("Apple"));
        Assert.That(saved.Kcal, Is.EqualTo(food.Kcal));
        Assert.That(saved.Fiber, Is.EqualTo(food.Fiber));
    }

    [Test]
    public void Import_RepeatedId_IsIdempotent()
    {
        var food = Food();
        _service.Import(new() { food });
        food.Name = "Renamed";
        var second = _service.Import(new() { food });
        Assert.That(second.Imported, Is.Zero);
        Assert.That(second.Skipped, Is.EqualTo(1));
        Assert.That(second.AcknowledgedIds, Does.Contain(food.Id));
        Assert.That(_library.Foods(), Has.Count.EqualTo(1));
    }

    [Test]
    public void Import_DuplicateName_IsSkippedAndAcknowledged()
    {
        _service.Import(new() { Food() });
        var duplicate = Food();
        duplicate.Name = " apple ";
        var result = _service.Import(new() { duplicate });
        Assert.That(result.Imported, Is.Zero);
        Assert.That(result.Skipped, Is.EqualTo(1));
        Assert.That(result.AcknowledgedIds, Does.Contain(duplicate.Id));
    }

    [Test]
    public void Import_EmptyBatch_ReturnsZeroCounts()
    {
        var result = _service.Import(new());
        Assert.That(result.Imported + result.Skipped + result.Received, Is.Zero);
        Assert.That(result.AcknowledgedIds, Is.Empty);
    }

    [Test]
    public void Import_StorageFailure_LogsOriginalException()
    {
        Sql("food_and_recipes.db", "DROP TABLE CustomFoods");
        var exception = Assert.Throws<SqliteException>(() => _service.Import(new() { Food() }));
        _logger.AssertError(exception!, "Import");
    }

    [Test]
    public void Import_FailureOnLaterItem_RollsBackEarlierItem()
    {
        var invalid = Food();
        invalid.Name = "Invalid";
        invalid.Unit = "kg";
        Assert.Throws<SqliteException>(() => _service.Import(new() { Food(), invalid }));
        Assert.That(_library.Foods(), Is.Empty);
    }
}