using System.Net;
using System.Net.Http.Json;

namespace MomentumGroves.Data.TrainingImages;

public sealed class SupabaseTrainingImageStore(HttpClient http, IConfiguration config) : ITrainingImageStore
{
    private const string Bucket = "momentum-training-images";

    public async Task PutAsync(string name, byte[] bytes, string mediaType, CancellationToken token)
    {
        using var request = Create(HttpMethod.Post, $"object/{Bucket}/{Uri.EscapeDataString(name)}");
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new(mediaType);
        request.Headers.Add("x-upsert", "false");
        using var response = await SendAsync(request, token);
        EnsureSuccess(response);
    }

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

    private async Task<StoredTrainingImage?> DownloadAsync(string name, CancellationToken token)
    {
        using var request = Create(HttpMethod.Get, $"object/authenticated/{Bucket}/{Uri.EscapeDataString(name)}");
        using var response = await SendAsync(request, token);
        if (response.StatusCode == HttpStatusCode.NotFound ||
            (response.StatusCode == HttpStatusCode.BadRequest && await IsMissingAsync(response, token)))
            return null;

        EnsureSuccess(response);
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not ("image/jpeg" or "image/png" or "image/webp"))
            throw new TrainingImageStorageException("Storage returned an unsupported image type.");

        await response.Content.LoadIntoBufferAsync(Service.TrainingImageService.MaxUploadBytes, token);
        return new(await response.Content.ReadAsByteArrayAsync(token), mediaType);
    }

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
            var page = await response.Content.ReadFromJsonAsync<List<ObjectEntry>>(cancellationToken: token)
                ?? throw new TrainingImageStorageException("Storage returned an empty gallery response.");
            names.AddRange(page.Select(entry => entry.Name));
            if (page.Count < pageSize)
                return names;
        }
    }

    private sealed record ObjectEntry(string Name);

    private HttpRequestMessage Create(HttpMethod method, string path)
    {
        var key = config["TrainingImages:ServerKey"];
        if (!Uri.TryCreate(config["TrainingImages:SupabaseUrl"], UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttps || url.UserInfo.Length != 0 || url.PathAndQuery != "/" ||
            url.Fragment.Length != 0 || string.IsNullOrWhiteSpace(key))
            throw new TrainingImageStorageException("Configure Supabase exercise image Storage on the web host.");

        var request = new HttpRequestMessage(method, new Uri(url, "/storage/v1/" + path));
        request.Headers.Add("apikey", key);
        if (!key.StartsWith("sb_secret_", StringComparison.Ordinal))
            request.Headers.Authorization = new("Bearer", key);
        return request;
    }

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

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new TrainingImageStorageException($"Supabase exercise image Storage returned HTTP {(int)response.StatusCode}.");
    }

    private static async Task<bool> IsMissingAsync(HttpResponseMessage response, CancellationToken token)
    {
        await response.Content.LoadIntoBufferAsync(8192, token);
        using var body = await System.Text.Json.JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(token), cancellationToken: token);
        return body.RootElement.TryGetProperty("statusCode", out var status) && status.ToString() == "404"
            || body.RootElement.TryGetProperty("code", out var code) && code.ToString() == "NoSuchKey";
    }
}
