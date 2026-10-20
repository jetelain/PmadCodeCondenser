namespace Pmad.CodeCondenser.Benchmark.Cli;

/// <summary>
/// Parses command line arguments for the benchmark CLI tool.
/// </summary>
public static class BenchmarkCliParser
{
    public static bool TryParse(string[] args, out BenchmarkCliOptions options, out string? errorMessage)
    {
        options = new BenchmarkCliOptions();
        errorMessage = null;

        if (args.Length == 0)
        {
            options.Command = BenchmarkCommand.Help;
            return true;
        }

        int index = 0;
        var firstArg = args[0].Trim();

        if (firstArg.StartsWith("-"))
        {
            options.Command = BenchmarkCommand.All;
        }
        else
        {
            switch (firstArg.ToLowerInvariant())
            {
                case "extract":
                    options.Command = BenchmarkCommand.Extract;
                    index = 1;
                    break;
                case "run":
                case "bench":
                case "benchmark":
                    options.Command = BenchmarkCommand.Run;
                    index = 1;
                    break;
                case "judge":
                    options.Command = BenchmarkCommand.Judge;
                    index = 1;
                    break;
                case "report":
                    options.Command = BenchmarkCommand.Report;
                    index = 1;
                    break;
                case "all":
                    options.Command = BenchmarkCommand.All;
                    index = 1;
                    break;
                case "help":
                    options.Help = true;
                    options.Command = BenchmarkCommand.Help;
                    return true;
                default:
                    errorMessage = $"Unknown command: '{firstArg}'. Available commands: extract, run, judge, report, all.";
                    return false;
            }
        }

        for (; index < args.Length; index++)
        {
            var arg = args[index];

            if (arg.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-?", StringComparison.OrdinalIgnoreCase))
            {
                options.Help = true;
                return true;
            }

            if (arg.Equals("--no-color", StringComparison.OrdinalIgnoreCase))
            {
                options.NoColor = true;
                continue;
            }

            if (arg.Equals("--skip-judge", StringComparison.OrdinalIgnoreCase))
            {
                options.SkipJudge = true;
                continue;
            }

            if (arg.Equals("--resume", StringComparison.OrdinalIgnoreCase))
            {
                options.Resume = true;
                continue;
            }

            if (arg.Equals("-p", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--projects", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetNextValue(args, ref index, out var val))
                {
                    errorMessage = $"Missing value for {arg}.";
                    return false;
                }
                options.Projects = val.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                continue;
            }

            if (arg.Equals("-c", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--candidates", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetNextValue(args, ref index, out var val))
                {
                    errorMessage = $"Missing value for {arg}.";
                    return false;
                }
                options.CandidateModels = val.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                continue;
            }

            if (arg.Equals("-j", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--judge", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetNextValue(args, ref index, out var val))
                {
                    errorMessage = $"Missing value for {arg}.";
                    return false;
                }
                options.JudgeModel = val;
                continue;
            }

            if (arg.Equals("-e", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--endpoint", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetNextValue(args, ref index, out var val))
                {
                    errorMessage = $"Missing value for {arg}.";
                    return false;
                }
                options.Endpoint = val;
                continue;
            }

            if (arg.Equals("-d", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--dataset", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetNextValue(args, ref index, out var val))
                {
                    errorMessage = $"Missing value for {arg}.";
                    return false;
                }
                options.DatasetPath = val;
                continue;
            }

            if (arg.Equals("-o", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--output", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--output-dir", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetNextValue(args, ref index, out var val))
                {
                    errorMessage = $"Missing value for {arg}.";
                    return false;
                }
                options.OutputDirectory = val;
                continue;
            }

            if (arg.Equals("-r", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--results", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetNextValue(args, ref index, out var val))
                {
                    errorMessage = $"Missing value for {arg}.";
                    return false;
                }
                options.ResultsPath = val;
                continue;
            }

            if (arg.Equals("-s", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--sample", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetNextValue(args, ref index, out var val) || !int.TryParse(val, out var sample) || sample < 1)
                {
                    errorMessage = $"Invalid sample count for {arg}. Expected positive integer.";
                    return false;
                }
                options.SampleSize = sample;
                continue;
            }

            if (arg.Equals("-l", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--limit", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetNextValue(args, ref index, out var val) || !int.TryParse(val, out var limit) || limit < 1)
                {
                    errorMessage = $"Invalid limit count for {arg}. Expected positive integer.";
                    return false;
                }
                options.Limit = limit;
                continue;
            }

            if (arg.Equals("-tc", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--threshold-comments", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetNextValue(args, ref index, out var val) || !int.TryParse(val, out var tc) || tc < 1)
                {
                    errorMessage = $"Invalid threshold for {arg}. Expected positive integer.";
                    return false;
                }
                options.CommentLengthThreshold = tc;
                continue;
            }

            if (arg.Equals("--context-size", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--num-ctx", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetNextValue(args, ref index, out var val) || !int.TryParse(val, out var ctx) || ctx < 512)
                {
                    errorMessage = $"Invalid context size for {arg}. Expected integer >= 512 (e.g. 16384 or 24576).";
                    return false;
                }
                options.ContextSize = ctx;
                continue;
            }

            errorMessage = $"Unrecognized option '{arg}'. Use --help for usage details.";
            return false;
        }

        return true;
    }

    private static bool TryGetNextValue(string[] args, ref int index, out string value)
    {
        if (index + 1 < args.Length && !args[index + 1].StartsWith("-"))
        {
            index++;
            value = args[index];
            return true;
        }
        value = string.Empty;
        return false;
    }

    public static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine("""
            Pmad.CodeCondenser.Benchmark - Comment Summarization Model Benchmark & Evaluation CLI

            Usage:
              pmad-bench <command> [options]

            Commands:
              extract                     Extract and curate verbose comments from reference projects
              run | bench                 Run candidate models and judge evaluations on comments
              judge                       Evaluate an existing run's summaries with the LLM judge
              report                      Generate console summary and Markdown report from existing run results
              all                         Extract comments, run candidate benchmarks, judge, and generate report (default)

            Options:
              -p, --projects <dirs>       Comma-separated list of reference project paths to scan
              -c, --candidates <models>   Comma-separated list of candidate models (default: gemma4:e4b,qwen3.5:4b,qwen3.5:2b,Qwen3.5-4B:latest,gemma4:e2b)
              -j, --judge <model>         LLM-as-a-judge model (default: Qwen3.6-35B-A3B:latest)
              -e, --endpoint <url>        Ollama endpoint URL (default: from OLLAMA_HOST or http://localhost:11434)
              -d, --dataset <path>        Path to comments dataset JSON (default: benchmark-comments.json)
              -o, --output-dir <path>     Directory for results, reports, and summary (default: benchmark-output)
              -r, --results <path>        Path to benchmark-run.json (for judge or report commands)
              -s, --sample <n>            Stratified sample size across projects and comment kinds (default: all or 25)
              -l, --limit <n>             Limit benchmark run to first N comments in dataset
              -tc, --threshold-comments <n> Minimum character length for verbose comments (default: 100)
              --skip-judge                Generate candidate summaries without running judge evaluation
              --resume                    Resume benchmark run from existing partial results
              --no-color                  Disable ANSI color formatting
              -h, --help                  Show this help message
            """);
    }
}
