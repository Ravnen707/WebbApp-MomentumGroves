using System.Data;
using System.Data.Common;
using System.Globalization;
using MomentumGroves.Data;
using MomentumGroves.Models;

namespace MomentumGroves.Repositories;

/// <summary>
/// Persists the personal food and recipe library using the configured database provider.
/// </summary>
/// <param name="database">The configured personal database.</param>
/// <param name="logger">The logger for food library storage failures.</param>
internal sealed class FoodLibraryRepository(PersonalDatabase database, ILogger logger)
{
    /// <summary>
    /// Opens a connection to the library database.
    /// </summary>
    /// <returns>An open library database connection.</returns>
    private DbConnection Open()
    {
        return database.Open("food_and_recipes.db");
    }

    private string FoodVersionExpression => database.MobileDataApiEnabled ? "md5(to_jsonb(f)::text)" : "NULL";

    /// <summary>
    /// Maps a database row to a library food.
    /// </summary>
    /// <param name="reader">The reader positioned at the input value or row.</param>
    /// <returns>The food and its version mapped from the current row.</returns>
    private static LibraryFood ReadFood(DbDataReader reader)
    {
        var fiber = reader.IsDBNull(7) ? (double?)null : reader.GetDouble(7);
        var version = reader.IsDBNull(8) ? null : reader.GetString(8);
        return new LibraryFood(Id: reader.GetInt64(0), Name: reader.GetString(1),
            Unit: reader.GetString(2), Kcal: reader.GetDouble(3),
            Protein: reader.GetDouble(4), Carbs: reader.GetDouble(5),
            Fat: reader.GetDouble(6), Fiber: fiber, Version: version);
    }

