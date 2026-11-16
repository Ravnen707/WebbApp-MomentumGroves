using Microsoft.Data.Sqlite;
using MomentumGroves.Models;

namespace MomentumGroves.Service;

/// <summary>
/// Manages stored foods, recipes, and nutrition calculations.
/// </summary>
public sealed class FoodLibraryService
{
    private readonly ILogger<FoodLibraryService> _logger;
    private readonly string _connectionString;

    /// <summary>
    /// Initializes the food and recipe database.
    /// </summary>
    public FoodLibraryService(IWebHostEnvironment environment, ILogger<FoodLibraryService> logger)
    {
        _logger = logger;
        try
        {
            var path = Path.Combine(environment.ContentRootPath, "src", "Data", "food_and_recipes.db");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                                  CREATE TABLE IF NOT EXISTS CustomFoods (
                                      Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                      Name TEXT NOT NULL, Unit TEXT NOT NULL CHECK(Unit IN ('g','ml')),
                                      Kcal REAL NOT NULL, Protein REAL NOT NULL, Carbs REAL NOT NULL,
                                      Fat REAL NOT NULL, Fiber REAL NULL
                                  );
                                  CREATE TABLE IF NOT EXISTS Recipes (
                                      Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                      Name TEXT NOT NULL, FinishedWeight REAL NULL
                                  );
                                  CREATE TABLE IF NOT EXISTS RecipeIngredients (
                                      RecipeId INTEGER NOT NULL REFERENCES Recipes(Id) ON DELETE CASCADE,
                                      FoodId INTEGER NOT NULL REFERENCES CustomFoods(Id) ON DELETE RESTRICT,
                                      Amount REAL NOT NULL CHECK(Amount > 0),
                                      PRIMARY KEY (RecipeId, FoodId)
                                  );
                                  """;
            command.ExecuteNonQuery();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not initialize FoodLibraryService storage");
            throw;
        }
    }

    /// <summary>
    /// Opens a database connection with foreign keys enabled.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Adds a parameter to a SQLite command.
    /// </summary>
    private static void Add(SqliteCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    /// <summary>
    /// Maps a database row to a library food.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    private static LibraryFood ReadFood(SqliteDataReader reader)
    {
        return new LibraryFood(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetDouble(3),
            reader.GetDouble(4), reader.GetDouble(5), reader.GetDouble(6),
            reader.IsDBNull(7) ? null : reader.GetDouble(7));
    }

    /// <summary>
    /// Finds saved foods by name.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public List<LibraryFood> Foods(string? query = null)
    {
        try
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT Id, Name, Unit, Kcal, Protein, Carbs, Fat, Fiber FROM CustomFoods WHERE $query = '' OR instr(lower(Name), lower($query)) > 0 ORDER BY Name LIMIT 250";
            Add(command, "$query", query?.Trim() ?? "");
            using var reader = command.ExecuteReader();
            var result = new List<LibraryFood>();
            while (reader.Read())
            {
                result.Add(ReadFood(reader));
            }

            return result;
        }
        catch (Exception exception) when (exception is not (ArgumentException or KeyNotFoundException))
        {
            _logger.LogError(exception, "Food library operation {Operation} failed", nameof(Foods));
            throw;
        }
    }

    /// <summary>
    /// Returns a page of saved foods, newest first, matching the entire library.
    /// </summary>
    public object PagedFoods(string? query, int page, int pageSize)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);
            var search = query?.Trim() ?? "";
            using var connection = Open();
            using var count = connection.CreateCommand();
            count.CommandText =
                "SELECT COUNT(*) FROM CustomFoods WHERE $query = '' OR instr(lower(Name), lower($query)) > 0";
            Add(count, "$query", search);
            var total = Convert.ToInt32(count.ExecuteScalar());
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT Id, Name, Unit, Kcal, Protein, Carbs, Fat, Fiber FROM CustomFoods WHERE $query = '' OR instr(lower(Name), lower($query)) > 0 ORDER BY Id DESC LIMIT $limit OFFSET $offset";
            Add(command, "$query", search);
            Add(command, "$limit", pageSize);
            Add(command, "$offset", (long)(page - 1) * pageSize);
            using var reader = command.ExecuteReader();
            var items = new List<LibraryFood>();
            while (reader.Read())
            {
                items.Add(ReadFood(reader));
            }

            return new { items, total, page, pageSize, hasMore = (long)page * pageSize < total };
        }
        catch (Exception exception) when (exception is not (ArgumentException or KeyNotFoundException))
        {
            _logger.LogError(exception, "Food library operation {Operation} failed", nameof(PagedFoods));
            throw;
        }
    }

    /// <summary>
    /// Creates or updates a library food.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public long SaveFood(long? id, SaveLibraryFood food)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(food.Name) || food.Name.Length > 200 || food.Unit is not ("g" or "ml") ||
                new[] { food.Kcal, food.Protein, food.Carbs, food.Fat, food.Fiber ?? 0 }.Any(x =>
                    !double.IsFinite(x) || x < 0))
            {
                throw new ArgumentException("Invalid food details.");
            }

            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = id.HasValue
                ? "UPDATE CustomFoods SET Name=$name, Unit=$unit, Kcal=$kcal, Protein=$protein, Carbs=$carbs, Fat=$fat, Fiber=$fiber WHERE Id=$id"
                : "INSERT INTO CustomFoods(Name,Unit,Kcal,Protein,Carbs,Fat,Fiber) VALUES($name,$unit,$kcal,$protein,$carbs,$fat,$fiber)";
            Add(command, "$id", id);
            Add(command, "$name", food.Name.Trim());
            Add(command, "$unit", food.Unit);
            Add(command, "$kcal", food.Kcal);
            Add(command, "$protein", food.Protein);
            Add(command, "$carbs", food.Carbs);
            Add(command, "$fat", food.Fat);
            Add(command, "$fiber", food.Fiber);
            if (command.ExecuteNonQuery() != 1)
            {
                throw new KeyNotFoundException("The food was not found.");
            }

            if (id.HasValue)
            {
                return id.Value;
            }

            using var last = connection.CreateCommand();
            last.CommandText = "SELECT last_insert_rowid()";
            return (long)last.ExecuteScalar()!;
        }
        catch (Exception exception) when (exception is not (ArgumentException or KeyNotFoundException))
        {
            _logger.LogError(exception, "Food library operation {Operation} failed", nameof(SaveFood));
            throw;
        }
    }

    /// <summary>
    /// Deletes a food from the library.
    /// </summary>
    public void DeleteFood(long id)
    {
        try
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = "SELECT 1 FROM RecipeIngredients WHERE FoodId=$id LIMIT 1";
            Add(command, "$id", id);
            if (command.ExecuteScalar() != null)
            {
                throw new InvalidOperationException("The food is used in a recipe and cannot be deleted.");
            }

            command.CommandText = "DELETE FROM MobileFoodImports WHERE FoodId=$id";
            // Older databases may not have the mobile import tracking table.
            using (var table = connection.CreateCommand())
            {
                table.Transaction = transaction;
                table.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name='MobileFoodImports'";
                if (table.ExecuteScalar() != null)
                {
                    command.ExecuteNonQuery();
                }
            }

            command.CommandText = "DELETE FROM CustomFoods WHERE Id=$id";
            command.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (Exception exception) when (exception is not (ArgumentException or KeyNotFoundException or InvalidOperationException))
        {
            _logger.LogError(exception, "Food library operation {Operation} failed", nameof(DeleteFood));
            throw;
        }
    }

    /// <summary>
    /// Loads recipes and calculates total nutrition from ingredient quantities.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public List<LibraryRecipe> Recipes()
    {
        try
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                                  SELECT r.Id, r.Name, r.FinishedWeight, i.FoodId, f.Name, f.Unit, i.Amount,
                                         f.Kcal, f.Protein, f.Carbs, f.Fat, f.Fiber
                                  FROM Recipes r LEFT JOIN RecipeIngredients i ON i.RecipeId=r.Id
                                  LEFT JOIN CustomFoods f ON f.Id=i.FoodId ORDER BY r.Name, f.Name
                                  """;
            using var reader = command.ExecuteReader();
            var recipes = new Dictionary<long, (string Name, double? Weight, List<RecipeIngredientDetail> Items)>();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                if (!recipes.ContainsKey(id))
                {
                    recipes[id] = (reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetDouble(2), new());
                }

                if (!reader.IsDBNull(3))
                {
                    recipes[id].Items.Add(new RecipeIngredientDetail(reader.GetInt64(3), reader.GetString(4),
                        reader.GetString(5), reader.GetDouble(6), reader.GetDouble(7), reader.GetDouble(8),
                        reader.GetDouble(9), reader.GetDouble(10), reader.IsDBNull(11) ? null : reader.GetDouble(11)));
                }
            }

            return recipes.Select(pair =>
            {
                var (name, weight, items) = pair.Value;

                double? total(Func<RecipeIngredientDetail, double?> selector) => items.Count > 0
                    ? items.Any(x => selector(x) is null)
                        ? null
                        : items.Sum(x => selector(x)!.Value * x.Amount / 100)
                    : null;

                return new LibraryRecipe(pair.Key, name, weight, items, total(x => x.Kcal), total(x => x.Protein),
                    total(x => x.Carbs), total(x => x.Fat), total(x => x.Fiber));
            }).ToList();
        }
        catch (Exception exception) when (exception is not (ArgumentException or KeyNotFoundException))
        {
            _logger.LogError(exception, "Food library operation {Operation} failed", nameof(Recipes));
            throw;
        }
    }

    /// <summary>
    /// Creates or updates a recipe and its ingredients.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public long SaveRecipe(long? id, SaveRecipe recipe)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(recipe.Name) || recipe.Name.Length > 200 ||
                recipe.FinishedWeight is double w && (!double.IsFinite(w) || w <= 0) ||
                recipe.Ingredients is null || recipe.Ingredients.Count == 0 ||
                recipe.Ingredients.Any(x => x.FoodId <= 0 || !double.IsFinite(x.Amount) || x.Amount <= 0) ||
                recipe.Ingredients.Select(x => x.FoodId).Distinct().Count() != recipe.Ingredients.Count)
            {
                throw new ArgumentException("The recipe does not contain valid details.");
            }

            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = id.HasValue
                ? "UPDATE Recipes SET Name=$name, FinishedWeight=$weight WHERE Id=$id"
                : "INSERT INTO Recipes(Name,FinishedWeight) VALUES($name,$weight)";
            Add(command, "$id", id);
            Add(command, "$name", recipe.Name.Trim());
            Add(command, "$weight", recipe.FinishedWeight);
            if (command.ExecuteNonQuery() != 1)
            {
                throw new KeyNotFoundException("The recipe was not found.");
            }

            if (!id.HasValue)
            {
                command.Parameters.Clear();
                command.CommandText = "SELECT last_insert_rowid()";
                id = (long)command.ExecuteScalar()!;
            }

            command.Parameters.Clear();
            command.CommandText = "DELETE FROM RecipeIngredients WHERE RecipeId=$id";
            Add(command, "$id", id.Value);
            command.ExecuteNonQuery();
            foreach (var ingredient in recipe.Ingredients)
            {
                command.Parameters.Clear();
                command.CommandText = "INSERT INTO RecipeIngredients(RecipeId,FoodId,Amount) VALUES($id,$food,$amount)";
                Add(command, "$id", id.Value);
                Add(command, "$food", ingredient.FoodId);
                Add(command, "$amount", ingredient.Amount);
                command.ExecuteNonQuery();
            }

            transaction.Commit();
            return id.Value;
        }
        catch (Exception exception) when (exception is not (ArgumentException or KeyNotFoundException))
        {
            _logger.LogError(exception, "Food library operation {Operation} failed", nameof(SaveRecipe));
            throw;
        }
    }

    /// <summary>
    /// Deletes a recipe and its ingredients.
    /// </summary>
    public void DeleteRecipe(long id)
    {
        try
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Recipes WHERE Id=$id";
            Add(command, "$id", id);
            command.ExecuteNonQuery();
        }
        catch (Exception exception) when (exception is not (ArgumentException or KeyNotFoundException))
        {
            _logger.LogError(exception, "Food library operation {Operation} failed", nameof(DeleteRecipe));
            throw;
        }
    }
}
