using System.Text.Json;
using Pmad.CodeCondenser.Benchmark.Cli;
using Pmad.CodeCondenser.Benchmark.Execution;
using Pmad.CodeCondenser.Benchmark.Extraction;
using Pmad.CodeCondenser.Benchmark.Models;
using Pmad.CodeCondenser.Benchmark.Reporting;
using Pmad.CodeCondenser.Core.Configuration;

namespace Pmad.CodeCondenser.Benchmark;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (!BenchmarkCliParser.TryParse(args, out var options, out var errorMessage))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Error: {errorMessage}");
            Console.ResetColor();
            Console.WriteLine();
            BenchmarkCliParser.PrintHelp(Console.Out);
            return 1;
        }

        if (options.Help || options.Command == BenchmarkCommand.Help)
        {
            BenchmarkCliParser.PrintHelp(Console.Out);
            return 0;
        }

        var extractor = new BenchmarkCommentExtractor();
        var reporter = new BenchmarkReporter();

        try
        {
            switch (options.Command)
            {
                case BenchmarkCommand.Extract:
                    await RunExtractAsync(extractor, options).ConfigureAwait(false);
                    return 0;

                case BenchmarkCommand.Run:
                    await RunBenchmarkAsync(extractor, reporter, options).ConfigureAwait(false);
                    return 0;

                case BenchmarkCommand.Judge:
                    await RunJudgeAsync(reporter, options).ConfigureAwait(false);
                    return 0;

                case BenchmarkCommand.Report:
                    await RunReportAsync(reporter, options).ConfigureAwait(false);
                    return 0;

                case BenchmarkCommand.All:
                    await RunAllAsync(extractor, reporter, options).ConfigureAwait(false);
                    return 0;

                default:
                    Console.Error.WriteLine("Unknown command.");
                    return 1;
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nOperation was canceled by user.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"\n[Fatal Error] {ex.Message}");
            Console.ResetColor();
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static async Task<BenchmarkDataset> RunExtractAsync(BenchmarkCommentExtractor extractor, BenchmarkCliOptions options)
    {
        Console.WriteLine("=== Extracting Comments for Benchmark ===");
        Console.WriteLine($"Scanning {options.Projects.Count} reference project(s):");
        foreach (var p in options.Projects)
        {
            Console.WriteLine($"  • {p}");
        }

        var settings = new CodeCondenserSettings
        {
            CommentLengthThreshold = options.CommentLengthThreshold,
            IncludeXmlDocumentation = true
        };

        var dataset = await extractor.ExtractAsync(options.Projects, settings, options.SampleSize).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine($"Extracted {dataset.Comments.Count} distinct verbose comments.");
        Console.WriteLine("Distribution by Project:");
        foreach (var (proj, count) in dataset.ProjectCounts)
        {
            Console.WriteLine($"  - {proj}: {count}");
        }

        Console.WriteLine("Distribution by Kind:");
        foreach (var (kind, count) in dataset.KindCounts)
        {
            Console.WriteLine($"  - {kind}: {count}");
        }

        await extractor.SaveDatasetAsync(dataset, options.DatasetPath).ConfigureAwait(false);
        Console.WriteLine($"\nDataset successfully saved to: {Path.GetFullPath(options.DatasetPath)}");

        return dataset;
    }

    private static async Task RunBenchmarkAsync(
        BenchmarkCommentExtractor extractor,
        BenchmarkReporter reporter,
        BenchmarkCliOptions options)
    {
        BenchmarkDataset dataset;
        if (File.Exists(options.DatasetPath))
        {
            Console.WriteLine($"Loading dataset from: {options.DatasetPath}");
            dataset = await extractor.LoadDatasetAsync(options.DatasetPath).ConfigureAwait(false);
        }
        else
        {
            Console.WriteLine($"Dataset '{options.DatasetPath}' not found. Extracting comments now...");
            dataset = await RunExtractAsync(extractor, options).ConfigureAwait(false);
        }

        using var client = new OllamaBenchmarkClient(options.Endpoint, options.ContextSize);
        var runner = new BenchmarkRunner(client);
        runner.LogMessage += msg => Console.WriteLine(msg);

        var run = await runner.RunAsync(
            dataset: dataset,
            candidateModels: options.CandidateModels,
            judgeModel: options.JudgeModel,
            outputDirectory: options.OutputDirectory,
            skipJudge: options.SkipJudge,
            resume: options.Resume,
            limit: options.Limit).ConfigureAwait(false);

        await GenerateAndDisplayReportsAsync(reporter, run, options).ConfigureAwait(false);
    }

    private static async Task RunJudgeAsync(BenchmarkReporter reporter, BenchmarkCliOptions options)
    {
        var runPath = options.ResultsPath;
        if (!File.Exists(runPath))
        {
            runPath = Path.Combine(options.OutputDirectory, "benchmark-run.json");
        }

        if (!File.Exists(runPath))
        {
            Console.Error.WriteLine($"Results file not found at: {runPath}");
            return;
        }

        using var client = new OllamaBenchmarkClient(options.Endpoint, options.ContextSize);
        var runner = new BenchmarkRunner(client);
        runner.LogMessage += msg => Console.WriteLine(msg);

        var run = await runner.JudgeExistingRunAsync(runPath, options.JudgeModel).ConfigureAwait(false);
        await GenerateAndDisplayReportsAsync(reporter, run, options).ConfigureAwait(false);
    }

    private static async Task RunReportAsync(BenchmarkReporter reporter, BenchmarkCliOptions options)
    {
        var runPath = options.ResultsPath;
        if (!File.Exists(runPath))
        {
            runPath = Path.Combine(options.OutputDirectory, "benchmark-run.json");
        }

        if (!File.Exists(runPath))
        {
            Console.Error.WriteLine($"Results file not found at: {runPath}");
            return;
        }

        await using var stream = File.OpenRead(runPath);
        var run = await JsonSerializer.DeserializeAsync<BenchmarkRun>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }).ConfigureAwait(false);

        if (run == null)
        {
            Console.Error.WriteLine($"Failed to load benchmark run from {runPath}");
            return;
        }

        await GenerateAndDisplayReportsAsync(reporter, run, options).ConfigureAwait(false);
    }

    private static async Task RunAllAsync(
        BenchmarkCommentExtractor extractor,
        BenchmarkReporter reporter,
        BenchmarkCliOptions options)
    {
        BenchmarkDataset dataset;
        if (File.Exists(options.DatasetPath) && !options.SampleSize.HasValue)
        {
            Console.WriteLine($"Found existing dataset: {options.DatasetPath}. Loading...");
            dataset = await extractor.LoadDatasetAsync(options.DatasetPath).ConfigureAwait(false);
        }
        else
        {
            dataset = await RunExtractAsync(extractor, options).ConfigureAwait(false);
        }

        using var client = new OllamaBenchmarkClient(options.Endpoint, options.ContextSize);
        var runner = new BenchmarkRunner(client);
        runner.LogMessage += msg => Console.WriteLine(msg);

        var run = await runner.RunAsync(
            dataset: dataset,
            candidateModels: options.CandidateModels,
            judgeModel: options.JudgeModel,
            outputDirectory: options.OutputDirectory,
            skipJudge: options.SkipJudge,
            resume: options.Resume,
            limit: options.Limit).ConfigureAwait(false);

        await GenerateAndDisplayReportsAsync(reporter, run, options).ConfigureAwait(false);
    }

    private static async Task GenerateAndDisplayReportsAsync(
        BenchmarkReporter reporter,
        BenchmarkRun run,
        BenchmarkCliOptions options)
    {
        var stats = reporter.ComputeStats(run);

        // 1. ANSI Console Report
        reporter.RenderConsoleSummary(Console.Out, run, stats, useColor: !options.NoColor);

        // 2. Markdown Report
        var mdPath = Path.Combine(options.OutputDirectory, "benchmark-report.md");
        await reporter.GenerateMarkdownReportAsync(run, stats, mdPath).ConfigureAwait(false);
        Console.WriteLine($"Markdown report saved to: {Path.GetFullPath(mdPath)}");

        // 3. JSON Summary
        var summaryPath = Path.Combine(options.OutputDirectory, "benchmark-summary.json");
        await reporter.SaveSummaryJsonAsync(stats, summaryPath).ConfigureAwait(false);
        Console.WriteLine($"Summary JSON saved to: {Path.GetFullPath(summaryPath)}");
    }
}
