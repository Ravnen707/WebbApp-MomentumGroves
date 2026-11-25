using System.Collections;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MomentumGroves.Models;
using MomentumGroves.Service;

namespace MomentumGroves.Tests.Services;

[TestFixture]
public class FoodLibraryServiceTests : DatabaseServiceTests
{
    private FoodLibraryService _service = null!;
    private TestLogger<FoodLibraryService> _logger = null!;
    /// <summary>
    /// Builds a valid library food for save tests.
    /// </summary>
    /// <param name="name">The name supplied by the case.</param>
    /// <param name="unit">The food unit supplied by this case.</param>
    /// <returns>The food input with sample nutrition values.</returns>
    private static SaveLibraryFood Food(string name = "Apple", string unit = "g")
    {
        return new(name, unit, 100, 2, 20, 3, 4);
    }
    /// <summary>
    /// Builds a recipe that contains one food.
    /// </summary>
    /// <param name="foodId">The ID of the food used in the recipe.</param>
    /// <returns>The recipe input for the supplied food ID.</returns>
    private static SaveRecipe Recipe(long foodId)
    {
        return new("Recipe", 200, new() { new(foodId, 50) });
    }

    /// <summary>
    /// Creates the library service against the temporary database.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _logger = new();
        _service = new(Environment, _logger);
    }

    /// <summary>
    /// Checks that repeated service initialization leaves an empty library ready for use.
    /// </summary>
    [Test]
    public void Constructor_IsIdempotent_AndCreatesEmptyLibrary()
    {
        _ = new FoodLibraryService(Environment, _logger);
        Assert.That(_service.Foods(), Is.Empty);
        Assert.That(_service.Recipes(), Is.Empty);
    }

    /// <summary>
    /// Checks that a new food name is trimmed and its nutrition values are persisted.
    /// </summary>
    /// <param name="unit">The food unit supplied by this case.</param>
    [TestCase("g")]
    [TestCase("ml")]
    public void SaveFood_Insert_TrimsNameAndRoundTripsNutrition(string unit)
    {
        var id = _service.SaveFood(null, Food(" Apple ", unit));
        Assert.That(id, Is.GreaterThan(0));
        Assert.That(_service.Foods().Single(), Is.EqualTo(new LibraryFood(id, "Apple", unit, 100, 2, 20, 3, 4)));
    }

    /// <summary>
    /// Checks that updating a food retains its ID and replaces its values.
    /// </summary>
    [Test]
    public void SaveFood_Update_PreservesIdAndReplacesValues()
    {
        var id = _service.SaveFood(null, Food());
        Assert.That(_service.SaveFood(id, Food("Pear") with { Fiber = null }), Is.EqualTo(id));
        Assert.That(_service.Foods().Single().Name, Is.EqualTo("Pear"));
        Assert.That(_service.Foods().Single().Fiber, Is.Null);
    }

    /// <summary>
    /// Checks that updating an absent food throws without an error log.
    /// </summary>
    [Test]
    public void SaveFood_UnknownId_ThrowsWithoutErrorLog()
    {
        Assert.Throws<KeyNotFoundException>(() => _service.SaveFood(999, Food()));
        Assert.That(_logger.Entries, Is.Empty);
    }

    /// <summary>
    /// Checks that a food with a missing or blank name is rejected.
    /// </summary>
    /// <param name="name">The name supplied by the case.</param>
    [TestCase("")]
    [TestCase(" ")]
    public void SaveFood_MissingName_Rejects(string name)
    {
        Assert.Throws<ArgumentException>(() => _service.SaveFood(null, Food(name)));
    }

    /// <summary>
    /// Checks that the length limit accepts and rejects the intended names.
    /// </summary>
    /// <param name="length">The number of characters used in the boundary case.</param>
    /// <param name="accepted">Whether the service should accept the input.</param>
    [TestCase(200, true)]
    [TestCase(201, false)]
    public void SaveFood_NameLengthBoundary(int length, bool accepted)
    {
        if (accepted)
        {
            Assert.That(_service.SaveFood(null, Food(new string('a', length))), Is.Positive);
        }
        else
        {
            Assert.Throws<ArgumentException>(() => _service.SaveFood(null, Food(new string('a', length))));
        }
    }

    /// <summary>
    /// Checks that a food with an unsupported unit is rejected.
    /// </summary>
    /// <param name="unit">The food unit supplied by this case.</param>
    [TestCase("kg")]
    [TestCase("")]
    [TestCase("G")]
    public void SaveFood_InvalidUnit_Rejects(string unit)
    {
        Assert.Throws<ArgumentException>(() => _service.SaveFood(null, Food(unit: unit)));
    }

    /// <summary>
    /// Checks that invalid nutrient values are rejected.
    /// </summary>
    /// <param name="field">The field set by this case.</param>
    /// <param name="value">The test value supplied for the field.</param>
    [TestCaseSource(nameof(InvalidNutrition))]
    public void SaveFood_InvalidNutrition_Rejects(string field, double value)
    {
        var food = Food();
        food = field switch
        {
            "Kcal" => food with { Kcal = value }, "Protein" => food with { Protein = value },
            "Carbs" => food with { Carbs = value }, "Fat" => food with { Fat = value }, _ => food with { Fiber = value }
        };
        Assert.Throws<ArgumentException>(() => _service.SaveFood(null, food));
        Assert.That(_service.Foods(), Is.Empty);
    }

    /// <summary>
    /// Enumerates invalid values for each food nutrition field.
    /// </summary>
    /// <returns>The parameterized nutrition cases.</returns>
    private static IEnumerable<TestCaseData> InvalidNutrition()
    {
        foreach (var field in new[] { "Kcal", "Protein", "Carbs", "Fat", "Fiber" })
        {
            foreach (var value in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                yield return new TestCaseData(field, value);
            }
        }
    }

    /// <summary>
    /// Checks that zero nutrition and unknown fiber can be saved.
    /// </summary>
    [Test]
    public void SaveFood_ZeroNutritionAndNullFiber_Accepted()
    {
        _service.SaveFood(null, new("Water", "ml", 0, 0, 0, 0, null));
        Assert.That(_service.Foods().Single().Kcal, Is.Zero);
    }

    /// <summary>
    /// Checks that saved food searches trim input and match literal text without regard to case.
    /// </summary>
    /// <param name="query">The search text supplied to the service.</param>
    /// <param name="count">The expected result count or the input batch size being exercised.</param>
    [TestCase("", 3)]
    [TestCase(" APP ", 2)]
    [TestCase("pear", 1)]
    [TestCase("missing", 0)]
    [TestCase("' OR 1=1 --", 0)]
    [TestCase("%", 0)]
    public void Foods_Search_IsLiteralCaseInsensitiveAndTrimmed(string query, int count)
    {
        _service.SaveFood(null, Food("Pineapple"));
        _service.SaveFood(null, Food("Apple"));
        _service.SaveFood(null, Food("Pear"));
        var results = _service.Foods(query);
        Assert.That(results, Has.Count.EqualTo(count));
        Assert.That(results.Select(f => f.Name), Is.Ordered.Using((IComparer)StringComparer.Ordinal));
    }

    /// <summary>
    /// Checks that unpaged food search caps its results at 250.
    /// </summary>
    [Test]
    public void Foods_LimitsResultsTo250()
    {
        Sql("food_and_recipes.db",
            "WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<251) INSERT INTO CustomFoods(Name,Unit,Kcal,Protein,Carbs,Fat) SELECT printf('Food%03d',x),'g',0,0,0,0 FROM n");
        Assert.That(_service.Foods(), Has.Count.EqualTo(250));
    }

    /// <summary>
    /// Checks that pagination clamps invalid bounds and reports the expected item count and continuation.
    /// </summary>
    /// <param name="page">The page requested from the food library.</param>
    /// <param name="size">The requested page size.</param>
    /// <param name="count">The expected number of returned food items.</param>
    /// <param name="hasMore">Whether the response should indicate another page.</param>
    [TestCase(1, 2, 2, true)]
    [TestCase(2, 2, 1, false)]
    [TestCase(3, 2, 0, false)]
    [TestCase(0, 0, 1, true)]
    [TestCase(-1, -5, 1, true)]
    [TestCase(1, 100, 3, false)]
    [TestCase(int.MaxValue, 50, 0, false)]
    public void PagedFoods_ClampsBoundsAndReportsTotals(int page, int size, int count, bool hasMore)
    {
        foreach (var name in new[] { "One", "Two", "Three" })
        {
            _service.SaveFood(null, Food(name));
        }
        var result = JsonSerializer.SerializeToElement(_service.PagedFoods(null, page, size));
        Assert.That(result.GetProperty("items").GetArrayLength(), Is.EqualTo(count));
        Assert.That(result.GetProperty("total").GetInt32(), Is.EqualTo(3));
        Assert.That(result.GetProperty("page").GetInt32(), Is.EqualTo(Math.Max(1, page)));
        Assert.That(result.GetProperty("pageSize").GetInt32(), Is.EqualTo(Math.Clamp(size, 1, 50)));
        Assert.That(result.GetProperty("hasMore").GetBoolean(), Is.EqualTo(hasMore));
    }

    /// <summary>
    /// Checks that pagination searches the entire library and returns the newest match first.
    /// </summary>
    [Test]
    public void PagedFoods_FiltersEntireLibrary_AndReturnsNewestFirst()
    {
        _service.SaveFood(null, Food("Apple old"));
        _service.SaveFood(null, Food("Pear"));
        var newest = _service.SaveFood(null, Food("Apple new"));
        var result = JsonSerializer.SerializeToElement(_service.PagedFoods(" APPLE ", 1, 1));
        Assert.That(result.GetProperty("total").GetInt32(), Is.EqualTo(2));
        Assert.That(result.GetProperty("items")[0].GetProperty("Id").GetInt64(), Is.EqualTo(newest));
    }

    /// <summary>
    /// Checks that a food can be deleted with or without the mobile import tracking table.
    /// </summary>
    /// <param name="tracking">Whether the test database has a mobile import tracking table.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void DeleteFood_RemovesFood_WithOrWithoutMobileImportTable(bool tracking)
    {
        var id = _service.SaveFood(null, Food());
        if (tracking)
        {
            Sql("food_and_recipes.db",
                "CREATE TABLE MobileFoodImports (MobileId TEXT, FoodId INTEGER); INSERT INTO MobileFoodImports VALUES ('id',$id)",
                ("$id", id));
        }
        _service.DeleteFood(id);
        _service.DeleteFood(id); // Idempotent missing delete.
        Assert.That(_service.Foods(), Is.Empty);
        if (tracking)
        {
            // An orphan tracking row would make this insert fail.
            Sql("food_and_recipes.db",
                "CREATE UNIQUE INDEX OneMapping ON MobileFoodImports(MobileId); INSERT INTO MobileFoodImports VALUES ('id',999)");
        }
    }

    /// <summary>
    /// Checks that deletion preserves a food and recipe when they are linked.
    /// </summary>
    [Test]
    public void DeleteFood_UsedByRecipe_PreservesBothRecords()
    {
        var food = _service.SaveFood(null, Food());
        _service.SaveRecipe(null, Recipe(food));
        Assert.Throws<InvalidOperationException>(() => _service.DeleteFood(food));
        Assert.That(_service.Foods(), Has.Count.EqualTo(1));
        Assert.That(_service.Recipes().Single().Ingredients, Has.Count.EqualTo(1));
        Assert.That(_logger.Entries, Is.Empty);
    }

    /// <summary>
    /// Checks that recipe nutrient totals account for each ingredient amount.
    /// </summary>
    [Test]
    public void Recipes_CalculatesScaledTotalsAcrossIngredients()
    {
        var apple = _service.SaveFood(null, Food());
        var pear = _service.SaveFood(null, Food("Pear") with { Fiber = 2 });
        _service.SaveRecipe(null, new(" Mix ", 300, new() { new(apple, 50), new(pear, 200) }));
        var recipe = _service.Recipes().Single();
        Assert.That(recipe.Name, Is.EqualTo("Mix"));
        Assert.That(recipe.FinishedWeight, Is.EqualTo(300));
        Assert.That(recipe.Ingredients, Has.Count.EqualTo(2));
        Assert.That(recipe.Kcal, Is.EqualTo(250));
        Assert.That(recipe.Protein, Is.EqualTo(5));
        Assert.That(recipe.Carbs, Is.EqualTo(50));
        Assert.That(recipe.Fat, Is.EqualTo(7.5));
        Assert.That(recipe.Fiber, Is.EqualTo(6));
    }

    /// <summary>
    /// Checks that fiber remains unknown when no ingredients provide a value.
    /// </summary>
    [Test]
    public void Recipes_UnknownFiber_IsNullInsteadOfZero()
    {
        var id = _service.SaveFood(null, Food() with { Fiber = null });
        _service.SaveRecipe(null, Recipe(id) with { FinishedWeight = null });
        var recipe = _service.Recipes().Single();
        Assert.That(recipe.Fiber, Is.Null);
        Assert.That(recipe.FinishedWeight, Is.Null);
        Assert.That(recipe.Kcal, Is.EqualTo(50));
    }

    /// <summary>
    /// Checks that a saved recipe without ingredients has unknown nutrition totals.
    /// </summary>
    [Test]
    public void Recipes_StoredRecipeWithoutIngredients_ReturnsUnknownTotals()
    {
        Sql("food_and_recipes.db", "INSERT INTO Recipes(Name) VALUES ('Empty')");
        var recipe = _service.Recipes().Single();
        Assert.That(recipe.Ingredients, Is.Empty);
        Assert.That(new[] { recipe.Kcal, recipe.Protein, recipe.Carbs, recipe.Fat, recipe.Fiber }, Is.All.Null);
    }

    /// <summary>
    /// Checks that updating a recipe replaces its ingredients while retaining its ID.
    /// </summary>
    [Test]
    public void SaveRecipe_Update_ReplacesIngredientsAndKeepsId()
    {
        var first = _service.SaveFood(null, Food());
        var second = _service.SaveFood(null, Food("Pear"));
        var id = _service.SaveRecipe(null, Recipe(first));
        Assert.That(_service.SaveRecipe(id, Recipe(second) with { Name = "Changed" }), Is.EqualTo(id));
        Assert.That(_service.Recipes().Single().Ingredients.Single().FoodId, Is.EqualTo(second));
        Assert.That(_service.Recipes().Single().Name, Is.EqualTo("Changed"));
    }

    /// <summary>
    /// Checks that updating a recipe with an unknown ID throws a missing-item error.
    /// </summary>
    [Test]
    public void SaveRecipe_UnknownId_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => _service.SaveRecipe(999, Recipe(1)));
    }

    /// <summary>
    /// Checks that invalid recipe inputs are rejected before saving.
    /// </summary>
    /// <param name="recipe">The invalid recipe submitted for validation.</param>
    [TestCaseSource(nameof(InvalidRecipes))]
    public void SaveRecipe_InvalidInput_Rejects(SaveRecipe recipe)
    {
        Assert.Throws<ArgumentException>(() => _service.SaveRecipe(null, recipe));
    }

    /// <summary>
    /// Enumerates recipes with invalid names, weights, or ingredients.
    /// </summary>
    /// <returns>The parameterized invalid recipes.</returns>
    private static IEnumerable<TestCaseData> InvalidRecipes()
    {
        var valid = Recipe(1);
        foreach (var name in new[] { null, "", " ", new string('n', 201) })
        {
            yield return new TestCaseData(valid with { Name = name! });
        }
        foreach (var weight in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
        {
            yield return new TestCaseData(valid with { FinishedWeight = weight });
        }
        yield return new TestCaseData(valid with { Ingredients = null! });
        yield return new TestCaseData(valid with { Ingredients = new() });
        yield return new TestCaseData(valid with { Ingredients = new() { null! } });
        foreach (var amount in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
        {
            yield return new TestCaseData(valid with { Ingredients = new() { new(1, amount) } });
        }
        yield return new TestCaseData(valid with { Ingredients = new() { new(0, 10) } });
        yield return new TestCaseData(valid with { Ingredients = new() { new(1, 10), new(1, 20) } });
    }

    /// <summary>
    /// Verifies that an invalid ingredient rolls back the recipe update and reports a validation error.
    /// </summary>
    [Test]
    public void SaveRecipe_ForeignKeyFailure_RollsBackPreviousRecipeAndIngredients()
    {
        var food = _service.SaveFood(null, Food());
        var id = _service.SaveRecipe(null, Recipe(food));
        var exception = Assert.Throws<ArgumentException>(() =>
            _service.SaveRecipe(id, Recipe(9999) with { Name = "Uncommitted" }));
        Assert.That(exception!.InnerException, Is.TypeOf<SqliteException>());
        var result = _service.Recipes().Single();
        Assert.That(result.Name, Is.EqualTo("Recipe"));
        Assert.That(result.Ingredients.Single().FoodId, Is.EqualTo(food));
    }

    /// <summary>
    /// Checks that deleting a recipe removes its ingredient links so the food can be deleted.
    /// </summary>
    [Test]
    public void DeleteRecipe_CascadesIngredientsAndAllowsDeletingFood()
    {
        var food = _service.SaveFood(null, Food());
        var recipe = _service.SaveRecipe(null, Recipe(food));
        _service.DeleteRecipe(recipe);
        _service.DeleteRecipe(recipe);
        _service.DeleteFood(food);
        Assert.That(_service.Recipes(), Is.Empty);
        Assert.That(_service.Foods(), Is.Empty);
    }

    /// <summary>
    /// Checks that library operations log and rethrow storage failures.
    /// </summary>
    /// <param name="operation">The operation exercised by the case.</param>
    [TestCase("Foods")]
    [TestCase("PagedFoods")]
    [TestCase("SaveFood")]
    [TestCase("DeleteFood")]
    [TestCase("Recipes")]
    [TestCase("SaveRecipe")]
    [TestCase("DeleteRecipe")]
    public void StorageFailure_EachMethodLogsAndRethrows(string operation)
    {
        SqliteConnection.ClearAllPools();
        File.WriteAllText(DataPath("food_and_recipes.db"), "not a sqlite database");
        TestDelegate action = operation switch
        {
            "Foods" => () => _service.Foods(), "PagedFoods" => () => _service.PagedFoods(null, 1, 10),
            "SaveFood" => () => _service.SaveFood(null, Food()), "DeleteFood" => () => _service.DeleteFood(1),
            "Recipes" => () => _service.Recipes(), "SaveRecipe" => () => _service.SaveRecipe(null, Recipe(1)),
            _ => () => _service.DeleteRecipe(1)
        };
        var exception = Assert.Throws<SqliteException>(action);
        _logger.AssertError(exception!, operation);
    }

    /// <summary>
    /// Checks that a corrupt library database is logged and rejected during initialization.
    /// </summary>
    [Test]
    public void Constructor_CorruptDatabase_LogsAndRethrows()
    {
        SqliteConnection.ClearAllPools();
        File.WriteAllText(DataPath("food_and_recipes.db"), "corrupt");
        var exception = Assert.Throws<SqliteException>(() => new FoodLibraryService(Environment, _logger));
        _logger.AssertError(exception!, "initialize");
    }
}
