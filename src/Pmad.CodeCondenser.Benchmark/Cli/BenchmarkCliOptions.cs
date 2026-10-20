namespace Pmad.CodeCondenser.Benchmark.Cli;

public enum BenchmarkCommand
{
    Help,
    Extract,
    Run,
    Judge,
    Report,
    All
}

/// <summary>
/// Command-line options for the benchmark CLI tool.
/// </summary>
public sealed class BenchmarkCliOptions
{
    public BenchmarkCommand Command { get; set; } = BenchmarkCommand.All;

    public List<string> Projects { get; set; } = new()
    {
        @"C:\Users\Julien\source\repos\ComicGenCLI",
        @"C:\Users\Julien\source\repos\jetelain\NightCodeForge",
        @"C:\Users\Julien\source\repos\jetelain\HugeImages",
        @"C:\Users\Julien\source\repos\jetelain\Pmad.Git"
    };

    public List<string> CandidateModels { get; set; } = new()
    {
        "gemma4:e4b",
        "qwen3.5:4b",
        "qwen3.5:2b",
        "Qwen3.5-4B:latest",
        "gemma4:e2b"
    };

    public string JudgeModel { get; set; } = "Qwen3.6-35B-A3B:latest";

    public string? Endpoint { get; set; }

    public string DatasetPath { get; set; } = "benchmark-comments.json";

    public string OutputDirectory { get; set; } = "benchmark-output";

    public string ResultsPath { get; set; } = Path.Combine("benchmark-output", "benchmark-run.json");

    public int? SampleSize { get; set; }

    public int? Limit { get; set; }

    public int ContextSize { get; set; } = 24576;

    public int CommentLengthThreshold { get; set; } = 100;

    public bool SkipJudge { get; set; }

    public bool Resume { get; set; }

    public bool NoColor { get; set; }

    public bool Help { get; set; }
}
