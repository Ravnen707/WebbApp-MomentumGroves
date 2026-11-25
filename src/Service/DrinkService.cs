using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using MomentumGroves.Models;

namespace MomentumGroves.Service;

public sealed class DrinkService
{
    private const string SearchUrl = "https://search.openfoodfacts.org/search";

    private static readonly SemaphoreSlim SearchGate = new(1, 1);
    private static DateTimeOffset _lastSearch = DateTimeOffset.MinValue;

    private readonly IHttpClientFactory _clients;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DrinkService> _logger;

    /// <summary>
    /// Initializes drink search with an HTTP client factory, cache, and logger.
    /// </summary>
    /// <param name="clients">The factory for Open Food Facts HTTP clients.</param>
    /// <param name="cache">The cache that holds successful drink searches.</param>
    /// <param name="logger">The logger for diagnostics and operation failures.</param>
    public DrinkService(IHttpClientFactory clients, IMemoryCache cache, ILogger<DrinkService> logger)
    {
        _clients = clients;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Searches and caches matching beverages from Open Food Facts.
    /// </summary>
    /// <param name="query">The name fragment to search for.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The matching drinks or a provider failure.</returns>
    public async Task<DrinkSearchResult> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(query))
        {
            return new DrinkSearchResult(true, Array.Empty<DrinkResult>());
        }

        query = query.Trim();
        var cacheKey = "drinks:search:v6:" + query.ToLowerInvariant();

        if (_cache.TryGetValue(cacheKey, out List<DrinkResult>? cached) && cached is not null)
        {
            return new DrinkSearchResult(true, cached);
        }

