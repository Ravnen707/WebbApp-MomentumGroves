using MomentumGroves.Data;
using MomentumGroves.Repositories;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MomentumGroves.Service;

public sealed partial class MomentumDataService
{
    private readonly ILogger<MomentumDataService> _logger;
    private readonly MomentumDataRepository _repository;

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

    /// <summary>
    /// Initializes application state operations and prepares the configured state schema.
    /// </summary>
    /// <param name="environment">The environment that supplies the application content root.</param>
    /// <param name="logger">The logger for diagnostics and operation failures.</param>
    public MomentumDataService(IWebHostEnvironment environment, ILogger<MomentumDataService> logger)
        : this(PersonalDatabase.Local(environment), logger)
    {
    }

    /// <summary>
    /// Initializes application state operations and prepares the configured state schema.
    /// </summary>
    /// <param name="database">The configured personal database.</param>
    /// <param name="logger">The logger for diagnostics and operation failures.</param>
    public MomentumDataService(PersonalDatabase database, ILogger<MomentumDataService> logger)
    {
        _repository = new MomentumDataRepository(database);
        _logger = logger;
        try
        {
            database.InitializeState();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not initialize MomentumDataService storage");
            throw;
        }
    }

    /// <summary>
    /// Loads stored application state and normalizes the training document.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The JSON values keyed by their application state keys.</returns>
    public async Task<Dictionary<string, JsonElement>> ReadAllAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _repository.ReadAllAsync(cancellationToken);
            result[TrainingKey] = JsonSerializer.SerializeToElement(NormalizeTraining(
                result.TryGetValue(TrainingKey, out var current) ? JsonNode.Parse(current.GetRawText()) : null,
                result.TryGetValue(LegacyTrainingKey, out var legacy) ? JsonNode.Parse(legacy.GetRawText()) : null));

            return result;
        }
        catch (Exception exception)
            when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _logger.LogError(exception, "Application data operation {Operation} failed", nameof(ReadAllAsync));
            throw;
        }
    }

    /// <summary>
    /// Validates and saves one application state document.
    /// </summary>
    /// <param name="key">The application data key to validate or update.</param>
    /// <param name="value">The supplied value to process.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes after the value is stored.</returns>
    public async Task SaveAsync(string key, JsonElement value, CancellationToken cancellationToken)
    {
        Validate(key, value);
        if (_repository.UsesCloudStorage && (key is TrainingKey or LegacyTrainingKey))
        {
            throw new ArgumentException("Use the training endpoint to save programmes.");
        }
        try
        {
            await _repository.SaveAsync(key, value, cancellationToken);
        }
        catch (Exception exception)
            when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _logger.LogError(exception, "Application data operation {Operation} failed", nameof(SaveAsync));
            throw;
        }
    }

    /// <summary>
    /// Validates the entire batch before inserting missing values.
    /// </summary>
    /// <param name="values">The submitted values keyed by application data key.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The number of previously missing values inserted.</returns>
    public async Task<int> ImportMissingAsync(Dictionary<string, JsonElement> values,
        CancellationToken cancellationToken)
    {
        foreach (var (key, value) in values)
        {
            Validate(key, value);
        }

        try
        {
            return await _repository.ImportMissingAsync(values, cancellationToken);
        }
        catch (Exception exception)
            when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _logger.LogError(exception, "Application data operation {Operation} failed", nameof(ImportMissingAsync));
            throw;
        }
    }

    /// <summary>
    /// Rejects unsupported application data keys and oversized JSON values.
    /// </summary>
    /// <param name="key">The application data key to validate or update.</param>
    /// <param name="value">The supplied value to process.</param>
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
