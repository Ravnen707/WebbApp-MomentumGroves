using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MomentumGroves.Service;

public sealed partial class MomentumDataService
{
    private readonly ILogger<MomentumDataService> _logger;
    private readonly string _connectionString;

    private static readonly HashSet<string> AllowedKeys = new(StringComparer.Ordinal)
    {
        "sunhed.food.v1", "sunhed.workouts.v1", "sunhed.done.v1",
        "sunhed.training-programs.v1", "sunhed.training-programs.v2", "sunhed.training-programs.v3",
        "sunhed.training-history.v1",
        "sunhed.profile.v1", "sunhed.goal.v1", "sunhed.cheat-days.v1",
        "sunhed.sick-days.v1", "sunhed.weight-history.v1",
        "sunhed.weight-goals.v1", "sunhed.fiber-start.v1",
        "momentum.drinks.recent.v1"
    };

    public MomentumDataService(IWebHostEnvironment environment, ILogger<MomentumDataService> logger)
    {
        _logger = logger;
        try
        {
            var path = Path.Combine(environment.ContentRootPath, "src", "Data", "momentum.db");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                                  CREATE TABLE IF NOT EXISTS AppState (
                                      Key TEXT PRIMARY KEY NOT NULL,
                                      JsonValue TEXT NOT NULL,
                                      UpdatedUtc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
                                  );
                                  """;
            command.ExecuteNonQuery();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not initialize MomentumDataService storage");
            throw;
        }
    }

    public async Task<Dictionary<string, JsonElement>> ReadAllAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT Key, JsonValue FROM AppState";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result[reader.GetString(0)] = JsonSerializer.Deserialize<JsonElement>(reader.GetString(1));
            }

            result[TrainingKey] = JsonSerializer.SerializeToElement(NormalizeTraining(
                result.TryGetValue(TrainingKey, out var current) ? JsonNode.Parse(current.GetRawText()) : null,
                result.TryGetValue(LegacyTrainingKey, out var legacy) ? JsonNode.Parse(legacy.GetRawText()) : null));

            return result;
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _logger.LogError(exception, "Application data operation {Operation} failed", nameof(ReadAllAsync));
            throw;
        }
    }

    public async Task SaveAsync(string key, JsonElement value, CancellationToken cancellationToken)
    {
        Validate(key, value);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                                  INSERT INTO AppState (Key, JsonValue, UpdatedUtc)
                                  VALUES ($key, $json, strftime('%Y-%m-%dT%H:%M:%fZ','now'))
                                  ON CONFLICT(Key) DO UPDATE SET JsonValue = excluded.JsonValue, UpdatedUtc = excluded.UpdatedUtc
                                  """;
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$json", value.GetRawText());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _logger.LogError(exception, "Application data operation {Operation} failed", nameof(SaveAsync));
            throw;
        }
    }

    public async Task<int> ImportMissingAsync(Dictionary<string, JsonElement> values,
        CancellationToken cancellationToken)
    {
        foreach (var (key, value) in values)
        {
            Validate(key, value);
        }

        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var count = 0;
            foreach (var (key, value) in values)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = (SqliteTransaction)transaction;
                command.CommandText = "INSERT OR IGNORE INTO AppState (Key, JsonValue) VALUES ($key, $json)";
                command.Parameters.AddWithValue("$key", key);
                command.Parameters.AddWithValue("$json", value.GetRawText());
                count += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return count;
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _logger.LogError(exception, "Application data operation {Operation} failed", nameof(ImportMissingAsync));
            throw;
        }
    }

    private void Validate(string key, JsonElement value)
    {
        if (!AllowedKeys.Contains(key))
        {
            _logger.LogWarning("Application data write rejected: unknown data key");
            throw new ArgumentException("Unknown data key.", nameof(key));
        }

        if (value.GetRawText().Length > 10_000_000)
        {
            _logger.LogWarning("Application data write rejected: value exceeds the size limit");
            throw new ArgumentException("The data value exceeds the size limit.", nameof(value));
        }
    }
}
