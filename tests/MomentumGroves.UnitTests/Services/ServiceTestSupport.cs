using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace MomentumGroves.Tests.Services;

// All database fixtures use disposable, isolated databases, never the application's data.
public abstract class DatabaseServiceTests
{
    private string _workspaceRoot = null!;
    protected TestEnvironment Environment { get; private set; } = null!;
    protected string DataPath(string name)
    {
        var path = Path.GetFullPath(Path.Combine(Environment.ContentRootPath, "src", "Data", name));
        if (!path.StartsWith(_workspaceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Test data paths must stay inside the temporary workspace.");
        return path;
    }

    [SetUp]
    public void CreateWorkspace()
    {
        _workspaceRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MomentumGroves-NUnit", Guid.NewGuid().ToString("N")));
        Environment = new TestEnvironment { ContentRootPath = _workspaceRoot };
        Directory.CreateDirectory(DataPath(""));
    }

    [TearDown]
    public void RemoveWorkspace()
    {
        // Pooling is disabled on fixture connections; services use pooled connections.
        SqliteConnection.ClearAllPools();
        var temporaryParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MomentumGroves-NUnit"));
        if (!string.Equals(Path.GetDirectoryName(_workspaceRoot), temporaryParent, StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParseExact(Path.GetFileName(_workspaceRoot), "N", out _))
            throw new InvalidOperationException("Refusing to delete a path outside the temporary test workspace.");
        if (Directory.Exists(_workspaceRoot))
            Directory.Delete(_workspaceRoot, true);
    }

    protected void Sql(string database, string sql, params (string Name, object? Value)[] parameters)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = DataPath(database), Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    protected void CreateFrida(string? version = null)
    {
        Sql("frida.db",
            "CREATE TABLE Foods (Id INTEGER PRIMARY KEY, NameDanish TEXT, NameEnglish TEXT, Kcal REAL, Protein REAL, Carbs REAL, Fat REAL)");
        if (version is not null)
            Sql("frida.db", "CREATE TABLE FridaMetadata (Version TEXT); INSERT INTO FridaMetadata VALUES ($version)",
                ("$version", version));
    }

    protected static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();
}

public sealed class TestEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "MomentumGroves.UnitTests";
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; } = "";
    public string WebRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}

internal sealed class TestLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel level) => true;

    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => Entries.Add((level, formatter(state, exception), exception));

    public void AssertError(Exception exception, string operation)
    {
        var entry = Entries.Single(e => e.Level == LogLevel.Error);
        Assert.That(entry.Exception, Is.SameAs(exception));
        Assert.That(entry.Message, Does.Contain(operation));
    }
}

internal sealed record CapturedRequest(
    HttpMethod Method,
    Uri Uri,
    string Body,
    Dictionary<string, string> Headers);

internal sealed class TestHttp : HttpMessageHandler, IHttpClientFactory
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;
    public List<CapturedRequest> Requests { get; } = new();
    public List<string> ClientNames { get; } = new();

    public TestHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) =>
        _respond = respond;

    public TestHttp(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : this((request, _) => Task.FromResult(respond(request)))
    {
    }

    public HttpClient CreateClient(string name)
    {
        ClientNames.Add(name);
        return new HttpClient(this, disposeHandler: false);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Requests.Add(new CapturedRequest(request.Method, request.RequestUri!,
            request.Content is null ? "" : await request.Content.ReadAsStringAsync(token),
            request.Headers.ToDictionary(h => h.Key, h => string.Join(" ", h.Value))));
        return await _respond(request, token);
    }

    public static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body) };

    public static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value)).Build();
}
