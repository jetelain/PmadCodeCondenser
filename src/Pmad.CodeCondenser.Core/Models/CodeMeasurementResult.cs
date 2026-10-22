namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Result of measuring codebase tokens using HeuristicTokenEstimator.
/// </summary>
public sealed record CodeMeasurementResult
{
    /// <summary>
    /// Aggregated token metrics for production code files.
    /// </summary>
    public CategoryMeasurement Production { get; init; } = new();

    /// <summary>
    /// Aggregated token metrics for test code files.
    /// </summary>
    public CategoryMeasurement Tests { get; init; } = new();

    /// <summary>
    /// Combined total token metrics across all measured files.
    /// </summary>
    public CategoryMeasurement Total { get; init; } = new();

    /// <summary>
    /// Per-file token measurements.
    /// </summary>
    public IReadOnlyList<FileMeasurement> Files { get; init; } = [];

    /// <summary>
    /// Number of auto-generated files skipped during measurement.
    /// </summary>
    public int SkippedGeneratedFilesCount { get; init; }

    /// <summary>
    /// Convenience property: total LLM tokens across all files.
    /// </summary>
    public int TotalTokens => Total.TotalTokens;

    /// <summary>
    /// Convenience property: total code tokens across all files.
    /// </summary>
    public int CodeTokens => Total.CodeTokens;

    /// <summary>
    /// Convenience property: total comment tokens across all files.
    /// </summary>
    public int CommentTokens => Total.CommentTokens;

    /// <summary>
    /// Convenience property: total files measured.
    /// </summary>
    public int FilesCount => Total.FilesCount;
}
