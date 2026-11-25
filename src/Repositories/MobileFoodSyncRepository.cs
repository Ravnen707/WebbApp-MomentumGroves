using System.Data;
using MomentumGroves.Data;
using MomentumGroves.Models;

namespace MomentumGroves.Repositories;

/// <summary>Persists mobile food imports in the existing desktop food database.</summary>
public sealed class MobileFoodSyncRepository
{
    private readonly PersonalDatabase _database;

    /// <summary>
    /// Initializes food import persistence with the supplied storage configuration.
    /// </summary>
    /// <param name="environment">The environment that supplies the application content root.</param>
    public MobileFoodSyncRepository(IWebHostEnvironment environment)
        : this(PersonalDatabase.Local(environment))
    {
    }

    /// <summary>
    /// Initializes food import persistence with the supplied storage configuration.
    /// </summary>
    /// <param name="database">The configured personal database.</param>
    public MobileFoodSyncRepository(PersonalDatabase database)
    {
        _database = database;
    }

    /// <summary>
    /// Persists the mobile batch and acknowledges previously processed items.
    /// </summary>
    /// <param name="foods">The foods submitted for import.</param>
    /// <returns>The import counts and acknowledged mobile IDs.</returns>
    public MobileFoodImportResult Import(List<FoodSyncDto> foods)
    {
        using var connection = _database.Open("food_and_recipes.db");
        using var transaction = connection.BeginTransaction();
        _database.EnsureMobileImports(connection, transaction);

        var imported = 0;
        var skipped = 0;
        var acknowledgedIds = new List<Guid>();
        foreach (var food in foods)
        {
            using var previous = connection.CreateCommand();
            previous.Transaction = transaction;
            previous.CommandText = "SELECT 1 FROM MobileFoodImports WHERE MobileId = @id LIMIT 1";
            previous.AddParameter("@id", food.Id.ToString("D"));
            if (previous.ExecuteScalar() != null)
            {
                skipped++;
                acknowledgedIds.Add(food.Id);
                continue;
            }

            using var duplicate = connection.CreateCommand();
            duplicate.Transaction = transaction;
            duplicate.CommandText = "SELECT 1 FROM CustomFoods WHERE lower(trim(Name)) = lower(@name) LIMIT 1";
            duplicate.AddParameter("@name", food.Name.Trim());
            if (duplicate.ExecuteScalar() != null)
            {
                skipped++;
                acknowledgedIds.Add(food.Id);
                continue;
            }

            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                "INSERT INTO CustomFoods(Name,Unit,Kcal,Protein,Carbs,Fat,Fiber) VALUES(@name,@unit,@kcal,@protein,@carbs,@fat,@fiber) RETURNING Id";
            insert.AddParameter("@name", food.Name.Trim());
            insert.AddParameter("@unit", food.Unit);
            insert.AddParameter("@kcal", food.Kcal);
            insert.AddParameter("@protein", food.Protein);
            insert.AddParameter("@carbs", food.Carbs);
            insert.AddParameter("@fat", food.Fat);
            insert.AddParameter("@fiber", food.Fiber, DbType.Double);
            var foodId = (long)insert.ExecuteScalar()!;

            using var mapping = connection.CreateCommand();
            mapping.Transaction = transaction;
            mapping.CommandText = "INSERT INTO MobileFoodImports(MobileId,FoodId) VALUES(@id,@foodId)";
            mapping.AddParameter("@id", food.Id.ToString("D"));
            mapping.AddParameter("@foodId", foodId);
            mapping.ExecuteNonQuery();
            imported++;
            acknowledgedIds.Add(food.Id);
        }

        transaction.Commit();
        return new MobileFoodImportResult(imported, skipped, foods.Count, acknowledgedIds);
    }
}
