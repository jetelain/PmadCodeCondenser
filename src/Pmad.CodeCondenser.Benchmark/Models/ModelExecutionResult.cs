using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Benchmark.Models;

/// <summary>
/// Result of a single candidate model's summarization on a benchmark comment.
/// </summary>
public sealed class ModelExecutionResult
{
    public string CommentId { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public CommentKind Kind { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public int OriginalLength { get; set; }
    public string OriginalText { get; set; } = string.Empty;
    public string RawResponse { get; set; } = string.Empty;
    public string CleanedSummary { get; set; } = string.Empty;
    public int SummaryLength { get; set; }
    public double CompressionRatio { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public double TokensPerSecond { get; set; }
    public bool IsValidFormat { get; set; }
    public string? FormatError { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public JudgeEvaluation? Evaluation { get; set; }
}
