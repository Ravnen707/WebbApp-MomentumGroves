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
    private static SaveLibraryFood Food(string name = "Apple", string unit = "g") => new(name, unit, 100, 2, 20, 3, 4);
    private static SaveRecipe Recipe(long foodId) => new("Recipe", 200, new() { new(foodId, 50) });

    [SetUp]
    public void SetUp()
    {
        _logger = new();
        _service = new(Environment, _logger);
    }

    [Test]
    public void Constructor_IsIdempotent_AndCreatesEmptyLibrary()
    {
        _ = new FoodLibraryService(Environment, _logger);
        Assert.That(_service.Foods(), Is.Empty);
        Assert.That(_service.Recipes(), Is.Empty);
    }

    [TestCase("g")]
    [TestCase("ml")]
    public void SaveFood_Insert_TrimsNameAndRoundTripsNutrition(string unit)
    {
        var id = _service.SaveFood(null, Food(" Apple ", unit));
        Assert.That(id, Is.GreaterThan(0));
        Assert.That(_service.Foods().Single(), Is.EqualTo(new LibraryFood(id, "Apple", unit, 100, 2, 20, 3, 4)));
    }

    [Test]
    public void SaveFood_Update_PreservesIdAndReplacesValues()
    {
        var id = _service.SaveFood(null, Food());
        Assert.That(_service.SaveFood(id, Food("Pear") with { Fiber = null }), Is.EqualTo(id));
        Assert.That(_service.Foods().Single().Name, Is.EqualTo("Pear"));
        Assert.That(_service.Foods().Single().Fiber, Is.Null);
    }

    [Test]
    public void SaveFood_UnknownId_ThrowsWithoutErrorLog()
    {
        Assert.Throws<KeyNotFoundException>(() => _service.SaveFood(999, Food()));
        Assert.That(_logger.Entries, Is.Empty);
    }

    [TestCase("")]
    [TestCase(" ")]
    public void SaveFood_MissingName_Rejects(string name) =>
        Assert.Throws<ArgumentException>(() => _service.SaveFood(null, Food(name)));

    [TestCase(200, true)]
    [TestCase(201, false)]
    public void SaveFood_NameLengthBoundary(int length, bool accepted)
    {
        if (accepted) Assert.That(_service.SaveFood(null, Food(new string('a', length))), Is.Positive);
        else Assert.Throws<ArgumentException>(() => _service.SaveFood(null, Food(new string('a', length))));
    }

    [TestCase("kg")]
    [TestCase("")]
    [TestCase("G")]
    public void SaveFood_InvalidUnit_Rejects(string unit) =>
        Assert.Throws<ArgumentException>(() => _service.SaveFood(null, Food(unit: unit)));

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

    private static IEnumerable<TestCaseData> InvalidNutrition()
    {
        foreach (var field in new[] { "Kcal", "Protein", "Carbs", "Fat", "Fiber" })
        foreach (var value in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            yield return new TestCaseData(field, value);
    }

    [Test]
    public void SaveFood_ZeroNutritionAndNullFiber_Accepted()
    {
        _service.SaveFood(null, new("Water", "ml", 0, 0, 0, 0, null));
        Assert.That(_service.Foods().Single().Kcal, Is.Zero);
    }

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

    [Test]
    public void Foods_LimitsResultsTo250()
    {
        Sql("food_and_recipes.db",
            "WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<251) INSERT INTO CustomFoods(Name,Unit,Kcal,Protein,Carbs,Fat) SELECT printf('Food%03d',x),'g',0,0,0,0 FROM n");
        Assert.That(_service.Foods(), Has.Count.EqualTo(250));
    }

    [TestCase(1, 2, 2, true)]
    [TestCase(2, 2, 1, false)]
    [TestCase(3, 2, 0, false)]
    [TestCase(0, 0, 1, true)]
    [TestCase(-1, -5, 1, true)]
    [TestCase(1, 100, 3, false)]
    [TestCase(int.MaxValue, 50, 0, false)]
    public void PagedFoods_ClampsBoundsAndReportsTotals(int page, int size, int count, bool hasMore)
    {
        foreach (var name in new[] { "One", "Two", "Three" }) _service.SaveFood(null, Food(name));
        var result = JsonSerializer.SerializeToElement(_service.PagedFoods(null, page, size));
        Assert.That(result.GetProperty("items").GetArrayLength(), Is.EqualTo(count));
        Assert.That(result.GetProperty("total").GetInt32(), Is.EqualTo(3));
        Assert.That(result.GetProperty("page").GetInt32(), Is.EqualTo(Math.Max(1, page)));
        Assert.That(result.GetProperty("pageSize").GetInt32(), Is.EqualTo(Math.Clamp(size, 1, 50)));
        Assert.That(result.GetProperty("hasMore").GetBoolean(), Is.EqualTo(hasMore));
    }

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

    [TestCase(false)]
    [TestCase(true)]
    public void DeleteFood_RemovesFood_WithOrWithoutMobileImportTable(bool tracking)
    {
        var id = _service.SaveFood(null, Food());
        if (tracking)
            Sql("food_and_recipes.db",
                "CREATE TABLE MobileFoodImports (MobileId TEXT, FoodId INTEGER); INSERT INTO MobileFoodImports VALUES ('id',$id)",
                ("$id", id));
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

    [Test]
    public void Recipes_StoredRecipeWithoutIngredients_ReturnsUnknownTotals()
    {
        Sql("food_and_recipes.db", "INSERT INTO Recipes(Name) VALUES ('Empty')");
        var recipe = _service.Recipes().Single();
        Assert.That(recipe.Ingredients, Is.Empty);
        Assert.That(new[] { recipe.Kcal, recipe.Protein, recipe.Carbs, recipe.Fat, recipe.Fiber }, Is.All.Null);
    }

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

    [Test]
    public void SaveRecipe_UnknownId_Throws() =>
        Assert.Throws<KeyNotFoundException>(() => _service.SaveRecipe(999, Recipe(1)));

    [TestCaseSource(nameof(InvalidRecipes))]
    public void SaveRecipe_InvalidInput_Rejects(SaveRecipe recipe) =>
        Assert.Throws<ArgumentException>(() => _service.SaveRecipe(null, recipe));

    private static IEnumerable<TestCaseData> InvalidRecipes()
    {
        var valid = Recipe(1);
        foreach (var name in new[] { null, "", " ", new string('n', 201) })
            yield return new TestCaseData(valid with { Name = name! });
        foreach (var weight in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
            yield return new TestCaseData(valid with { FinishedWeight = weight });
        yield return new TestCaseData(valid with { Ingredients = null! });
        yield return new TestCaseData(valid with { Ingredients = new() });
        foreach (var amount in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
            yield return new TestCaseData(valid with { Ingredients = new() { new(1, amount) } });
        yield return new TestCaseData(valid with { Ingredients = new() { new(0, 10) } });
        yield return new TestCaseData(valid with { Ingredients = new() { new(1, 10), new(1, 20) } });
    }

    [Test]
    public void SaveRecipe_ForeignKeyFailure_RollsBackPreviousRecipeAndIngredients()
    {
        var food = _service.SaveFood(null, Food());
        var id = _service.SaveRecipe(null, Recipe(food));
        Assert.Throws<SqliteException>(() => _service.SaveRecipe(id, Recipe(9999) with { Name = "Uncommitted" }));
        var result = _service.Recipes().Single();
        Assert.That(result.Name, Is.EqualTo("Recipe"));
        Assert.That(result.Ingredients.Single().FoodId, Is.EqualTo(food));
    }

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

    [Test]
    public void Constructor_CorruptDatabase_LogsAndRethrows()
    {
        SqliteConnection.ClearAllPools();
        File.WriteAllText(DataPath("food_and_recipes.db"), "corrupt");
        var exception = Assert.Throws<SqliteException>(() => new FoodLibraryService(Environment, _logger));
        _logger.AssertError(exception!, "initialize");
    }
}