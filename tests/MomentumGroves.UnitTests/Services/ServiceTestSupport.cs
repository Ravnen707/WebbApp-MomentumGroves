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
    /// <summary>
    /// Resolves the requested data file inside the temporary workspace.
    /// </summary>
    /// <param name="name">The name of the temporary database file.</param>
    /// <returns>The absolute path to the test data file.</returns>
    protected string DataPath(string name)
    {
        var path = Path.GetFullPath(Path.Combine(Environment.ContentRootPath, "src", "Data", name));
        if (!path.StartsWith(_workspaceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Test data paths must stay inside the temporary workspace.");
        }
        return path;
    }

    /// <summary>
    /// Allocates a new content root and data directory for the test.
    /// </summary>
    [SetUp]
    public void CreateWorkspace()
    {
        _workspaceRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MomentumGroves-NUnit", Guid.NewGuid().ToString("N")));
        Environment = new TestEnvironment { ContentRootPath = _workspaceRoot };
        Directory.CreateDirectory(DataPath(""));
    }

    /// <summary>
    /// Releases SQLite resources and deletes the test workspace.
    /// </summary>
    [TearDown]
    public void RemoveWorkspace()
    {
        // Pooling is disabled on fixture connections; services use pooled connections.
        SqliteConnection.ClearAllPools();
        var temporaryParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MomentumGroves-NUnit"));
        if (!string.Equals(Path.GetDirectoryName(_workspaceRoot), temporaryParent, StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParseExact(Path.GetFileName(_workspaceRoot), "N", out _))
        {
            throw new InvalidOperationException("Refusing to delete a path outside the temporary test workspace.");
        }
        if (Directory.Exists(_workspaceRoot))
        {
            Directory.Delete(_workspaceRoot, true);
        }
    }

    /// <summary>
    /// Executes a parameterized fixture statement against a temporary database.
    /// </summary>
    /// <param name="database">The temporary database filename.</param>
    /// <param name="sql">The SQL statement used to prepare or inspect the fixture.</param>
    /// <param name="parameters">The values bound to the SQL parameters.</param>
    protected void Sql(string database, string sql, params (string Name, object? Value)[] parameters)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = DataPath(database), Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Creates an isolated Frida database with optional release metadata.
    /// </summary>
    /// <param name="version">The release version used by the case.</param>
    protected void CreateFrida(string? version = null)
    {
        Sql("frida.db",
            "CREATE TABLE Foods (Id INTEGER PRIMARY KEY, NameDanish TEXT, NameEnglish TEXT, Kcal REAL, Protein REAL, Carbs REAL, Fat REAL)");
        if (version is not null)
        {
            Sql("frida.db", "CREATE TABLE FridaMetadata (Version TEXT); INSERT INTO FridaMetadata VALUES ($version)",
                ("$version", version));
        }
    }

    /// <summary>
    /// Parses a test JSON value independently of its document lifetime.
    /// </summary>
    /// <param name="json">The JSON submitted for deserialization or storage.</param>
    /// <returns>A clone of the parsed JSON root.</returns>
    protected static JsonElement Json(string json)
    {
        return JsonDocument.Parse(json).RootElement.Clone();
    }
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
    /// <summary>
    /// Accepts a logging scope without retaining scope state.
    /// </summary>
    /// <typeparam name="TState">The type of logging state supplied by the caller.</typeparam>
    /// <param name="state">The logging state supplied by the code under test.</param>
    /// <returns>Null because the test logger captures individual log entries.</returns>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }
    /// <summary>
    /// Enables capture of every log level in the fixture.
    /// </summary>
    /// <param name="level">The severity of the log entry.</param>
    /// <returns>True for all log levels.</returns>
    public bool IsEnabled(LogLevel level)
    {
        return true;
    }

    /// <summary>
    /// Captures the formatted message and original exception for later assertions.
    /// </summary>
    /// <typeparam name="TState">The type of logging state supplied by the caller.</typeparam>
    /// <param name="level">The severity of the log entry.</param>
    /// <param name="id">The event identifier supplied to the logger.</param>
    /// <param name="state">The logging state supplied by the code under test.</param>
    /// <param name="exception">The original exception passed to the logger.</param>
    /// <param name="formatter">The formatter that converts log state and exception to a message.</param>
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((level, formatter(state, exception), exception));
    }

    /// <summary>
    /// Checks that the one logged error matches the expected exception and operation.
    /// </summary>
    /// <param name="exception">The original exception passed to the logger.</param>
    /// <param name="operation">The operation exercised by the case.</param>
    public void AssertError(Exception exception, string operation)
    {
        var entry = Entries.Single(e => e.Level == LogLevel.Error);
        Assert.That(entry.Exception, Is.SameAs(exception));
        Assert.That(entry.Message, Does.Contain(operation));
    }
}

internal sealed class TestHttp : HttpMessageHandler, IHttpClientFactory
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;
    public List<CapturedRequest> Requests { get; } = new();
    public List<string> ClientNames { get; } = new();

    /// <summary>
    /// Initializes the fake HTTP client handler with a scripted response callback.
    /// </summary>
    /// <param name="respond">The callback that supplies the response for a captured HTTP request.</param>
    public TestHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    /// <summary>
    /// Initializes the fake HTTP client handler with a scripted response callback.
    /// </summary>
    /// <param name="respond">The callback that supplies the response for a captured HTTP request.</param>
    public TestHttp(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : this((request, _) => Task.FromResult(respond(request)))
    {
    }

    /// <summary>
    /// Creates a test HTTP client and records its requested registration name.
    /// </summary>
    /// <param name="name">The requested HTTP client registration name.</param>
    /// <returns>A client backed by the shared test handler.</returns>
    public HttpClient CreateClient(string name)
    {
        ClientNames.Add(name);
        return new HttpClient(this, disposeHandler: false);
    }

    /// <summary>
    /// Records the outgoing HTTP request before invoking the scripted response.
    /// </summary>
    /// <param name="request">The outgoing request to record.</param>
    /// <param name="token">The token used to cancel the test operation.</param>
    /// <returns>The response supplied by the test callback.</returns>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Requests.Add(new CapturedRequest(request.Method, request.RequestUri!,
            request.Content is null ? "" : await request.Content.ReadAsStringAsync(token),
            request.Headers.ToDictionary(h => h.Key, h => string.Join(" ", h.Value))));
        return await _respond(request, token);
    }

    /// <summary>
    /// Builds a disposable HTTP response for a fake provider.
    /// </summary>
    /// <param name="body">The response content sent by the fake provider.</param>
    /// <param name="status">The HTTP status used in the fixture response.</param>
    /// <returns>The response containing the supplied status and body.</returns>
    public static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new(status) { Content = new StringContent(body) };
    }

    /// <summary>
    /// Builds configuration from the test key-value pairs.
    /// </summary>
    /// <param name="values">The configuration entries to supply to the tested component.</param>
    /// <returns>Configuration backed only by the supplied values.</returns>
    public static IConfiguration Configuration(params (string Key, string? Value)[] values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value)).Build();
    }
}
