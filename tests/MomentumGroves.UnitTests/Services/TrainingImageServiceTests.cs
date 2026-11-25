using MomentumGroves.Models;
using MomentumGroves.Data.TrainingImages;
using MomentumGroves.Service;

namespace MomentumGroves.Tests.Services;

[TestFixture]
public class TrainingImageServiceTests : DatabaseServiceTests
{
    private readonly byte[] _png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j6yQAAAAASUVORK5CYII=");
    private TrainingImageService _service = null!;
    private string _directory = null!;

    /// <summary>
    /// Creates the temporary training image directory and its service.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        Environment.WebRootPath = Path.Combine(Environment.ContentRootPath, "wwwroot");
        _directory = Path.Combine(Environment.WebRootPath, "TrainingImages");
        _service = new TrainingImageService(Environment);
    }

    /// <summary>
    /// Checks that the gallery lists only files with supported image extensions.
    /// </summary>
    [Test]
    public void List_ReturnsOnlySupportedPicturesInTheFolder()
    {
        Assert.That(_service.List(), Is.Empty);
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "chest press.PNG"), _png);
        File.WriteAllText(Path.Combine(_directory, "notes.txt"), "notes");

        Assert.That(_service.List(), Is.EqualTo(new[]
        {
            new TrainingPicture("chest press", "/TrainingImages/chest%20press.PNG")
        }));
    }

    /// <summary>
    /// Checks that an upload preserves the original bytes and appears in the local gallery.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task Upload_StoresOriginalBytesAndAddsEachUploadToTheGallery()
    {
        var first = await _service.SaveAsync("Chest Press.png", _png, default);
        var second = await _service.SaveAsync("Chest Press.png", _png, default);

        Assert.That(first.Name, Is.EqualTo("Chest Press"));
        Assert.That(first.Url, Is.Not.EqualTo(second.Url));
        Assert.That(_service.List(), Is.EquivalentTo(new[] { first, second }));
        Assert.That(File.ReadAllBytes(Path.Combine(_directory, Path.GetFileName(first.Url))), Is.EqualTo(_png));
    }

    /// <summary>
    /// Checks that the service rejects data without a supported image signature.
    /// </summary>
    [Test]
    public void Upload_RejectsNonImageBytes()
    {
        Assert.ThrowsAsync<ArgumentException>(() => _service.SaveAsync("picture.png", "text"u8.ToArray(), default));
        Assert.That(Directory.Exists(_directory), Is.False);
    }

    /// <summary>
    /// Checks that an upload over the size limit is rejected.
    /// </summary>
    [Test]
    public void Upload_RejectsFilesOverTheLimit()
    {
        var data = new byte[TrainingImageService.MaxUploadBytes + 1];
        _png.CopyTo(data, 0);

        Assert.ThrowsAsync<ArgumentException>(() => _service.SaveAsync("picture.png", data, default));
        Assert.That(Directory.Exists(_directory), Is.False);
    }

    /// <summary>
    /// Checks that a canceled upload leaves no picture in the gallery.
    /// </summary>
    [Test]
    public void Upload_CancellationLeavesNoPicture()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() => _service.SaveAsync("picture.png", _png,
            cancellation.Token));
        Assert.That(_service.List(), Is.Empty);
    }

    /// <summary>
    /// Checks that repeated cloud uploads get unique keys and do not create local files.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task CloudUploadUsesUniqueKeysAndLeavesTheLocalGalleryUntouched()
    {
        var store = new MemoryImageStore();
        var service = new TrainingImageService(Environment, store);
        var first = await service.SaveAsync("Chest Press.png", _png, default);
        var replacement = await service.SaveAsync("Chest Press.png", _png, default);

        Assert.That(first.Url, Does.StartWith("/TrainingImages/cloud-"));
        Assert.That(replacement.Url, Is.Not.EqualTo(first.Url));
        Assert.That(await service.ListAsync(default), Is.EquivalentTo(new[] { first, replacement }));
        Assert.That(Directory.Exists(_directory), Is.False);
    }

    /// <summary>
    /// Checks that copying a picture twice leaves both the stored object and the source file intact.
    /// </summary>
    /// <returns>A task that completes after the assertions finish.</returns>
    [Test]
    public async Task MigrationRetriesDoNotOverwriteCloudObjectsOrRemoveLocalFiles()
    {
        var store = new MemoryImageStore();
        var migration = new TrainingImageMigration(store);
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "bench.png");
        await File.WriteAllBytesAsync(file, _png);

        Assert.That(await migration.CopyAsync(file, false, default), Is.EqualTo("would copy"));
        Assert.That(store.Objects, Is.Empty);
        Assert.That(await migration.CopyAsync(file, true, default), Is.EqualTo("copied"));
        Assert.That(await migration.CopyAsync(file, true, default), Is.EqualTo("already copied"));
        var updated = _png.Concat(new byte[] { 1 }).ToArray();
        await File.WriteAllBytesAsync(file, updated);
        Assert.That(await migration.CopyAsync(file, true, default), Is.EqualTo("conflict"));
        Assert.That(store.Objects["bench.png"].Bytes, Is.EqualTo(_png));
        Assert.That(await File.ReadAllBytesAsync(file), Is.EqualTo(updated));
    }

    private sealed class MemoryImageStore : ITrainingImageStore
    {
        public Dictionary<string, StoredTrainingImage> Objects { get; } = new();

        /// <summary>
        /// Stores a picture in the in-memory fixture and records the write.
        /// </summary>
        /// <param name="name">The name supplied by the case.</param>
        /// <param name="bytes">The image data to store in the fixture.</param>
        /// <param name="mediaType">The content type accompanying the stored image.</param>
        /// <param name="token">The token used to cancel the test operation.</param>
        /// <returns>A completed task after the fake write.</returns>
        public Task PutAsync(string name, byte[] bytes, string mediaType, CancellationToken token)
        {
            Objects.Add(name, new(bytes, mediaType));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Looks up an exercise picture in the in-memory fixture.
        /// </summary>
        /// <param name="name">The name supplied by the case.</param>
        /// <param name="token">The token used to cancel the test operation.</param>
        /// <returns>The matching fixture image or null if missing.</returns>
        public Task<StoredTrainingImage?> ReadAsync(string name, CancellationToken token)
        {
            return Task.FromResult(Objects.GetValueOrDefault(name));
        }

        /// <summary>
        /// Lists picture names stored by the fixture.
        /// </summary>
        /// <param name="token">The token used to cancel the test operation.</param>
        /// <returns>The recorded fixture image filenames.</returns>
        public Task<IReadOnlyList<string>> ListAsync(CancellationToken token)
        {
            return Task.FromResult<IReadOnlyList<string>>(Objects.Keys.ToArray());
        }
    }
}
