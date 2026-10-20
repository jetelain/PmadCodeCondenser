namespace Pmad.CodeCondenser.Core.Snapshots;

/// <summary>
/// Abstraction for persisting and retrieving analysis snapshots.
/// </summary>
public interface ISnapshotStore
{
    Task<AnalysisSnapshot?> LoadAsync(string filePath, CancellationToken cancellationToken = default);

    Task SaveAsync(string filePath, AnalysisSnapshot snapshot, CancellationToken cancellationToken = default);
}
