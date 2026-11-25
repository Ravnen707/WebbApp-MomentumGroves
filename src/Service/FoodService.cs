using MomentumGroves.Models;
using MomentumGroves.Repositories;

namespace MomentumGroves.Service;

public sealed class FoodService
{
    private readonly FridaRepository _repository;
    private readonly ILogger<FoodService> _logger;
    private readonly Dictionary<string, double> _fiber;

    /// <summary>
    /// Initializes local Frida search and loads the supplementary fiber values.
    /// </summary>
    /// <param name="environment">The environment that supplies the application content root.</param>
    /// <param name="logger">The logger for diagnostics and operation failures.</param>
    public FoodService(IWebHostEnvironment environment, ILogger<FoodService> logger)
    {
        _repository = new FridaRepository(Path.Combine(environment.ContentRootPath, "src", "Data", "frida.db"));
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

    /// <summary>
    /// Searches the installed Frida food dataset and reports storage failures.
    /// </summary>
    /// <param name="query">The name fragment to search for.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The matching food results or a storage failure.</returns>
    public async Task<FoodSearchResult> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new FoodSearchResult(true, Array.Empty<FoodResult>());
        }

        try
        {
            return new FoodSearchResult(true, await _repository.SearchAsync(query, _fiber, cancellationToken));
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
}
