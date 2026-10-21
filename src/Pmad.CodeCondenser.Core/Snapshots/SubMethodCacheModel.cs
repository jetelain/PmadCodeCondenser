namespace Pmad.CodeCondenser.Core.Snapshots;

/// <summary>
/// Cache of pre-extracted statement sequence hashes and spans for sub-method clone detection.
/// </summary>
public sealed class SubMethodCacheModel
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public int NormalizerVersion { get; set; } = AnalysisSnapshot.CurrentNormalizerVersion;

    public Dictionary<string, FileSubMethodCacheEntry> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Cached sub-method block sequences for a single file.
/// </summary>
public sealed class FileSubMethodCacheEntry
{
    public required string FileHash { get; set; }

    public required int NormalizerVersion { get; set; }

    public List<SubMethodBlockCacheDto> Blocks { get; set; } = new();
}

/// <summary>
/// Serialized DTO for a single sub-method block.
/// </summary>
public sealed class SubMethodBlockCacheDto
{
    public required string MethodName { get; set; }

    public required string BlockScopeId { get; set; }

    public required string ProjectId { get; set; }

    public int MethodStartLine { get; set; }

    public required ulong[] StatementHashes { get; set; }

    public required int[] StatementStartLines { get; set; }

    public required int[] StatementEndLines { get; set; }
}
