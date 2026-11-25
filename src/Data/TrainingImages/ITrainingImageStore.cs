using MomentumGroves.Models;
namespace MomentumGroves.Data.TrainingImages;

public interface ITrainingImageStore
{
    /// <summary>
    /// Stores a picture under the supplied object name.
    /// </summary>
    /// <param name="name">The name used to identify the item.</param>
    /// <param name="bytes">The image bytes to store.</param>
    /// <param name="mediaType">The HTTP media type of the image.</param>
    /// <param name="token">The token used to cancel the operation.</param>
    /// <returns>A task that completes once the picture is stored.</returns>
    Task PutAsync(string name, byte[] bytes, string mediaType, CancellationToken token);
    /// <summary>
    /// Reads a picture and its media type from the store.
    /// </summary>
    /// <param name="name">The name used to identify the item.</param>
    /// <param name="token">The token used to cancel the operation.</param>
    /// <returns>The image bytes and media type, or null if the object is missing.</returns>
    Task<StoredTrainingImage?> ReadAsync(string name, CancellationToken token);
    /// <summary>
    /// Lists available exercise image object names.
    /// </summary>
    /// <param name="token">The token used to cancel the operation.</param>
    /// <returns>The available image filenames.</returns>
    Task<IReadOnlyList<string>> ListAsync(CancellationToken token);
}


/// <summary>
/// Initializes an error that describes a failed exercise image storage operation.
/// </summary>
/// <param name="message">The storage failure description safe to expose to the caller.</param>
public sealed class TrainingImageStorageException(string message) : Exception(message);
