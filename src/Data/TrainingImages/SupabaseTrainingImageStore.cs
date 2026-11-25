using MomentumGroves.Models;
using System.Net;
using System.Net.Http.Json;

namespace MomentumGroves.Data.TrainingImages;

/// <summary>
/// Initializes the Supabase-backed store for exercise pictures.
/// </summary>
/// <param name="http">The HTTP client used for provider requests.</param>
/// <param name="config">The configuration that supplies the Supabase Storage endpoint and credentials.</param>
public sealed class SupabaseTrainingImageStore(HttpClient http, IConfiguration config) : ITrainingImageStore
{
    private const string Bucket = "momentum-training-images";

    /// <summary>
    /// Stores a picture under the supplied object name.
    /// </summary>
    /// <param name="name">The name used to identify the item.</param>
    /// <param name="bytes">The image bytes to store.</param>
    /// <param name="mediaType">The HTTP media type of the image.</param>
    /// <param name="token">The token used to cancel the operation.</param>
    /// <returns>A task that completes once the picture is stored.</returns>
    public async Task PutAsync(string name, byte[] bytes, string mediaType, CancellationToken token)
    {
        using var request = Create(HttpMethod.Post, $"object/{Bucket}/{Uri.EscapeDataString(name)}");
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new(mediaType);
        request.Headers.Add("x-upsert", "false");
        using var response = await SendAsync(request, token);
        EnsureSuccess(response);
    }

    /// <summary>
    /// Downloads the named Supabase object and handles missing objects.
    /// </summary>
    /// <param name="name">The name used to identify the item.</param>
    /// <param name="token">The token used to cancel the operation.</param>
    /// <returns>The image bytes and media type, or null if the object is missing.</returns>
    public async Task<StoredTrainingImage?> ReadAsync(string name, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            return await DownloadAsync(name, timeout.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TrainingImageStorageException("Supabase exercise image download timed out.");
        }
        catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException)
        {
            throw new TrainingImageStorageException("Supabase returned an invalid exercise image response.");
        }
    }

    /// <summary>
    /// Requests the image bytes and media type from Supabase Storage.
    /// </summary>
    /// <param name="name">The name used to identify the item.</param>
    /// <param name="token">The token used to cancel the operation.</param>
    /// <returns>The downloaded image, or null if the object is missing.</returns>
    private async Task<StoredTrainingImage?> DownloadAsync(string name, CancellationToken token)
    {
        using var request = Create(HttpMethod.Get, $"object/authenticated/{Bucket}/{Uri.EscapeDataString(name)}");
        using var response = await SendAsync(request, token);
        if (response.StatusCode == HttpStatusCode.NotFound ||
            (response.StatusCode == HttpStatusCode.BadRequest && await IsMissingAsync(response, token)))
        {
            return null;
        }

        EnsureSuccess(response);
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not ("image/jpeg" or "image/png" or "image/webp"))
        {
            throw new TrainingImageStorageException("Storage returned an unsupported image type.");
        }

        await response.Content.LoadIntoBufferAsync(Service.TrainingImageService.MaxUploadBytes, token);
        return new(await response.Content.ReadAsByteArrayAsync(token), mediaType);
    }

    /// <summary>
    /// Lists exercise image names from the Supabase bucket.
    /// </summary>
    /// <param name="token">The token used to cancel the operation.</param>
    /// <returns>The available image filenames.</returns>
    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken token)
    {
        var names = new List<string>();
        const int pageSize = 100;
        while (true)
        {
            using var request = Create(HttpMethod.Post, $"object/list/{Bucket}");
            request.Content = JsonContent.Create(new { prefix = "", limit = pageSize, offset = names.Count,
                sortBy = new { column = "name", order = "asc" } });
            using var response = await SendAsync(request, token);
            EnsureSuccess(response);
            var page = await response.Content.ReadFromJsonAsync<List<StorageObjectEntry>>(cancellationToken: token)
                ?? throw new TrainingImageStorageException("Storage returned an empty gallery response.");
            names.AddRange(page.Select(entry => entry.Name));
            if (page.Count < pageSize)
            {
                return names;
            }
        }
    }


    /// <summary>
    /// Creates an authenticated request for the specified Storage operation.
    /// </summary>
    /// <param name="method">The HTTP method for the Storage request.</param>
    /// <param name="path">The relative Storage API path.</param>
    /// <returns>The authenticated request for the Storage API.</returns>
    private HttpRequestMessage Create(HttpMethod method, string path)
    {
        var key = config["TrainingImages:ServerKey"];
        if (!Uri.TryCreate(config["TrainingImages:SupabaseUrl"], UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttps || url.UserInfo.Length != 0 || url.PathAndQuery != "/" ||
            url.Fragment.Length != 0 || string.IsNullOrWhiteSpace(key))
        {
            throw new TrainingImageStorageException("Configure Supabase exercise image Storage on the web host.");
        }

        var request = new HttpRequestMessage(method, new Uri(url, "/storage/v1/" + path));
        request.Headers.Add("apikey", key);
        if (!key.StartsWith("sb_secret_", StringComparison.Ordinal))
        {
            request.Headers.Authorization = new("Bearer", key);
        }
        return request;
    }

    /// <summary>
    /// Sends a Storage request and translates connection and timeout failures.
    /// </summary>
    /// <param name="request">The authenticated Storage request to send.</param>
    /// <param name="token">The token used to cancel the operation.</param>
    /// <returns>The response from the Storage API.</returns>
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        try
        {
            return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        }
        catch (HttpRequestException)
        {
            throw new TrainingImageStorageException("Could not reach Supabase exercise image Storage.");
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TrainingImageStorageException("Supabase exercise image Storage timed out.");
        }
    }

    /// <summary>
    /// Rejects unsuccessful Storage responses.
    /// </summary>
    /// <param name="response">The HTTP response to inspect.</param>
    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new TrainingImageStorageException(
                $"Supabase exercise image Storage returned HTTP {(int)response.StatusCode}.");
        }
    }

    /// <summary>
    /// Checks the Storage error body for a missing object.
    /// </summary>
    /// <param name="response">The HTTP response to inspect.</param>
    /// <param name="token">The token used to cancel the operation.</param>
    /// <returns>True when the Storage response identifies the object as missing.</returns>
    private static async Task<bool> IsMissingAsync(HttpResponseMessage response, CancellationToken token)
    {
        await response.Content.LoadIntoBufferAsync(8192, token);
        using var body = await System.Text.Json.JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(token), cancellationToken: token);
        return body.RootElement.TryGetProperty("statusCode", out var status) && status.ToString() == "404"
            || body.RootElement.TryGetProperty("code", out var code) && code.ToString() == "NoSuchKey";
    }
}