    /// <summary>
    /// Finds saved foods by name.
    /// </summary>
    /// <param name="query">The name fragment to search for.</param>
    /// <returns>The matching saved foods in name order.</returns>
    internal List<LibraryFood> Foods(string? query = null)
    {
        try
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT Id, Name, Unit, Kcal, Protein, Carbs, Fat, Fiber, {FoodVersionExpression} " +
                $"FROM CustomFoods AS f WHERE @query = '' OR {database.FoodNameMatch} ORDER BY Name LIMIT 250";
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
            logger.LogError(exception, "Food library operation {Operation} failed", nameof(Foods));
            throw;
        }
    }

    /// <summary>
    /// Returns a page of saved foods, newest first, matching the entire library.
    /// </summary>
    /// <param name="query">The name fragment to search for.</param>
    /// <param name="page">The requested one-based page number.</param>
    /// <param name="pageSize">The requested number of items per page.</param>
    /// <returns>The matching foods and the total number of matches.</returns>
    internal (List<LibraryFood> Items, int Total) PagedFoods(string? query, int page, int pageSize)
    {
        try
        {
            var search = query?.Trim() ?? "";
            using var connection = Open();
            using var count = connection.CreateCommand();
            count.CommandText =
                $"SELECT COUNT(*) FROM CustomFoods WHERE @query = '' OR {database.FoodNameMatch}";
            count.AddParameter("@query", search);
            var total = Convert.ToInt32(count.ExecuteScalar());
            using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT Id, Name, Unit, Kcal, Protein, Carbs, Fat, Fiber, {FoodVersionExpression} " +
                $"FROM CustomFoods AS f WHERE @query = '' OR {database.FoodNameMatch} " +
                "ORDER BY Id DESC LIMIT @limit OFFSET @offset";
            command.AddParameter("@query", search);
            command.AddParameter("@limit", pageSize);
            command.AddParameter("@offset", (long)(page - 1) * pageSize);
            using var reader = command.ExecuteReader();
            var items = new List<LibraryFood>();
            while (reader.Read())
            {
                items.Add(ReadFood(reader));
            }

            return (items, total);
        }
        catch (Exception exception) when (exception is not (ArgumentException or KeyNotFoundException))
        {
            logger.LogError(exception, "Food library operation {Operation} failed", nameof(PagedFoods));
            throw;
        }
    }

    /// <summary>
    /// Creates or updates a library food.
    /// </summary>
    /// <param name="id">The identifier of the existing item.</param>
    /// <param name="food">The submitted food and nutrition values.</param>
    /// <param name="version">The revision submitted by the client.</param>
    /// <returns>The ID of the inserted or updated food.</returns>
    internal long SaveFood(long? id, SaveLibraryFood food, string? version = null)
    {
        try
        {
            using var connection = Open();
            using var transaction = database.UsesPostgres ? connection.BeginTransaction() : null;
            if (transaction is not null)
            {
                database.LockFoodWrites(connection, transaction);
                if (id.HasValue && database.MobileDataApiEnabled)
                {
                    VerifyFoodVersion(connection, transaction, id.Value, version);
                }

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
                {
                    throw new ArgumentException("A food with this name already exists.");
                }
            }

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = id.HasValue
                ? "UPDATE CustomFoods SET Name=@name, Unit=@unit, Kcal=@kcal, Protein=@protein, Carbs=@carbs, " +
                  "Fat=@fat, Fiber=@fiber WHERE Id=@id"
                : "INSERT INTO CustomFoods(Name,Unit,Kcal,Protein,Carbs,Fat,Fiber) VALUES(@name,@unit,@kcal,@protein," +
                  "@carbs,@fat,@fiber) RETURNING Id";
            if (id.HasValue)
            {
                command.AddParameter("@id", id.Value);
            }

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
            {
                throw new KeyNotFoundException("The food was not found.");
            }

            transaction?.Commit();
            return id.Value;
        }
        catch (Exception exception)
            when (exception is not (ArgumentException or KeyNotFoundException or InvalidOperationException))
        {
            logger.LogError(exception, "Food library operation {Operation} failed", nameof(SaveFood));
            throw;
        }
    }

    /// <summary>
    /// Deletes a food from the library.
    /// </summary>
    /// <param name="id">The identifier of the existing item.</param>
    /// <param name="version">The revision submitted by the client.</param>
    internal void DeleteFood(long id, string? version = null)
    {
        try
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            database.LockFoodDeletion(connection, transaction);
            if (database.MobileDataApiEnabled)
            {
                VerifyFoodVersion(connection, transaction, id, version);
            }

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            command.CommandText = "SELECT 1 FROM RecipeIngredients WHERE FoodId=@id LIMIT 1";
            command.AddParameter("@id", id);
            if (command.ExecuteScalar() != null)
            {
                throw new InvalidOperationException("The food is used in a recipe and cannot be deleted.");
            }

            command.CommandText = "DELETE FROM MobileFoodImports WHERE FoodId=@id";
            if (database.HasMobileImports(connection, transaction))
            {
                command.ExecuteNonQuery();
            }

            command.CommandText = "DELETE FROM CustomFoods WHERE Id=@id";
            command.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (Exception exception) when (exception is not (ArgumentException or KeyNotFoundException
                                              or InvalidOperationException))
        {
            logger.LogError(exception, "Food library operation {Operation} failed", nameof(DeleteFood));
            throw;
        }
    }

    /// <summary>
    /// Rejects writes when the supplied revision cannot match the stored food.
    /// </summary>
    /// <param name="connection">The connection used for the database operation.</param>
    /// <param name="transaction">The transaction that contains the database operation.</param>
    /// <param name="id">The identifier of the existing item.</param>
    /// <param name="version">The revision submitted by the client.</param>
    private static void VerifyFoodVersion(DbConnection connection, DbTransaction transaction, long id, string? version)
    {
        using var current = connection.CreateCommand();
        current.Transaction = transaction;
        current.CommandText = "SELECT md5(to_jsonb(f)::text) FROM CustomFoods AS f WHERE Id=@id";
        current.AddParameter("@id", id);
        if (current.ExecuteScalar() is not string stored || stored != version)
        {
            throw new InvalidOperationException(
                "This food changed or was removed. Refresh the food library and review it.");
        }
    }

    /// <summary>
    /// Loads recipe metadata and ingredient nutrition from storage.
    /// </summary>
    /// <returns>The stored recipes and their ingredient amounts.</returns>
    internal List<StoredRecipe> Recipes()
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
                    var weight = reader.IsDBNull(2) ? (double?)null : reader.GetDouble(2);
                    var createdUtc = DateTimeOffset.Parse(reader.GetString(12),
                        CultureInfo.InvariantCulture);
                    recipes[id] = (reader.GetString(1), weight, createdUtc, new());
                }

                if (!reader.IsDBNull(3))
                {
                    var fiber = reader.IsDBNull(11) ? (double?)null : reader.GetDouble(11);
                    recipes[id].Items.Add(new RecipeIngredientDetail(
                        FoodId: reader.GetInt64(3), Name: reader.GetString(4),
                        Unit: reader.GetString(5), Amount: reader.GetDouble(6),
                        Kcal: reader.GetDouble(7), Protein: reader.GetDouble(8),
                        Carbs: reader.GetDouble(9), Fat: reader.GetDouble(10), Fiber: fiber));
                }
            }

            return recipes.Select(pair => new StoredRecipe(pair.Key, pair.Value.Name, pair.Value.Weight,
                pair.Value.CreatedUtc, pair.Value.Items)).ToList();
        }
        catch (Exception exception) when (exception is not (ArgumentException or KeyNotFoundException))
        {
            logger.LogError(exception, "Food library operation {Operation} failed", nameof(Recipes));
            throw;
        }
    }

    /// <summary>
    /// Creates or updates a recipe and its ingredients.
    /// </summary>
    /// <param name="id">The identifier of the existing item.</param>
    /// <param name="recipe">The submitted recipe details and ingredients.</param>
    /// <returns>The ID of the inserted or updated recipe.</returns>
    internal long SaveRecipe(long? id, SaveRecipe recipe)
    {
        try
        {
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
            {
                command.AddParameter("@id", id.Value);
            }

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
        catch (DbException exception) when (PersonalDatabase.IsConstraintViolation(exception))
        {
            throw new ArgumentException("Kontrollér ingredienserne i opskriften.", exception);
        }
        catch (Exception exception) when (exception is not (ArgumentException or KeyNotFoundException))
        {
            logger.LogError(exception, "Food library operation {Operation} failed", nameof(SaveRecipe));
            throw;
        }
    }

    /// <summary>
    /// Deletes a recipe and its ingredients.
    /// </summary>
    /// <param name="id">The identifier of the existing item.</param>
    internal void DeleteRecipe(long id)
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
            logger.LogError(exception, "Food library operation {Operation} failed", nameof(DeleteRecipe));
            throw;
        }
    }
}
