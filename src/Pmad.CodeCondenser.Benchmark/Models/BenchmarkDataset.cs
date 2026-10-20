namespace Pmad.CodeCondenser.Benchmark.Models;

/// <summary>
/// A collection of comments extracted from reference projects.
/// </summary>
public sealed class BenchmarkDataset
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<string> SourceProjects { get; set; } = new();
    public List<BenchmarkComment> Comments { get; set; } = new();
    public Dictionary<string, int> ProjectCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> KindCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
