namespace Pmad.CodeCondenser.Core.Snapshots;

/// <summary>
/// Persisted snapshot containing file hashes, method clone hashes, and cached comment summarizations.
/// </summary>
public sealed class AnalysisSnapshot
{
    /// <summary>
    /// The current AST normalizer engine version.
    /// </summary>
    public const int CurrentNormalizerVersion = 2;

    public int Version { get; set; } = 1;

    /// <summary>
    /// The AST normalizer engine version used to generate method hashes in this snapshot.
    /// Default snapshots without this property are treated as NormalizerVersion 1.
    /// </summary>
    public int NormalizerVersion { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Map of normalized relative file paths to their file content hashes.
    /// </summary>
    public Dictionary<string, FileSnapshotInfo> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Map of method identifier (FilePath::MethodSignature) to its normalized AST hash.
    /// </summary>
    public Dictionary<string, string> MethodHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// WYSIWYG comment cache: maps SHA256 of original comment to summarized comment text.
    /// Ensures that what was reviewed in dry-run is exactly what gets applied in --apply.
    /// </summary>
    public Dictionary<string, string> CommentCache { get; set; } = new(StringComparer.Ordinal);
}
