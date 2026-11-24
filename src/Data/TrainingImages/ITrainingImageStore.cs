namespace MomentumGroves.Data.TrainingImages;

public interface ITrainingImageStore
{
    Task PutAsync(string name, byte[] bytes, string mediaType, CancellationToken token);
    Task<StoredTrainingImage?> ReadAsync(string name, CancellationToken token);
    Task<IReadOnlyList<string>> ListAsync(CancellationToken token);
}

public sealed record StoredTrainingImage(byte[] Bytes, string MediaType);

public sealed class TrainingImageStorageException(string message) : Exception(message);
