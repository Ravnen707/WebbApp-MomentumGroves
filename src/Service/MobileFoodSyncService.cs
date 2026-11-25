using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MomentumGroves.Helper;
using MomentumGroves.Models;
using MomentumGroves.Repositories;

namespace MomentumGroves.Service;

/// <summary>
/// Validates and coordinates imports from the private mobile application.
/// </summary>
public sealed class MobileFoodSyncService
{
    private readonly ILogger<MobileFoodSyncService> _logger;
    private readonly IConfiguration _configuration;
    private readonly MobileFoodSyncRepository _repository;

    /// <summary>
    /// Initializes authorization, validation, and logging for mobile food imports.
    /// </summary>
    /// <param name="configuration">The settings that supply the mobile synchronization key.</param>
    /// <param name="repository">The repository that persists mobile food imports.</param>
    /// <param name="logger">The logger for diagnostics and operation failures.</param>
    public MobileFoodSyncService(IConfiguration configuration, MobileFoodSyncRepository repository,
        ILogger<MobileFoodSyncService> logger)
    {
        _logger = logger;
        _configuration = configuration;
        _repository = repository;
    }

    /// <summary>
    /// Checks the mobile synchronization key against the configured key.
    /// </summary>
    /// <param name="suppliedKey">The synchronization key supplied in the request.</param>
    /// <returns>True when the supplied key matches the configured synchronization key.</returns>
    public bool IsAuthorized(string suppliedKey)
    {
        var expectedKey = _configuration["MobileSync:Key"];
        if (string.IsNullOrWhiteSpace(expectedKey) || expectedKey.Length < 16)
        {
            _logger.LogWarning("Mobile food sync is unavailable because its key is missing or too short");
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        if (string.IsNullOrEmpty(suppliedKey))
        {
            _logger.LogWarning("Mobile food sync authorization rejected: no key supplied");
            return false;
        }

        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedKey);
        var authorized = expectedBytes.Length == suppliedBytes.Length && CryptographicOperations.FixedTimeEquals(
            expectedBytes, suppliedBytes);
        if (!authorized)
        {
            _logger.LogWarning("Mobile food sync authorization rejected: invalid key");
        }

        return authorized;
    }

    /// <summary>
    /// Deserializes a food batch while accepting either decimal separator in numeric strings.
    /// </summary>
    /// <param name="payload">The raw JSON food import batch.</param>
    /// <param name="foods">Receives the deserialized food batch when the payload has a supported shape.</param>
    /// <returns>True when the payload could be interpreted as a food batch.</returns>
    public bool TryDeserializeBatch(JsonElement payload, out List<FoodSyncDto>? foods)
    {
        foods = null;
        try
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            options.Converters.Add(new FlexibleDecimalJsonConverter());
            foods = payload.Deserialize<List<FoodSyncDto>>(options);
            return true;
        }
        catch (JsonException)
        {
            _logger.LogWarning("Mobile food batch rejected: invalid JSON structure or numeric values");
            return false;
        }
    }

    /// <summary>
    /// Checks batch size and validates each submitted mobile food.
    /// </summary>
    /// <param name="foods">The foods submitted for import.</param>
    /// <returns>True when the batch size and all food details are valid.</returns>
    public bool IsValidBatch(List<FoodSyncDto>? foods)
    {
        if (foods == null || foods.Count > 500)
        {
            _logger.LogWarning("Mobile food batch rejected: missing batch or more than 500 items");
            return false;
        }

        foreach (var food in foods)
        {
            if (food == null || food.Id == Guid.Empty || string.IsNullOrWhiteSpace(food.Name) ||
                food.Name.Trim().Length > 200 || food.Unit is not ("g" or "ml"))
            {
                _logger.LogWarning("Mobile food batch rejected: invalid item identity, name, or unit");
                return false;
            }

            if (new[]
                {
                    food.Kcal, food.Protein, food.Carbs, food.Fat, food.Fiber ?? 0
                }.Any(value => !double.IsFinite(value) || value < 0))
            {
                _logger.LogWarning("Mobile food batch rejected: nutrition values must be finite and non-negative");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Imports a validated mobile food batch and logs storage failures.
    /// </summary>
    /// <param name="foods">The foods submitted for import.</param>
    /// <returns>The counts and acknowledged IDs from the import.</returns>
    public MobileFoodImportResult Import(List<FoodSyncDto> foods)
    {
        try
        {
            return _repository.Import(foods);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Mobile food sync operation {Operation} failed", nameof(Import));
            throw;
        }
    }
}
