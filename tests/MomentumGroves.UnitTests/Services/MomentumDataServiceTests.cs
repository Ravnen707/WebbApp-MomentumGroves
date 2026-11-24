using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MomentumGroves.Service;

namespace MomentumGroves.Tests.Services;

[TestFixture]
public class MomentumDataServiceTests : DatabaseServiceTests
{
    private TestLogger<MomentumDataService> _logger = null!;
    private MomentumDataService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _logger = new();
        _service = new(Environment, _logger);
    }

    [Test]
    public async Task ReadAll_NewDatabase_ReturnsOnlyEmptyTraining()
    {
        var values = await _service.ReadAllAsync(default);
        Assert.That(values.Keys, Is.EqualTo(new[] { MomentumDataService.TrainingKey }));
        Assert.That(values[MomentumDataService.TrainingKey].GetProperty("groups").GetArrayLength(), Is.Zero);
    }

    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("{\"name\":\"private\",\"weight\":72.5}")]
    [TestCase("42")]
    [TestCase("true")]
    [TestCase("\"æøå\"")]
    public async Task Save_ReadAll_RoundTripsJson(string json)
    {
        await _service.SaveAsync("sunhed.profile.v1", Json(json), default);
        var values = await _service.ReadAllAsync(default);
        Assert.That(values["sunhed.profile.v1"].GetRawText(), Is.EqualTo(json));
        Assert.That(_logger.Entries, Is.Empty);
    }

    [Test]
    public async Task Save_ExistingKey_ReplacesValue()
    {
        await _service.SaveAsync("sunhed.goal.v1", Json("1"), default);
        await _service.SaveAsync("sunhed.goal.v1", Json("2"), default);
        var result = await _service.ReadAllAsync(default);
        Assert.That(result.Keys, Is.EquivalentTo(new[] { "sunhed.goal.v1", MomentumDataService.TrainingKey }));
        Assert.That(result["sunhed.goal.v1"].GetInt32(), Is.EqualTo(2));
    }

    [TestCase("sunhed.food.v1")]
    [TestCase("sunhed.workouts.v1")]
    [TestCase("sunhed.done.v1")]
    [TestCase("sunhed.training-programs.v1")]
    [TestCase("sunhed.training-programs.v2")]
    [TestCase("sunhed.training-programs.v3")]
    [TestCase("sunhed.training-history.v1")]
    [TestCase("sunhed.profile.v1")]
    [TestCase("sunhed.goal.v1")]
    [TestCase("sunhed.cheat-days.v1")]
    [TestCase("sunhed.sick-days.v1")]
    [TestCase("sunhed.weight-history.v1")]
    [TestCase("sunhed.weight-goals.v1")]
    [TestCase("sunhed.fiber-start.v1")]
    [TestCase("momentum.drinks.recent.v1")]
    public async Task Save_AllSupportedKeys_Accepted(string key)
    {
        await _service.SaveAsync(key, Json("{}"), default);
        Assert.That((await _service.ReadAllAsync(default)).ContainsKey(key), Is.True);
    }

    [TestCase("")]
    [TestCase("unknown")]
    [TestCase("SUNHED.PROFILE.V1")]
    public void Save_UnknownKey_ThrowsValidationError(string key)
    {
        Assert.ThrowsAsync<ArgumentException>(() => _service.SaveAsync(key, Json("{}"), default));
        Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Warning));
    }

    [TestCase(9_999_998, true)]
    [TestCase(9_999_999, false)]
    public async Task Save_JsonSizeBoundary_IncludesStringQuotes(int length, bool accepted)
    {
        var value = Json("\"" + new string('a', length) + "\"");
        if (accepted)
        {
            await _service.SaveAsync("sunhed.profile.v1", value, default);
            Assert.That((await _service.ReadAllAsync(default))["sunhed.profile.v1"].GetString(),
                Has.Length.EqualTo(length));
        }
        else Assert.ThrowsAsync<ArgumentException>(() => _service.SaveAsync("sunhed.profile.v1", value, default));
    }

    [Test]
    public async Task ImportMissing_PreservesExistingValues_AndCountsOnlyNewKeys()
    {
        await _service.SaveAsync("sunhed.profile.v1", Json("1"), default);
        var batch = new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["sunhed.profile.v1"] = Json("2"), ["sunhed.goal.v1"] = Json("3")
        };
        Assert.That(await _service.ImportMissingAsync(batch, default), Is.EqualTo(1));
        Assert.That(await _service.ImportMissingAsync(batch, default), Is.Zero);
        Assert.That((await _service.ReadAllAsync(default))["sunhed.profile.v1"].GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task ImportMissing_EmptyBatch_ReturnsZero() =>
        Assert.That(await _service.ImportMissingAsync(new(), default), Is.Zero);

    [Test]
    public async Task ImportMissing_InvalidLastKey_DoesNotWriteFirstKey()
    {
        Assert.ThrowsAsync<ArgumentException>(() => _service.ImportMissingAsync(new()
        {
            ["sunhed.profile.v1"] = Json("1"), ["invalid"] = Json("2")
        }, default));
        Assert.That((await _service.ReadAllAsync(default)).Keys,
            Is.EqualTo(new[] { MomentumDataService.TrainingKey }));
    }

    [TestCase("read")]
    [TestCase("save")]
    [TestCase("import")]
    public void DatabaseFailure_LogsAndRethrows(string operation)
    {
        Sql("momentum.db", "DROP TABLE AppState");
        var exception = Assert.ThrowsAsync<SqliteException>(() => Execute(operation, default));
        _logger.AssertError(exception!,
            operation == "read" ? "ReadAllAsync" : operation == "save" ? "SaveAsync" : "ImportMissingAsync");
    }

    [TestCase("read")]
    [TestCase("save")]
    [TestCase("import")]
    public void CallerCancellation_PropagatesWithoutErrorLog(string operation)
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.CatchAsync<OperationCanceledException>(() => Execute(operation, cts.Token));
        Assert.That(_logger.Entries, Is.Empty);
    }

    [Test]
    public void ReadAll_CorruptStoredJson_IsLoggedAndRethrown()
    {
        Sql("momentum.db", "INSERT INTO AppState(Key,JsonValue) VALUES ('sunhed.profile.v1','broken')");
        var exception = Assert.ThrowsAsync<System.Text.Json.JsonException>(() => _service.ReadAllAsync(default));
        _logger.AssertError(exception!, "ReadAllAsync");
    }

    [Test]
    public void Constructor_InvalidStoragePath_LogsAndThrows()
    {
        File.WriteAllText(Path.Combine(Environment.ContentRootPath, "blocked"), "file");
        Environment.ContentRootPath = Path.Combine(Environment.ContentRootPath, "blocked", "child");
        var originalRoot = Path.GetDirectoryName(Path.GetDirectoryName(Environment.ContentRootPath))!;
        try
        {
            Assert.Catch<IOException>(() => new MomentumDataService(Environment, _logger));
            Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Error));
        }
        finally
        {
            Environment.ContentRootPath = originalRoot;
        }
    }

    private Task Execute(string operation, CancellationToken token) => operation switch
    {
        "read" => _service.ReadAllAsync(token),
        "save" => _service.SaveAsync("sunhed.profile.v1", Json("{}"), token),
        _ => _service.ImportMissingAsync(new() { ["sunhed.profile.v1"] = Json("{}") }, token)
    };
}
