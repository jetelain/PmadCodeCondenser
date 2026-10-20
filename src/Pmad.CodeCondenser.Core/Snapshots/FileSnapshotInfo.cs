namespace Pmad.CodeCondenser.Core.Snapshots;

/// <summary>
/// Snapshot information for an individual analyzed file.
/// </summary>
public sealed class FileSnapshotInfo
{
    public required string FileHash { get; set; }
    public DateTimeOffset LastAnalyzed { get; set; } = DateTimeOffset.UtcNow;
}
