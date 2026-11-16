using Microsoft.Data.Sqlite;

namespace MomentumGroves.Service;

/// <summary>
/// Reads and stores training exercise images in SQLite.
/// </summary>
public sealed class TrainingImageService
{
    private readonly ILogger<TrainingImageService> _logger;
    private readonly string _connectionString;

    /// <summary>
    /// Initializes the training image storage connection.
    /// </summary>
    /// <param name="environment">The hosting environment.</param>
    public TrainingImageService(IWebHostEnvironment environment, ILogger<TrainingImageService> logger)
    {
        _logger = logger;
        var path = Path.Combine(environment.ContentRootPath, "src", "Data", "momentum.db");
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
    }

    /// <summary>
    /// Retrieves an exercise image from the existing database.
    /// </summary>
    /// <param name="key">The exercise image key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The image data and content type, or null if not found.</returns>
    public async Task<(byte[] Data, string Type)?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT ImageData, ContentType FROM TrainingImages WHERE ExerciseKey = $key";
            command.Parameters.AddWithValue("$key", key);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return ((byte[])reader[0], reader.GetString(1));
            }

            return null;
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _logger.LogError(exception, "Training image operation {Operation} failed", nameof(ReadAsync));
            throw;
        }
    }

    /// <summary>
    /// Creates or updates an exercise image in the existing database.
    /// </summary>
    /// <param name="key">The exercise image key.</param>
    /// <param name="type">The image content type.</param>
    /// <param name="data">The image bytes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    public async Task SaveAsync(string key, string type, byte[] data, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100 || data is null || data.Length is 0 or > 8_000_000 ||
            type is not ("image/jpeg" or "image/png" or "image/webp"))
        {
            _logger.LogWarning("Training image save rejected: invalid key, content type, or image size");
            throw new ArgumentException("Invalid training image.");
        }

        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO TrainingImages (ExerciseKey, ContentType, ImageData) VALUES ($key, $type, $data) ON CONFLICT(ExerciseKey) DO UPDATE SET ContentType = excluded.ContentType, ImageData = excluded.ImageData";
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$type", type);
            command.Parameters.AddWithValue("$data", data);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _logger.LogError(exception, "Training image operation {Operation} failed", nameof(SaveAsync));
            throw;
        }
    }
}
