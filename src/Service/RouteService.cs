using System.Text.Json;
using MomentumGroves.Models;

namespace MomentumGroves.Service;

/// <summary>
/// Requests round trips from OpenRouteService and searches addresses using Nominatim.
/// </summary>
public sealed class RouteService
{
    private readonly ILogger<RouteService> _logger;
    private readonly IHttpClientFactory _clients;
    private readonly IConfiguration _configuration;

    public RouteService(IHttpClientFactory clients, IConfiguration configuration, ILogger<RouteService> logger)
    {
        _logger = logger;
        _clients = clients;
        _configuration = configuration;
    }

    /// <summary>
    /// SearchAsync
    /// </summary>
    /// <param name="query"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<string> SearchAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var client = _clients.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MomentumGroves/1.0 (personal route planner)");
            var url = (_configuration["Routes:NominatimSearchUrl"] ??
                       throw new InvalidOperationException("Routes__NominatimSearchUrl is missing from .env.")) +
                      "?format=json&limit=5&countrycodes=dk&q=" + Uri.EscapeDataString(query);
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _logger.LogError(exception, "Route service operation {Operation} failed", nameof(SearchAsync));
            throw;
        }
    }

    public async Task<IReadOnlyList<RouteCandidate>> GenerateAsync(RouteGenerateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var key = _configuration["OpenRouteService:ApiKey"] ?? Environment.GetEnvironmentVariable("ORS_API_KEY");
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("The ORS API key is missing. Set ORS_API_KEY in .env.");
            }

            var client = _clients.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", key);
            var results = new List<RouteCandidate>();
            var profile = "foot-walking";
            // Different seeds request alternative round trips without saving them.
            foreach (var seed in new[] { 11, 29, 47 })
            {
                var body = new
                {
                    coordinates = new[] { new[] { request.Longitude, request.Latitude } },
                    options = new
                        { round_trip = new { length = (int)Math.Round(request.DistanceKm * 1000), points = 5, seed } },
                    preference = request.Environment == "outside" ? "recommended" : "recommended",
                    instructions = false
                };
                using var response = await client.PostAsJsonAsync(
                    $"{(_configuration["Routes:OrsBaseUrl"] ?? throw new InvalidOperationException("Routes__OrsBaseUrl is missing from .env.")).TrimEnd('/')}/v2/directions/{profile}/geojson",
                    body, cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    if (results.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"ORS could not calculate the route (HTTP {(int)response.StatusCode}). Please try again later.");
                    }

                    _logger.LogWarning("Route provider returned HTTP {StatusCode}; returning {RouteCount} completed routes",
                        (int)response.StatusCode, results.Count);
                    break;
                }

                using var document = JsonDocument.Parse(content);
                var feature = document.RootElement.GetProperty("features")[0];
                var distance =
                    feature.GetProperty("properties").GetProperty("summary").GetProperty("distance").GetDouble() / 1000;
                results.Add(new RouteCandidate(Math.Round(distance, 2), feature.GetProperty("geometry").GetRawText()));
            }

            return results;
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _logger.LogError(exception, "Route service operation {Operation} failed", nameof(GenerateAsync));
            throw;
        }
    }
}
