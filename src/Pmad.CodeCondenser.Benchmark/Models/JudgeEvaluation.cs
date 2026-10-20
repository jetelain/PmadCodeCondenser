namespace Pmad.CodeCondenser.Benchmark.Models;

/// <summary>
/// Evaluation results from the LLM-as-a-judge for a candidate model's summary.
/// </summary>
public sealed class JudgeEvaluation
{
    public string JudgeModel { get; set; } = string.Empty;
    public double AccuracyScore { get; set; }
    public double ConcisenessScore { get; set; }
    public double FormatComplianceScore { get; set; }
    public double OverallScore { get; set; }
    public string Critique { get; set; } = string.Empty;
    public long ElapsedMilliseconds { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}
