using System.Data;
using System.Data.Common;
using System.Globalization;
using MomentumGroves.Data;
using MomentumGroves.Models;

namespace MomentumGroves.Service;

/// <summary>
/// Manages stored foods, recipes, and nutrition calculations.
/// </summary>
public sealed class FoodLibraryService
{
    private readonly ILogger<FoodLibraryService> _logger;
    private readonly PersonalDatabase _database;

    /// <summary>
    /// Initializes the food and recipe database.
    /// </summary>
    public FoodLibraryService(IWebHostEnvironment environment, ILogger<FoodLibraryService> logger)
        : this(PersonalDatabase.Local(environment), logger)
    {
    }

    public FoodLibraryService(PersonalDatabase database, ILogger<FoodLibraryService> logger)
    {
        _database = database;
        _logger = logger;
        try
        {
            _database.InitializeLibrary();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not initialize FoodLibraryService storage");
            throw;
        }
    }

    private DbConnection Open() => _database.Open("food_and_recipes.db");

    private string FoodVersionExpression => _database.MobileDataApiEnabled ? "md5(to_jsonb(f)::text)" : "NULL";

    /// <summary>
    /// Maps a database row to a library food.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    private static LibraryFood ReadFood(DbDataReader reader)
    {
        return new LibraryFood(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetDouble(3),
            reader.GetDouble(4), reader.GetDouble(5), reader.GetDouble(6),
            reader.IsDBNull(7) ? null : reader.GetDouble(7), reader.IsDBNull(8) ? null : reader.GetString(8));
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
                $"SELECT Id, Name, Unit, Kcal, Protein, Carbs, Fat, Fiber, {FoodVersionExpression} FROM CustomFoods AS f WHERE @query = '' OR {_database.FoodNameMatch} ORDER BY Name LIMIT 250";
            command.AddParameter("@query", query?.Trim() ?? "");
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
                $"SELECT COUNT(*) FROM CustomFoods WHERE @query = '' OR {_database.FoodNameMatch}";
            count.AddParameter("@query", search);
            var total = Convert.ToInt32(count.ExecuteScalar());
            using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT Id, Name, Unit, Kcal, Protein, Carbs, Fat, Fiber, {FoodVersionExpression} FROM CustomFoods AS f WHERE @query = '' OR {_database.FoodNameMatch} ORDER BY Id DESC LIMIT @limit OFFSET @offset";
            command.AddParameter("@query", search);
            command.AddParameter("@limit", pageSize);
            command.AddParameter("@offset", (long)(page - 1) * pageSize);
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
    public long SaveFood(long? id, SaveLibraryFood food, string? version = null)
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
            using var transaction = _database.UsesPostgres ? connection.BeginTransaction() : null;
            if (transaction is not null)
            {
                _database.LockFoodWrites(connection, transaction);
                if (id.HasValue && _database.MobileDataApiEnabled)
                    VerifyFoodVersion(connection, transaction, id.Value, version);

                using var duplicate = connection.CreateCommand();
                duplicate.Transaction = transaction;
                duplicate.CommandText = """
                    SELECT 1 FROM CustomFoods
                    WHERE lower(trim(Name)) = lower(@name) AND Id IS DISTINCT FROM @id
                        AND NOT EXISTS (SELECT 1 FROM CustomFoods WHERE Id = @id AND lower(trim(Name)) = lower(@name))
                    LIMIT 1
                    """;
                duplicate.AddParameter("@name", food.Name.Trim());
                duplicate.AddParameter("@id", id, System.Data.DbType.Int64);
                if (duplicate.ExecuteScalar() is not null)
                    throw new ArgumentException("A food with this name already exists.");
            }

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = id.HasValue
                ? "UPDATE CustomFoods SET Name=@name, Unit=@unit, Kcal=@kcal, Protein=@protein, Carbs=@carbs, Fat=@fat, Fiber=@fiber WHERE Id=@id"
                : "INSERT INTO CustomFoods(Name,Unit,Kcal,Protein,Carbs,Fat,Fiber) VALUES(@name,@unit,@kcal,@protein,@carbs,@fat,@fiber) RETURNING Id";
            if (id.HasValue)
                command.AddParameter("@id", id.Value);
            command.AddParameter("@name", food.Name.Trim());
            command.AddParameter("@unit", food.Unit);
            command.AddParameter("@kcal", food.Kcal);
            command.AddParameter("@protein", food.Protein);
            command.AddParameter("@carbs", food.Carbs);
            command.AddParameter("@fat", food.Fat);
            command.AddParameter("@fiber", food.Fiber, DbType.Double);
            if (!id.HasValue)
            {
                var createdId = (long)command.ExecuteScalar()!;
                transaction?.Commit();
                return createdId;
            }

            if (command.ExecuteNonQuery() != 1)
                throw new KeyNotFoundException("The food was not found.");

            transaction?.Commit();
            return id.Value;
        }
        catch (Exception exception) when (exception is not (ArgumentException or KeyNotFoundException or InvalidOperationException))
        {
            _logger.LogError(exception, "Food library operation {Operation} failed", nameof(SaveFood));
            throw;
        }
    }

    /// <summary>
    /// Deletes a food from the library.
    /// </summary>
    public void DeleteFood(long id, string? version = null)
    {
        try
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            _database.LockFoodDeletion(connection, transaction);
            if (_database.MobileDataApiEnabled)
                VerifyFoodVersion(connection, transaction, id, version);
            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = "SELECT 1 FROM RecipeIngredients WHERE FoodId=@id LIMIT 1";
            command.AddParameter("@id", id);
            if (command.ExecuteScalar() != null)
            {
                throw new InvalidOperationException("The food is used in a recipe and cannot be deleted.");
            }

            command.CommandText = "DELETE FROM MobileFoodImports WHERE FoodId=@id";
            if (_database.HasMobileImports(connection, transaction))
                command.ExecuteNonQuery();

            command.CommandText = "DELETE FROM CustomFoods WHERE Id=@id";
            command.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (Exception exception) when (exception is not (ArgumentException or KeyNotFoundException
                                              or InvalidOperationException))
        {
            _logger.LogError(exception, "Food library operation {Operation} failed", nameof(DeleteFood));
            throw;
        }
    }

    private static void VerifyFoodVersion(DbConnection connection, DbTransaction transaction, long id, string? version)
    {
        using var current = connection.CreateCommand();
        current.Transaction = transaction;
        current.CommandText = "SELECT md5(to_jsonb(f)::text) FROM CustomFoods AS f WHERE Id=@id";
        current.AddParameter("@id", id);
        if (current.ExecuteScalar() is not string stored || stored != version)
            throw new InvalidOperationException("This food changed or was removed. Refresh the food library and review it.");
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
                                         f.Kcal, f.Protein, f.Carbs, f.Fat, f.Fiber, r.CreatedUtc
                                  FROM Recipes r LEFT JOIN RecipeIngredients i ON i.RecipeId=r.Id
                                  LEFT JOIN CustomFoods f ON f.Id=i.FoodId ORDER BY r.Name, f.Name
                                  """;
            using var reader = command.ExecuteReader();
            var recipes = new Dictionary<long, (string Name, double? Weight, DateTimeOffset CreatedUtc,
                List<RecipeIngredientDetail> Items)>();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                if (!recipes.ContainsKey(id))
                {
                    recipes[id] = (reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetDouble(2),
                        DateTimeOffset.Parse(reader.GetString(12), CultureInfo.InvariantCulture), new());
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
                var (name, weight, createdUtc, items) = pair.Value;

                double? total(Func<RecipeIngredientDetail, double?> selector) => items.Any(x => selector(x).HasValue)
                    ? items.Sum(x => (selector(x) ?? 0) * x.Amount / 100)
                    : null;

                return new LibraryRecipe(pair.Key, name, weight, items, total(x => x.Kcal), total(x => x.Protein),
                    total(x => x.Carbs), total(x => x.Fat), total(x => x.Fiber), createdUtc);
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
                ? "UPDATE Recipes SET Name=@name, FinishedWeight=@weight WHERE Id=@id"
                : """
                  INSERT INTO Recipes(Name, FinishedWeight, CreatedUtc)
                  VALUES(@name, @weight, @created) RETURNING Id
                  """;
            if (id.HasValue)
                command.AddParameter("@id", id.Value);
            command.AddParameter("@name", recipe.Name.Trim());
            command.AddParameter("@weight", recipe.FinishedWeight, DbType.Double);
            if (!id.HasValue)
            {
                command.AddParameter("@created", PersonalDatabase.UtcNow());
                id = (long)command.ExecuteScalar()!;
            }
            else if (command.ExecuteNonQuery() != 1)
            {
                throw new KeyNotFoundException("The recipe was not found.");
            }

            command.Parameters.Clear();
            command.CommandText = "DELETE FROM RecipeIngredients WHERE RecipeId=@id";
            command.AddParameter("@id", id.Value);
            command.ExecuteNonQuery();
            foreach (var ingredient in recipe.Ingredients)
            {
                command.Parameters.Clear();
                command.CommandText = "INSERT INTO RecipeIngredients(RecipeId,FoodId,Amount) VALUES(@id,@food,@amount)";
                command.AddParameter("@id", id.Value);
                command.AddParameter("@food", ingredient.FoodId);
                command.AddParameter("@amount", ingredient.Amount);
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
            command.CommandText = "DELETE FROM Recipes WHERE Id=@id";
            command.AddParameter("@id", id);
            command.ExecuteNonQuery();
        }
        catch (Exception exception) when (exception is not (ArgumentException or KeyNotFoundException))
        {
            _logger.LogError(exception, "Food library operation {Operation} failed", nameof(DeleteRecipe));
            throw;
        }
    }
}