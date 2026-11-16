using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MomentumGroves.Service;

namespace MomentumGroves.Tests.Services;

[TestFixture]
public class TrainingImageServiceTests : DatabaseServiceTests
{
    private TrainingImageService _service = null!;
    private TestLogger<TrainingImageService> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        Sql("momentum.db",
            "CREATE TABLE TrainingImages (ExerciseKey TEXT PRIMARY KEY, ContentType TEXT NOT NULL, ImageData BLOB NOT NULL)");
        _logger = new();
        _service = new(Environment, _logger);
    }

    [TestCase("image/jpeg")]
    [TestCase("image/png")]
    [TestCase("image/webp")]
    public async Task SaveAndRead_RoundTripBytesAndContentType(string type)
    {
        byte[] bytes = { 0, 1, 127, 255 };
        await _service.SaveAsync("squat", type, bytes, default);
        var result = await _service.ReadAsync("squat", default);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.Data, Is.EqualTo(bytes));
        Assert.That(result.Value.Type, Is.EqualTo(type));
    }

    [Test]
    public async Task Save_ExistingKey_ReplacesBytesAndType()
    {
        await _service.SaveAsync("key", "image/png", new byte[] { 1 }, default);
        await _service.SaveAsync("key", "image/webp", new byte[] { 2, 3 }, default);
        var result = await _service.ReadAsync("key", default);
        Assert.That(result!.Value.Data, Is.EqualTo(new byte[] { 2, 3 }));
        Assert.That(result.Value.Type, Is.EqualTo("image/webp"));
    }

    [TestCase("missing")]
    [TestCase("")]
    [TestCase("' OR 1=1 --")]
    public async Task Read_UnknownKey_ReturnsNull(string key) =>
        Assert.That(await _service.ReadAsync(key, default), Is.Null);

    [TestCase(1)]
    [TestCase(100)]
    public async Task Save_KeyLengthBoundary_Accepted(int length)
    {
        var key = new string('k', length);
        await _service.SaveAsync(key, "image/png", new byte[] { 1 }, default);
        Assert.That(await _service.ReadAsync(key, default), Is.Not.Null);
    }

    [TestCase("")]
    [TestCase("  ")]
    public void Save_MissingKey_Rejects(string key) => AssertInvalid(key, "image/png", new byte[] { 1 });

    [Test]
    public void Save_OverlongKey_Rejects() => AssertInvalid(new string('x', 101), "image/png", new byte[] { 1 });

    [TestCase("")]
    [TestCase("image/gif")]
    [TestCase("IMAGE/PNG")]
    [TestCase("text/plain")]
    public void Save_UnsupportedType_Rejects(string type) => AssertInvalid("key", type, new byte[] { 1 });

    [TestCase(0)]
    [TestCase(8_000_001)]
    public void Save_InvalidByteCount_Rejects(int length) => AssertInvalid("key", "image/png", new byte[length]);

    [Test]
    public void Save_NullBytes_Rejects() => AssertInvalid("key", "image/png", null!);

    [Test]
    public async Task Save_MaximumByteCount_Accepted()
    {
        await _service.SaveAsync("max", "image/png", new byte[8_000_000], default);
        Assert.That((await _service.ReadAsync("max", default))!.Value.Data, Has.Length.EqualTo(8_000_000));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void StorageFailure_LogsAndRethrows(bool save)
    {
        Sql("momentum.db", "DROP TABLE TrainingImages");
        var exception = Assert.ThrowsAsync<SqliteException>(() => Execute(save, default));
        _logger.AssertError(exception!, save ? "SaveAsync" : "ReadAsync");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Cancellation_PropagatesWithoutErrorLogging(bool save)
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.CatchAsync<OperationCanceledException>(() => Execute(save, cts.Token));
        Assert.That(_logger.Entries, Is.Empty);
    }

    private Task Execute(bool save, CancellationToken token) => save
        ? _service.SaveAsync("key", "image/png",
            new byte[] { 1 }, token)
        : _service.ReadAsync("key", token);

    private void AssertInvalid(string key, string type, byte[] bytes)
    {
        Assert.ThrowsAsync<ArgumentException>(() => _service.SaveAsync(key, type, bytes, default));
        Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Warning));
    }
}