using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace MomentumGroves.Service;

public sealed partial class MomentumDataService
{
    public const string TrainingKey = "sunhed.training-programs.v3";
    private const string LegacyTrainingKey = "sunhed.training-programs.v2";

    public sealed record WeightEdit(Guid ChangeId, string ExerciseId, decimal BaseKg, decimal Kg);

    public sealed record WeightResult(Guid ChangeId, string Status);

    public sealed record TrainingSyncResult(JsonObject Programmes, List<WeightResult> Results)
    {
        /// <summary>
        /// Computes a version fingerprint from the serialized training programmes.
        /// </summary>
        /// <returns>The SHA-256 fingerprint as a hexadecimal string.</returns>
        public string Version() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Programmes.ToJsonString())));
    }

    public sealed record TrainingSave(JsonElement Programmes, Dictionary<string, decimal> OriginalWeights);

    /// <summary>
    /// Applies mobile weight edits in one transaction when their original weights match the stored values.
    /// </summary>
    /// <returns>The resulting programmes and the outcome of each edit.</returns>
    public async Task<TrainingSyncResult> SyncTrainingAsync(List<WeightEdit> edits, CancellationToken token)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var transaction = connection.BeginTransaction(deferred: edits.Count == 0);
        var programmes = await ReadTrainingAsync(connection, transaction, token);
        var exercises = ExercisesById(programmes);
        var results = new List<WeightResult>();
        var changed = false;
        foreach (var edit in edits)
        {
            if (!exercises.TryGetValue(edit.ExerciseId, out var exercise) ||
                exercise["type"]?.ToString() == "cardio")
            {
                results.Add(new(edit.ChangeId, "missing"));
                {
                    continue;
                }
            }

            var kg = Weight(exercise);
            if (kg != edit.BaseKg && kg != edit.Kg)
            {
                results.Add(new(edit.ChangeId, "conflict"));
                {
                    continue;
                }
            }

            if (kg != edit.Kg)
            {
                exercise["kg"] = edit.Kg;
                changed = true;
            }

            results.Add(new(edit.ChangeId, "saved"));
        }

        if (changed)
        {
            await StoreTrainingAsync(connection, transaction, programmes, token);
        }

        await transaction.CommitAsync(token);
        return new(programmes, results);
    }

    /// <summary>
    /// Reads the stored exercise weights for the browser to refresh.
    /// </summary>
    /// <returns>The weights in kilograms keyed by exercise ID.</returns>
    public async Task SaveTrainingAsync(TrainingSave save, CancellationToken token)
    {
        Validate(TrainingKey, save.Programmes);
        if (JsonNode.Parse(save.Programmes.GetRawText()) is not JsonObject incoming ||
            incoming["groups"] is not JsonArray || save.OriginalWeights is null)
        {
            throw new ArgumentException("Invalid training programmes.");
        }

        incoming = NormalizeTraining(incoming, null);
        
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var transaction = connection.BeginTransaction();
        
        var current = ExercisesById(await ReadTrainingAsync(connection, transaction, token));
        
        foreach (var (id, exercise) in ExercisesById(incoming))
        {
            // Keep a phone edit when the browser saves a programme without changing that weight.
            if (save.OriginalWeights.TryGetValue(id, out var original) &&
                Weight(exercise) == original && current.TryGetValue(id, out var savedExercise))
            {
                exercise["kg"] = Weight(savedExercise);
            }
        }

        await StoreTrainingAsync(connection, transaction, incoming, token);
        await transaction.CommitAsync(token);
    }

    /// <summary>
    /// Loads the current and legacy training documents within the supplied transaction.
    /// </summary>
    /// <returns>The normalized training document.</returns>
    private static async Task<JsonObject> ReadTrainingAsync(SqliteConnection connection,
        SqliteTransaction transaction, CancellationToken token)
    {
        using var command = connection.CreateCommand();
        
        command.Transaction = transaction;
        command.CommandText = "SELECT Key, JsonValue FROM AppState WHERE Key IN ($current, $legacy)";
        command.Parameters.AddWithValue("$current", TrainingKey);
        command.Parameters.AddWithValue("$legacy", LegacyTrainingKey);
        
        JsonNode? current = null;
        JsonNode? legacy = null;
        
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var value = JsonNode.Parse(reader.GetString(1));
            if (reader.GetString(0) == TrainingKey)
            {
                current = value;
            }
            else
            {
                legacy = value;
            }
        }

        return NormalizeTraining(current, legacy);
    }

    /// <summary>
    /// Inserts or replaces the current training document within the supplied transaction.
    /// </summary>
    /// <returns>A task that completes when SQLite finishes the write.</returns>
    private static async Task StoreTrainingAsync(SqliteConnection connection, SqliteTransaction transaction,
        JsonObject programmes, CancellationToken token)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              INSERT INTO AppState (Key, JsonValue) VALUES ($key, $json)
                              ON CONFLICT(Key) DO UPDATE SET JsonValue = excluded.JsonValue,
                                  UpdatedUtc = strftime('%Y-%m-%dT%H:%M:%fZ','now')
                              """;
        command.Parameters.AddWithValue("$key", TrainingKey);
        command.Parameters.AddWithValue("$json", programmes.ToJsonString());
        await command.ExecuteNonQueryAsync(token);
    }

    /// <summary>
    /// Converts legacy programme groups to the current format and fills in missing exercise IDs.
    /// </summary>
    /// <returns>A training document containing programme groups and stable exercise IDs.</returns>
    private static JsonObject NormalizeTraining(JsonNode? current, JsonNode? legacy)
    {
        var source = (current ?? legacy) as JsonObject;
        if (source?["groups"] is not JsonArray)
        {
            var groups = new JsonArray();
            foreach (var (id, name) in new[]
                     {
                         ("full", "My programmes"), ("custom", "My other programmes")
                     })
            {
                if (source?[id] is JsonArray { Count: > 0 } programmes)
                {
                    groups.Add(new JsonObject
                    {
                        ["id"] = id, ["name"] = name, ["programmes"] = programmes.DeepClone()
                    });
                }
            }

            source = new JsonObject
            {
                ["groups"] = groups, ["dashboardGroupId"] = groups.Count > 0 && 
                                                            groups[0]?["id"]?.ToString() == "full" ? "full" : null
            };
        }

        foreach (var group in ((JsonArray)source["groups"]!).OfType<JsonObject>())
        {
            if (group["programmes"] is not JsonArray programmes)
            {
                continue;
            }

            foreach (var programme in programmes.OfType<JsonObject>())
            {
                if (programme["exercises"] is not JsonArray exercises)
                {
                    continue;
                }

                for (var index = 0; index < exercises.Count; index++)
                {
                    if (exercises[index] is JsonObject exercise &&
                        string.IsNullOrWhiteSpace(exercise["id"]?.ToString()))
                    {
                        exercise["id"] = $"legacy:{group["id"]}:{programme["id"]}:{index}";
                    }
                }
            }
        }

        return source;
    }

    /// <summary>
    /// Indexes training exercises that have an ID.
    /// </summary>
    /// <returns>The first exercise found for each ID, keyed by that ID.</returns>
    private static Dictionary<string, JsonObject> ExercisesById(JsonObject programmes)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var group in ((JsonArray)programmes["groups"]!).OfType<JsonObject>())
        {
            if (group["programmes"] is not JsonArray groupProgrammes)
            {
                continue;
            }

            foreach (var programme in groupProgrammes.OfType<JsonObject>())
            {
                if (programme["exercises"] is not JsonArray exercises)
                {
                    continue;
                }

                foreach (var exercise in exercises.OfType<JsonObject>())
                {
                    if (exercise["id"]?.ToString() is { Length: > 0 } id)
                    {
                        result.TryAdd(id, exercise);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Reads an exercise weight using culture-independent numeric parsing.
    /// </summary>
    /// <returns>The weight in kilograms, or zero if the value is absent or invalid.</returns>
    private static decimal Weight(JsonObject exercise) =>
        decimal.TryParse(exercise["kg"]?.ToString(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var kg) ? kg : 0;
}
