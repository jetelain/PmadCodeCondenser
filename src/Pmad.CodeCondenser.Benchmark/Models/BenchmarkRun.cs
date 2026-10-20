namespace Pmad.CodeCondenser.Benchmark.Models;

/// <summary>
/// Container for a complete benchmark run including all evaluations and metadata.
/// </summary>
public sealed class BenchmarkRun
{
    public string RunId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string JudgeModel { get; set; } = string.Empty;
    public List<string> CandidateModels { get; set; } = new();
    public int CommentCount { get; set; }
    public List<ModelExecutionResult> Results { get; set; } = new();
}
