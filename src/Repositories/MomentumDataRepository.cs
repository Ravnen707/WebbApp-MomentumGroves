using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using MomentumGroves.Data;

namespace MomentumGroves.Repositories;

/// <summary>
/// Persists application state and coordinates atomic training updates.
/// </summary>
/// <param name="database">The personal database for this application instance.</param>
internal sealed class MomentumDataRepository(PersonalDatabase database)
{
    internal const string TrainingKey = "sunhed.training-programs.v3";
    internal const string LegacyTrainingKey = "sunhed.training-programs.v2";

    internal bool UsesCloudStorage => database.UsesPostgres;

    /// <summary>
    /// Reads the stored application values.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the storage operation.</param>
    /// <returns>The stored values keyed by application data key.</returns>
    internal async Task<Dictionary<string, JsonElement>> ReadAllAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        await using var connection = database.CreateConnection(PersonalDatabase.StateFile);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, JsonValue FROM AppState";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetString(0)] = JsonSerializer.Deserialize<JsonElement>(reader.GetString(1));
        }

        return result;
    }

    /// <summary>
    /// Inserts or replaces a stored application value.
    /// </summary>
    /// <param name="key">The application data key.</param>
    /// <param name="value">The JSON document to store.</param>
    /// <param name="cancellationToken">The token used to cancel the storage operation.</param>
    /// <returns>A task that completes after the value is written.</returns>
    internal async Task SaveAsync(string key, JsonElement value, CancellationToken cancellationToken)
    {
        await using var connection = database.CreateConnection(PersonalDatabase.StateFile);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
        INSERT INTO AppState (Key, JsonValue, UpdatedUtc)
        VALUES (@key, @json, @updated)
        ON CONFLICT(Key) DO UPDATE SET JsonValue = excluded.JsonValue, UpdatedUtc = excluded.UpdatedUtc
        """;
        command.AddParameter("@key", key);
        command.AddParameter("@json", value.GetRawText());
        command.AddParameter("@updated", PersonalDatabase.UtcNow());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Inserts missing values in one transaction.
    /// </summary>
    /// <param name="values">The submitted documents keyed by application data key.</param>
    /// <param name="cancellationToken">The token used to cancel the storage operation.</param>
    /// <returns>The number of values inserted.</returns>
    internal async Task<int> ImportMissingAsync(Dictionary<string, JsonElement> values,
        CancellationToken cancellationToken)
    {
        await using var connection = database.CreateConnection(PersonalDatabase.StateFile);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await PersonalDatabase.BeginStateAsync(connection, true, cancellationToken);
        var count = 0;
        foreach (var (key, value) in values)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "INSERT INTO AppState (Key, JsonValue, UpdatedUtc) VALUES (@key, @json, @updated) ON CONFLICT(Key) " +
                    "DO NOTHING";
            command.AddParameter("@key", key);
            command.AddParameter("@json", value.GetRawText());
            command.AddParameter("@updated", PersonalDatabase.UtcNow());
            count += await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return count;
    }

    /// <summary>
    /// Reads the current training document, falling back to the legacy document when needed.
    /// </summary>
    /// <param name="token">The token used to cancel the database read.</param>
    /// <returns>The stored training JSON, or null if neither document exists.</returns>
    internal async Task<string?> ReadCurrentTrainingJsonAsync(CancellationToken token)
    {
        await using var connection = database.CreateConnection(PersonalDatabase.StateFile);
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT JsonValue FROM AppState
                              WHERE Key IN (@current, @legacy)
                              ORDER BY CASE WHEN Key = @current THEN 0 ELSE 1 END
                              LIMIT 1
                              """;
        command.AddParameter("@current", TrainingKey);
        command.AddParameter("@legacy", LegacyTrainingKey);
        return await command.ExecuteScalarAsync(token) as string;
    }

    /// <summary>
    /// Reads the training snapshot and commits an optional replacement in the same transaction.
    /// </summary>
    /// <typeparam name="T">The result produced by the training operation.</typeparam>
    /// <param name="write">Whether the operation can change training state.</param>
    /// <param name="apply">Applies training rules and returns the result and any replacement document.</param>
    /// <param name="token">The token used to cancel the transaction.</param>
    /// <returns>The result after the transaction commits.</returns>
    internal async Task<T> WithTrainingAsync<T>(bool write,
        Func<JsonNode?, JsonNode?, (T Result, JsonObject? Replacement)> apply, CancellationToken token)
    {
        await using var connection = database.CreateConnection(PersonalDatabase.StateFile);
        await connection.OpenAsync(token);
        await using var transaction = await PersonalDatabase.BeginStateAsync(connection, write, token);
        var (current, legacy) = await ReadTrainingAsync(connection, transaction, token);
        var (result, replacement) = apply(current, legacy);
        if (replacement is not null)
        {
            await StoreTrainingAsync(connection, transaction, replacement, token);
        }

        await transaction.CommitAsync(token);
        return result;
    }

    /// <summary>
    /// Loads the current and legacy training documents within the supplied transaction.
    /// </summary>
    /// <param name="connection">The open application state connection.</param>
    /// <param name="transaction">The active state transaction.</param>
    /// <param name="token">The token used to cancel the database read.</param>
    /// <returns>The current and legacy stored documents.</returns>
    private static async Task<(JsonNode? Current, JsonNode? Legacy)> ReadTrainingAsync(DbConnection connection,
        DbTransaction transaction, CancellationToken token)
    {
        using var command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = "SELECT Key, JsonValue FROM AppState WHERE Key IN (@current, @legacy)";
        command.AddParameter("@current", TrainingKey);
        command.AddParameter("@legacy", LegacyTrainingKey);

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

        return (current, legacy);
    }

    /// <summary>
    /// Inserts or replaces the current training document within the supplied transaction.
    /// </summary>
    /// <param name="connection">The open application state connection.</param>
    /// <param name="transaction">The active state transaction.</param>
    /// <param name="programmes">The current training document to persist.</param>
    /// <param name="token">The token used to cancel the database write.</param>
    /// <returns>A task that completes when the database finishes the write.</returns>
    private static async Task StoreTrainingAsync(DbConnection connection, DbTransaction transaction,
        JsonObject programmes, CancellationToken token)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              INSERT INTO AppState (Key, JsonValue, UpdatedUtc) VALUES (@key, @json, @updated)
                              ON CONFLICT(Key) DO UPDATE SET JsonValue = excluded.JsonValue,
                                  UpdatedUtc = excluded.UpdatedUtc
                              """;
        command.AddParameter("@key", TrainingKey);
        command.AddParameter("@json", programmes.ToJsonString());
        command.AddParameter("@updated", PersonalDatabase.UtcNow());
        await command.ExecuteNonQueryAsync(token);
    }

}
