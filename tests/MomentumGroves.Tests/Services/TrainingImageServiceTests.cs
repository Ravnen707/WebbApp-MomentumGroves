using MomentumGroves.Service;

namespace MomentumGroves.Tests.Services;

[TestFixture]
public class TrainingImageServiceTests : DatabaseServiceTests
{
    private readonly byte[] _png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j6yQAAAAASUVORK5CYII=");
    private TrainingImageService _service = null!;
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        Environment.WebRootPath = Path.Combine(Environment.ContentRootPath, "wwwroot");
        _directory = Path.Combine(Environment.WebRootPath, "TrainingImages");
        _service = new TrainingImageService(Environment);
    }

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

    [Test]
    public void Upload_RejectsNonImageBytes()
    {
        Assert.ThrowsAsync<ArgumentException>(() => _service.SaveAsync("picture.png", "text"u8.ToArray(), default));
        Assert.That(Directory.Exists(_directory), Is.False);
    }

    [Test]
    public void Upload_RejectsFilesOverTheLimit()
    {
        var data = new byte[TrainingImageService.MaxUploadBytes + 1];
        _png.CopyTo(data, 0);

        Assert.ThrowsAsync<ArgumentException>(() => _service.SaveAsync("picture.png", data, default));
        Assert.That(Directory.Exists(_directory), Is.False);
    }

    [Test]
    public void Upload_CancellationLeavesNoPicture()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() =>
            _service.SaveAsync("picture.png", _png, cancellation.Token));
        Assert.That(_service.List(), Is.Empty);
    }
}
