using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Benchmark.Models;

/// <summary>
/// Aggregated benchmark performance metrics for a single candidate model.
/// </summary>
public sealed class ModelAggregateStats
{
    public string ModelName { get; set; } = string.Empty;
    public int TotalEvaluated { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public double SuccessRate { get; set; }

    public double AverageLatencyMs { get; set; }
    public double MedianLatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public double MinLatencyMs { get; set; }
    public double MaxLatencyMs { get; set; }

    public double AverageTokensPerSecond { get; set; }
    public double AverageCompressionRatio { get; set; }
    public double FormatComplianceRate { get; set; }

    public double AverageOverallScore { get; set; }
    public double AverageAccuracyScore { get; set; }
    public double AverageConcisenessScore { get; set; }
    public double AverageFormatScore { get; set; }

    public Dictionary<CommentKind, CategoryStats> ByCategory { get; set; } = new();
}

/// <summary>
/// Category-specific statistics for a candidate model (e.g. XML documentation vs regular comments).
/// </summary>
public sealed class CategoryStats
{
    public int Count { get; set; }
    public double AverageLatencyMs { get; set; }
    public double AverageCompressionRatio { get; set; }
    public double FormatComplianceRate { get; set; }
    public double AverageOverallScore { get; set; }
    public double AverageAccuracyScore { get; set; }
    public double AverageConcisenessScore { get; set; }
    public double AverageFormatScore { get; set; }
}
