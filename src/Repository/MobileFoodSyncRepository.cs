using Microsoft.Data.Sqlite;
using MomentumGroves.Models;
using MomentumGroves.DTO;

namespace MomentumGroves.Repository;

/// <summary>Persists mobile food imports in the existing desktop food database.</summary>
public sealed class MobileFoodSyncRepository
{
    private readonly IWebHostEnvironment _environment;

    public MobileFoodSyncRepository(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public MobileFoodImportResult Import(List<FoodSyncDto> foods)
    {
        var path = Path.Combine(_environment.ContentRootPath, "src", "Data", "food_and_recipes.db");
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using (var setup = connection.CreateCommand())
        {
            setup.Transaction = transaction;
            setup.CommandText = "CREATE TABLE IF NOT EXISTS MobileFoodImports (MobileId TEXT PRIMARY KEY, FoodId INTEGER NOT NULL REFERENCES CustomFoods(Id))";
            setup.ExecuteNonQuery();
        }

        var imported = 0;
        var skipped = 0;
        var acknowledgedIds = new List<Guid>();
        foreach (var food in foods)
        {
            using var previous = connection.CreateCommand();
            previous.Transaction = transaction;
            previous.CommandText = "SELECT 1 FROM MobileFoodImports WHERE MobileId = $id LIMIT 1";
            previous.Parameters.AddWithValue("$id", food.Id.ToString("D"));
            if (previous.ExecuteScalar() != null)
            {
                skipped++;
                acknowledgedIds.Add(food.Id);
                continue;
            }

            using var duplicate = connection.CreateCommand();
            duplicate.Transaction = transaction;
            duplicate.CommandText = "SELECT 1 FROM CustomFoods WHERE lower(trim(Name)) = lower($name) LIMIT 1";
            duplicate.Parameters.AddWithValue("$name", food.Name.Trim());
            if (duplicate.ExecuteScalar() != null)
            {
                skipped++;
                acknowledgedIds.Add(food.Id);
                continue;
            }

            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO CustomFoods(Name,Unit,Kcal,Protein,Carbs,Fat,Fiber) VALUES($name,$unit,$kcal,$protein,$carbs,$fat,$fiber)";
            insert.Parameters.AddWithValue("$name", food.Name.Trim());
            insert.Parameters.AddWithValue("$unit", food.Unit);
            insert.Parameters.AddWithValue("$kcal", food.Kcal);
            insert.Parameters.AddWithValue("$protein", food.Protein);
            insert.Parameters.AddWithValue("$carbs", food.Carbs);
            insert.Parameters.AddWithValue("$fat", food.Fat);
            insert.Parameters.AddWithValue("$fiber", (object?)food.Fiber ?? DBNull.Value);
            insert.ExecuteNonQuery();

            using var mapping = connection.CreateCommand();
            mapping.Transaction = transaction;
            mapping.CommandText = "INSERT INTO MobileFoodImports(MobileId,FoodId) VALUES($id,last_insert_rowid())";
            mapping.Parameters.AddWithValue("$id", food.Id.ToString("D"));
            mapping.ExecuteNonQuery();
            imported++;
            acknowledgedIds.Add(food.Id);
        }

        transaction.Commit();
        return new MobileFoodImportResult(imported, skipped, foods.Count, acknowledgedIds);
    }
}
