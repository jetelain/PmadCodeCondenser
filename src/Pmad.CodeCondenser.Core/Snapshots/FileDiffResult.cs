namespace Pmad.CodeCondenser.Core.Snapshots;

/// <summary>
/// Result of diffing workspace files against a snapshot baseline.
/// </summary>
public sealed record FileDiffResult(
    IReadOnlyList<string> AddedFiles,
    IReadOnlyList<string> ModifiedFiles,
    IReadOnlyList<string> UnchangedFiles,
    IReadOnlyList<string> DeletedFiles)
{
    /// <summary>
    /// Indicates whether any files were added, modified, or deleted compared to the baseline snapshot.
    /// </summary>
    public bool HasChanges => AddedFiles.Count > 0 || ModifiedFiles.Count > 0 || DeletedFiles.Count > 0;

    /// <summary>
    /// Files that require analysis in incremental mode (added and modified files).
    /// </summary>
    public IReadOnlyList<string> FilesToAnalyze => [.. AddedFiles, .. ModifiedFiles];
}
