using Microsoft.Data.Sqlite;

namespace MomentumGroves.Service;

public sealed class FoodService
{
    private readonly string _databasePath;
    private readonly ILogger<FoodService> _logger;
    private readonly Dictionary<string, double> _fiber;

    public FoodService(IWebHostEnvironment environment, ILogger<FoodService> logger)
    {
        _databasePath = Path.Combine(environment.ContentRootPath, "src", "Data", "frida.db");
        _logger = logger;
        var fiberPath = Path.Combine(environment.ContentRootPath, "src", "Data", "frida-fiber.json");
        try
        {
            _fiber = File.Exists(fiberPath)
                ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(fiberPath)) ?? new()
                : new();
            if (!File.Exists(fiberPath))
            {
                _logger.LogWarning("Fiber supplement is missing; searches will return unavailable fiber values");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            _logger.LogWarning(exception, "Could not load fiber supplement; searches will return unavailable fiber values");
            _fiber = new();
        }
    }

    public async Task<FoodSearchResult> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new FoodSearchResult(true, Array.Empty<FoodResult>());
        }

        try
        {
            var results = new List<FoodResult>();
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                // Frida updates replace this file; completed searches must release their file handles.
                Pooling = false
            }.ToString();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                                  SELECT NameDanish, Kcal, Protein, Carbs, Fat
                                  FROM Foods
                                  WHERE instr(lower(NameDanish), lower($query)) > 0
                                     OR instr(lower(NameEnglish), lower($query)) > 0
                                  ORDER BY
                                      CASE
                                          WHEN lower(NameDanish) = lower($query) THEN 0
                                          WHEN lower(NameDanish) LIKE lower($prefix) ESCAPE '\' THEN 1
                                          WHEN instr(lower(NameDanish), lower($query)) > 0 THEN 2
                                          WHEN lower(NameEnglish) LIKE lower($prefix) ESCAPE '\' THEN 3
                                          ELSE 4
                                      END,
                                      length(NameDanish), NameDanish
                                  LIMIT 20;
                                  """;
            command.Parameters.AddWithValue("$query", query.Trim());
            var escapedPrefix = query.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            command.Parameters.AddWithValue("$prefix", escapedPrefix);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new FoodResult(
                    reader.GetString(0),
                    "DTU Frida 6.1",
                    NullableDouble(reader, 1),
                    NullableDouble(reader, 2),
                    NullableDouble(reader, 3),
                    NullableDouble(reader, 4),
                    _fiber.TryGetValue(reader.GetString(0), out var fiber) ? fiber : null));
            }

            return new FoodSearchResult(true, results);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Local Frida SQLite search failed");
            return new FoodSearchResult(false, Array.Empty<FoodResult>(),
                "The local food database could not be read.");
        }
    }

    private static double? NullableDouble(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);
    }
}

public sealed record FoodResult(
    string Name,
    string Source,
    double? Kcal,
    double? Protein,
    double? Carbs,
    double? Fat,
    double? Fiber);

public sealed record FoodSearchResult(bool Success, IReadOnlyList<FoodResult> Results, string? Error = null);
