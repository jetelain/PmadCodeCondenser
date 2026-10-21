namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Aggregates all results from a CodeCondenser analysis run.
/// </summary>
public sealed class AnalysisResult
{
    public required IReadOnlyList<VerboseComment> VerboseComments { get; init; }
    public required IReadOnlyList<CodeCloneGroup> CloneGroups { get; init; }
    public required IReadOnlyDictionary<string, string> SummarizedComments { get; init; }
    public required int AnalyzedFilesCount { get; init; }
    public required int TotalMethodsScanned { get; init; }
    public int ProductionFilesCount { get; init; }
    public int TestFilesCount { get; init; }
    public int SkippedGeneratedFilesCount { get; init; }
}