        try
        {
            var drinks = await SearchOpenFoodFactsAsync(query, cancellationToken);
            _cache.Set(cacheKey, drinks, TimeSpan.FromHours(8));

            return new DrinkSearchResult(true, drinks);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Drink search failed; returning an unavailable result");
            return new DrinkSearchResult(false, Array.Empty<DrinkResult>(),
                "Open Food Facts is unavailable. Please try again later.");
        }
    }

    /// <summary>
    /// Fetches and parses beverage candidates from Open Food Facts.
    /// </summary>
    /// <param name="query">The name fragment to search for.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The eligible drinks parsed from the provider response.</returns>
    private async Task<List<DrinkResult>> SearchOpenFoodFactsAsync(string query, CancellationToken cancellationToken)
    {
        await SearchGate.WaitAsync(cancellationToken);

        try
        {
            var wait = TimeSpan.FromSeconds(7) - (DateTimeOffset.UtcNow - _lastSearch);

            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken);
            }

            _lastSearch = DateTimeOffset.UtcNow;

            var client = _clients.CreateClient("openfoodfacts");
            var url = SearchUrl + "?q=" + Uri.EscapeDataString(query) + "&page_size=20&langs=da,en";

            using var response = await client.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Open Food Facts returned HTTP {(int)response.StatusCode}.",
                    null, response.StatusCode);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var products = FindProducts(document.RootElement);
            if (products.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException("The search response does not contain a recognized product array.");
            }

            var drinks = new List<DrinkResult>();

            foreach (var item in products.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var product = GetProductObject(item);

                if (product.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var drink = ParseDrink(product);

                if (drink is not null)
                {
                    drinks.Add(drink);
                }
            }

            return drinks.OrderByDescending(x => x.Danish).DistinctBy(x => x.Name,
                StringComparer.OrdinalIgnoreCase).Take(20).ToList();
        }
        finally
        {
            SearchGate.Release();
        }
    }

    /// <summary>
    /// Locates the product array in the provider response.
    /// </summary>
    /// <param name="root">The root of the provider response document.</param>
    /// <returns>The products array, or an undefined element if it is absent.</returns>
    private static JsonElement FindProducts(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        if (root.TryGetProperty("hits", out var hits))
        {
            if (hits.ValueKind == JsonValueKind.Array)
            {
                return hits;
            }

            if (hits.ValueKind == JsonValueKind.Object &&
                hits.TryGetProperty("hits", out var nestedHits) &&
                nestedHits.ValueKind == JsonValueKind.Array)
            {
                return nestedHits;
            }
        }

        if (root.TryGetProperty("products", out var products) && products.ValueKind == JsonValueKind.Array)
        {
            return products;
        }

        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            return results;
        }

        return default;
    }

    /// <summary>
    /// Unwraps a product from its enclosing response item.
    /// </summary>
    /// <param name="item">The array element that may wrap a product.</param>
    /// <returns>The unwrapped product element.</returns>
    private static JsonElement GetProductObject(JsonElement item)
    {
        if (item.TryGetProperty("_source", out var source) && source.ValueKind == JsonValueKind.Object)
        {
            return source;
        }

        if (item.TryGetProperty("source", out source) && source.ValueKind == JsonValueKind.Object)
        {
            return source;
        }

        if (item.TryGetProperty("product", out source) && source.ValueKind == JsonValueKind.Object)
        {
            return source;
        }

        return item;
    }

    /// <summary>
    /// Maps an eligible provider product to a drink with available nutrition values.
    /// </summary>
    /// <param name="product">The JSON object containing the provider product.</param>
    /// <returns>The parsed drink, or null for an unusable product.</returns>
    private static DrinkResult? ParseDrink(JsonElement product)
    {
        var name = GetString(product, "product_name");

        if (string.IsNullOrWhiteSpace(name))
        {
            name = GetString(product, "product_name_da");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = GetString(product, "product_name_en");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var brand = GetBrand(product);

        if (!string.IsNullOrWhiteSpace(brand) && !name.Contains(brand, StringComparison.OrdinalIgnoreCase))
        {
            name = brand + " – " + name;
        }

        if (!product.TryGetProperty("nutriments", out var nutrients) ||
            nutrients.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Værdier pr. 100 g må ikke behandles som værdier pr. 100 ml uden en dokumenteret omregning.
        var kcal = GetNumber(nutrients, "energy-kcal_100ml");
        var protein = GetNumber(nutrients, "proteins_100ml");
        var carbs = GetNumber(nutrients, "carbohydrates_100ml");
        var fat = GetNumber(nutrients, "fat_100ml");

        var nutritionUnit = GetString(product, "nutrition_data_per");
        var approximate = false;

        if (string.Equals(nutritionUnit, "100ml", StringComparison.OrdinalIgnoreCase) || IsBeverage(product))
        {
            approximate = !string.Equals(nutritionUnit, "100ml", StringComparison.OrdinalIgnoreCase) &&
                          ((kcal is null && GetNumber(nutrients, "energy-kcal_100g") is not null) ||
                           (protein is null && GetNumber(nutrients, "proteins_100g") is not null) ||
                           (carbs is null && GetNumber(nutrients, "carbohydrates_100g") is not null) ||
                           (fat is null && GetNumber(nutrients, "fat_100g") is not null));

            kcal ??= GetNumber(nutrients, "energy-kcal_100g");
            protein ??= GetNumber(nutrients, "proteins_100g");
            carbs ??= GetNumber(nutrients, "carbohydrates_100g");
            fat ??= GetNumber(nutrients, "fat_100g");
        }

        if (kcal is null && protein is null && carbs is null && fat is null)
        {
            return null;
        }

        var danish = IsDanish(product);

        var source = danish ? "Open Food Facts (Danmark)" : "Open Food Facts";

        if (approximate)
        {
            source += " (ca. pr. 100 ml, baseret på 100 g)";
        }

        return new DrinkResult(name, source, kcal, protein, carbs, fat, danish);
    }


    /// <summary>
    /// Reads a string property from the supplied JSON object.
    /// </summary>
    /// <param name="element">The JSON object containing the property.</param>
    /// <param name="name">The property name to read.</param>
    /// <returns>The property value, or null when no suitable value exists.</returns>
    private static string? GetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    /// <summary>
    /// Extracts the first usable brand label from the provider product.
    /// </summary>
    /// <param name="product">The JSON object containing the provider product.</param>
    /// <returns>The usable brand name, or null if unknown.</returns>
    private static string? GetBrand(JsonElement product)
    {
        if (!product.TryGetProperty("brands", out var brands))
        {
            return null;
        }

        if (brands.ValueKind == JsonValueKind.String)
        {
            return brands.GetString();
        }

        if (brands.ValueKind == JsonValueKind.Array)
        {
            return brands.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        }

        return null;
    }

    /// <summary>
    /// Checks the provider categories and serving data for beverage indicators.
    /// </summary>
    /// <param name="product">The JSON object containing the provider product.</param>
    /// <returns>True when the product is identified as a beverage.</returns>
    private static bool IsBeverage(JsonElement product)
    {
        if (!product.TryGetProperty("categories_tags",
                out var categories) || categories.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return categories.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String
                                                    && x.GetString() == "en:beverages");
    }

    /// <summary>
    /// Reads a finite nonnegative nutrition number from a JSON property.
    /// </summary>
    /// <param name="element">The JSON object containing the property.</param>
    /// <param name="name">The nutrition property name to read.</param>
    /// <returns>The valid numeric value, or null when unknown.</returns>
    private static double? GetNumber(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetDouble(out var number) && double.IsFinite(number) && number >= 0 ? number : null;
    }


    /// <summary>
    /// Checks whether the provider product is identified as Danish.
    /// </summary>
    /// <param name="product">The JSON object containing the provider product.</param>
    /// <returns>True when the product is identified as Danish.</returns>
    private static bool IsDanish(JsonElement product)
    {
        return product.TryGetProperty("countries_tags", out var tags) && tags.ValueKind ==
            JsonValueKind.Array && tags.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String &&
                                                                  x.GetString() == "en:denmark");
    }
}