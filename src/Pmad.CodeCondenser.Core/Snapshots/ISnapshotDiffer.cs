using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Snapshots;

/// <summary>
/// Compares workspace state against a snapshot baseline and coordinates incremental diffing.
/// </summary>
public interface ISnapshotDiffer
{
    /// <summary>
    /// Event raised when informational messages (e.g. normalizer upgrades) are emitted during diffing.
    /// </summary>
    event Action<string>? InfoLogged;
    /// <summary>
    /// Diffs the provided file hashes against a baseline snapshot.
    /// </summary>
    FileDiffResult DiffFiles(
        IReadOnlyDictionary<string, string> currentFileHashes,
        AnalysisSnapshot? baseline);

    /// <summary>
    /// Computes file hashes for the specified files and diffs them against a baseline snapshot.
    /// </summary>
    Task<FileDiffResult> DiffFilesAsync(
        string basePath,
        IEnumerable<string> filePaths,
        AnalysisSnapshot? baseline,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Categorizes analyzed methods into new, modified, or unchanged relative to the baseline.
    /// </summary>
    MethodDiffResult DiffMethods(
        IEnumerable<MethodCloneInfo> currentMethods,
        AnalysisSnapshot? baseline);

    /// <summary>
    /// Filters detected code clone groups for incremental reporting.
    /// Only returns groups where at least one clone was introduced or modified since the baseline snapshot.
    /// </summary>
    IReadOnlyList<CodeCloneGroup> FilterNewCloneGroups(
        IEnumerable<CodeCloneGroup> currentGroups,
        AnalysisSnapshot? baseline,
        FileDiffResult? fileDiff = null);

    /// <summary>
    /// Filters verbose comments to retain only those that are new or not yet present in the snapshot's comment cache.
    /// </summary>
    IReadOnlyList<VerboseComment> FilterNewComments(
        IEnumerable<VerboseComment> comments,
        AnalysisSnapshot? baseline);

    /// <summary>
    /// Creates or updates an AnalysisSnapshot by preserving unchanged files/methods and updating modified ones.
    /// </summary>
    AnalysisSnapshot CreateOrUpdateSnapshot(
        AnalysisSnapshot? baseline,
        IReadOnlyDictionary<string, string> currentFileHashes,
        IEnumerable<MethodCloneInfo> analyzedMethods,
        IReadOnlyDictionary<string, string> commentCache);
}
