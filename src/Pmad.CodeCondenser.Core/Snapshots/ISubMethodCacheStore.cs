namespace Pmad.CodeCondenser.Core.Snapshots;

/// <summary>
/// Abstraction for persisting and retrieving cached sub-method statement sequence hashes.
/// </summary>
public interface ISubMethodCacheStore
{
    Task<SubMethodCacheModel?> LoadAsync(string filePath, CancellationToken cancellationToken = default);

    Task SaveAsync(string filePath, SubMethodCacheModel cache, CancellationToken cancellationToken = default);
}
