using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MomentumGroves.Repository;
using MomentumGroves.Helper;
using MomentumGroves.Models;
using MomentumGroves.DTO;

namespace MomentumGroves.Service;

/// <summary>
/// Validates and coordinates imports from the private mobile application.
/// </summary>
public sealed class MobileFoodSyncService
{
    private readonly ILogger<MobileFoodSyncService> _logger;
    private readonly IConfiguration _configuration;
    private readonly MobileFoodSyncRepository _repository;

    public MobileFoodSyncService(IConfiguration configuration, MobileFoodSyncRepository repository, ILogger<MobileFoodSyncService> logger)
    {
        _logger = logger;
        _configuration = configuration;
        _repository = repository;
    }

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
                }.Any(value =>
                    !double.IsFinite(value) || value < 0))
            {
                _logger.LogWarning("Mobile food batch rejected: nutrition values must be finite and non-negative");
                return false;
            }
        }

        return true;
    }

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
