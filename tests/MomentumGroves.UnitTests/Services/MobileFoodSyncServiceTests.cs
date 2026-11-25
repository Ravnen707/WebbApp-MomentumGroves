using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MomentumGroves.Models;
using MomentumGroves.Repositories;
using MomentumGroves.Service;

namespace MomentumGroves.Tests.Services;

[TestFixture]
public class MobileFoodSyncServiceTests : DatabaseServiceTests
{
    private const string Key = "private-sync-key-123456";
    private TestLogger<MobileFoodSyncService> _logger = null!;
    private MobileFoodSyncService _service = null!;
    private FoodLibraryService _library = null!;

    /// <summary>
    /// Creates the import service with a known key and temporary database.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _logger = new();
        _library = new(Environment, new TestLogger<FoodLibraryService>());
        _service = Create(Key);
    }

    /// <summary>
    /// Creates a food synchronization service with an isolated repository and the supplied key.
    /// </summary>
    /// <param name="key">The synchronization key or application data key exercised by the case.</param>
    /// <returns>The configured food synchronization service.</returns>
    private MobileFoodSyncService Create(string? key)
    {
        return new(TestHttp.Configuration(("MobileSync:Key", key)), new MobileFoodSyncRepository(Environment), _logger);
    }

    /// <summary>
    /// Builds a valid food payload for batch validation tests.
    /// </summary>
    /// <returns>A food import item with representative nutrition values.</returns>
    private static FoodSyncDto Food()
    {
        return new()
        { Id = Guid.NewGuid(), Name = "Apple", Unit = "g", Kcal = 50, Protein = 1, Carbs = 12, Fat = 0.2, Fiber = 3 };
    }

    /// <summary>
    /// Checks that the exact configured key grants access without logging.
    /// </summary>
    [Test]
    public void IsAuthorized_ExactKey_AcceptedWithoutLogging()
    {
        Assert.That(_service.IsAuthorized(Key), Is.True);
    }

    /// <summary>
    /// Checks that a missing or short configured key denies access.
    /// </summary>
    /// <param name="key">The synchronization key or application data key exercised by the case.</param>
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("short")]
    [TestCase("123456789012345")]
    public void IsAuthorized_MissingOrShortConfiguredKey_Denies(string key)
    {
        Assert.That(Create(key).IsAuthorized(Key), Is.False);
        Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Warning));
    }

    /// <summary>
    /// Checks that an invalid supplied key is rejected without exposing either key in logs.
    /// </summary>
    /// <param name="key">The synchronization key or application data key exercised by the case.</param>
    [TestCase("")]
    [TestCase("wrong")]
    [TestCase("PRIVATE-SYNC-KEY-123456")]
    [TestCase("private-sync-key-123457")]
    [TestCase(" private-sync-key-123456")]
    public void IsAuthorized_InvalidSuppliedKey_DeniesWithoutLoggingSecrets(string key)
    {
        Assert.That(_service.IsAuthorized(key), Is.False);
        Assert.That(_logger.Entries.Single().Message, Does.Not.Contain(Key));
        if (!string.IsNullOrWhiteSpace(key))
        {
            Assert.That(_logger.Entries.Single().Message, Does.Not.Contain(key));
        }
    }

    /// <summary>
    /// Checks that valid boundary-length and Unicode synchronization keys are accepted.
    /// </summary>
    /// <param name="key">The synchronization key or application data key exercised by the case.</param>
    [TestCase("1234567890123456")]
    [TestCase("æøå-秘密-1234567890123456")]
    public void IsAuthorized_BoundaryAndUnicodeKeys_Work(string key)
    {
        Assert.That(Create(key).IsAuthorized(key), Is.True);
    }

    /// <summary>
    /// Checks that mobile food nutrition accepts numbers and supported decimal strings.
    /// </summary>
    /// <param name="number">The numeric representation supplied by this case.</param>
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

    /// <summary>
    /// Checks that unsupported JSON shapes and numeric values fail deserialization.
    /// </summary>
    /// <param name="json">The JSON submitted for deserialization or storage.</param>
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

    /// <summary>
    /// Checks that a JSON null batch deserializes and then fails validation.
    /// </summary>
    [Test]
    public void TryDeserializeBatch_NullJson_IsDeserializedButFailsValidation()
    {
        Assert.That(_service.TryDeserializeBatch(Json("null"), out var foods), Is.True);
        Assert.That(foods, Is.Null);
        Assert.That(_service.IsValidBatch(foods), Is.False);
    }

    /// <summary>
    /// Checks that case-insensitive properties and a null fiber value deserialize successfully.
    /// </summary>
    [Test]
    public void TryDeserializeBatch_CaseInsensitivePropertiesAndNullableFiber()
    {
        Assert.That(_service.TryDeserializeBatch(Json("[{\"NAME\":\"Apple\",\"fiber\":null}]"), out var foods),
            Is.True);
        Assert.That(foods!.Single().Name, Is.EqualTo("Apple"));
        Assert.That(foods!.Single().Fiber, Is.Null);
    }

    /// <summary>
    /// Checks the minimum and maximum supported number of items in an import batch.
    /// </summary>
    /// <param name="count">The number of foods submitted for validation.</param>
    /// <param name="expected">The expected result for this input.</param>
    [TestCase(0, true)]
    [TestCase(1, true)]
    [TestCase(500, true)]
    [TestCase(501, false)]
    public void IsValidBatch_CountBoundaries(int count, bool expected)
    {
        Assert.That(_service.IsValidBatch(Enumerable.Range(0, count).Select(_ => Food()).ToList()),
            Is.EqualTo(expected));
    }

    /// <summary>
    /// Checks that validation rejects a null food batch.
    /// </summary>
    [Test]
    public void IsValidBatch_NullBatch_Rejects()
    {
        Assert.That(_service.IsValidBatch(null), Is.False);
    }

    /// <summary>
    /// Checks that validation rejects a batch containing a null food.
    /// </summary>
    [Test]
    public void IsValidBatch_NullItem_Rejects()
    {
        Assert.That(_service.IsValidBatch(new() { null! }), Is.False);
    }

    /// <summary>
    /// Checks that validation rejects a food without a mobile ID.
    /// </summary>
    [Test]
    public void IsValidBatch_EmptyId_Rejects()
    {
        var food = Food();
        food.Id = Guid.Empty;
        Assert.That(_service.IsValidBatch(new() { food }), Is.False);
    }

    /// <summary>
    /// Checks whether a mobile food name is accepted or rejected for this case.
    /// </summary>
    /// <param name="name">The name supplied by the case.</param>
    /// <param name="expected">The expected result for this input.</param>
    [TestCase("", false)]
    [TestCase("  ", false)]
    [TestCase("Apple", true)]
    public void IsValidBatch_NameValidation(string name, bool expected)
    {
        var food = Food();
        food.Name = name;
        Assert.That(_service.IsValidBatch(new() { food }), Is.EqualTo(expected));
    }

    /// <summary>
    /// Checks that name length is validated after trimming surrounding whitespace.
    /// </summary>
    /// <param name="length">The number of characters used in the boundary case.</param>
    /// <param name="expected">The expected result for this input.</param>
    [TestCase(200, true)]
    [TestCase(201, false)]
    public void IsValidBatch_TrimmedNameLengthBoundary(int length, bool expected)
    {
        var food = Food();
        food.Name = " " + new string('n', length) + " ";
        Assert.That(_service.IsValidBatch(new() { food }), Is.EqualTo(expected));
    }

    /// <summary>
    /// Checks the supported and unsupported mobile food units.
    /// </summary>
    /// <param name="unit">The food unit supplied by this case.</param>
    /// <param name="expected">The expected result for this input.</param>
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

    /// <summary>
    /// Checks nutrition validation against finite, nonnegative values.
    /// </summary>
    /// <param name="field">The field set by this case.</param>
    /// <param name="value">The test value supplied for the field.</param>
    /// <param name="expected">The expected result for this input.</param>
    [TestCaseSource(nameof(NutritionCases))]
    public void IsValidBatch_NutritionBoundaries(string field, double value, bool expected)
    {
        var food = Food();
        typeof(FoodSyncDto).GetProperty(field)!.SetValue(food, value);
        Assert.That(_service.IsValidBatch(new() { food }), Is.EqualTo(expected));
    }

    /// <summary>
    /// Enumerates boundary values for each nutrition field in an import batch.
    /// </summary>
    /// <returns>The parameterized nutrition validation cases.</returns>
    private static IEnumerable<TestCaseData> NutritionCases()
    {
        foreach (var field in new[] { "Kcal", "Protein", "Carbs", "Fat", "Fiber" })
        {
            foreach (var value in new[] { 0d, -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                yield return new TestCaseData(field, value, value == 0);
            }
        }
    }

    /// <summary>
    /// Checks that unknown fiber is accepted as null.
    /// </summary>
    [Test]
    public void IsValidBatch_NullFiber_Accepted()
    {
        var food = Food();
        food.Fiber = null;
        Assert.That(_service.IsValidBatch(new() { food }), Is.True);
    }

    /// <summary>
    /// Checks that importing a new food preserves its values and acknowledges its mobile ID.
    /// </summary>
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

    /// <summary>
    /// Checks that retrying an imported mobile ID does not create another food.
    /// </summary>
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

    /// <summary>
    /// Checks that a duplicate name is skipped and its mobile ID is acknowledged.
    /// </summary>
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

    /// <summary>
    /// Checks that an empty import returns zero counters and no acknowledgements.
    /// </summary>
    [Test]
    public void Import_EmptyBatch_ReturnsZeroCounts()
    {
        var result = _service.Import(new());
        Assert.That(result.Imported + result.Skipped + result.Received, Is.Zero);
        Assert.That(result.AcknowledgedIds, Is.Empty);
    }

    /// <summary>
    /// Checks that an import storage failure logs the original exception.
    /// </summary>
    [Test]
    public void Import_StorageFailure_LogsOriginalException()
    {
        Sql("food_and_recipes.db", "DROP TABLE CustomFoods");
        var exception = Assert.Throws<SqliteException>(() => _service.Import(new() { Food() }));
        _logger.AssertError(exception!, "Import");
    }

    /// <summary>
    /// Checks that a failure on a later import item rolls back an earlier insert.
    /// </summary>
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
