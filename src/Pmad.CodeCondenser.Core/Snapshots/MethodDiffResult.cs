using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Snapshots;

/// <summary>
/// Result of diffing extracted methods against a snapshot baseline.
/// </summary>
public sealed record MethodDiffResult(
    IReadOnlyList<MethodCloneInfo> NewMethods,
    IReadOnlyList<MethodCloneInfo> ModifiedMethods,
    IReadOnlyList<MethodCloneInfo> UnchangedMethods)
{
    /// <summary>
    /// Indicates whether any methods were added or modified compared to the baseline snapshot.
    /// </summary>
    public bool HasChanges => NewMethods.Count > 0 || ModifiedMethods.Count > 0;
}
