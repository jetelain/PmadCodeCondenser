using System.Text.Json;
using Pmad.CodeCondenser.Benchmark.Models;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Benchmark.Execution;

/// <summary>
/// Orchestrates the execution of candidate summarization models and LLM judge evaluations.
/// </summary>
public sealed class BenchmarkRunner
{
    private readonly OllamaBenchmarkClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public event Action<string>? LogMessage;
    public event Action<int, int, string>? ProgressUpdated;

    public BenchmarkRunner(OllamaBenchmarkClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Executes the benchmark across candidate models and the judge.
    /// </summary>
    public async Task<BenchmarkRun> RunAsync(
        BenchmarkDataset dataset,
        IReadOnlyList<string> candidateModels,
        string judgeModel,
        string outputDirectory,
        bool skipJudge = false,
        bool resume = false,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(candidateModels);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);
        var runFilePath = Path.Combine(outputDirectory, "benchmark-run.json");

        BenchmarkRun run;
        if (resume && File.Exists(runFilePath))
        {
            LogMessage?.Invoke($"Resuming benchmark from existing state: {runFilePath}");
            await using var stream = File.OpenRead(runFilePath);
            run = await JsonSerializer.DeserializeAsync<BenchmarkRun>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                  ?? new BenchmarkRun();

            foreach (var m in candidateModels)
            {
                if (!run.CandidateModels.Contains(m, StringComparer.OrdinalIgnoreCase))
                {
                    run.CandidateModels.Add(m);
                }
            }
        }
        else
        {
            run = new BenchmarkRun
            {
                Endpoint = _client.Endpoint,
                JudgeModel = judgeModel,
                CandidateModels = candidateModels.ToList()
            };
        }

        var commentsToBench = dataset.Comments;
        if (limit.HasValue && limit.Value > 0 && limit.Value < commentsToBench.Count)
        {
            commentsToBench = commentsToBench.Take(limit.Value).ToList();
        }
        run.CommentCount = commentsToBench.Count;

        int totalGenerations = candidateModels.Count * commentsToBench.Count;
        int completedGenerations = 0;

        LogMessage?.Invoke($"Starting benchmark: {candidateModels.Count} candidate models across {commentsToBench.Count} comments (Total {totalGenerations} tasks).");
        LogMessage?.Invoke($"Ollama Endpoint: {_client.Endpoint}");

        // 1. Run Candidate Models (Batched model-by-model to prevent Ollama VRAM swapping)
        foreach (var model in candidateModels)
        {
            LogMessage?.Invoke($"\n--- Processing Candidate Model: {model} ---");

            for (int i = 0; i < commentsToBench.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var comment = commentsToBench[i];
                completedGenerations++;

                // Check if already completed in resumed run
                var existing = run.Results.FirstOrDefault(r =>
                    string.Equals(r.ModelName, model, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.CommentId, comment.Id, StringComparison.OrdinalIgnoreCase));

                if (existing != null && existing.Success)
                {
                    LogMessage?.Invoke($"[{completedGenerations}/{totalGenerations}] Model: {model} | Comment: {comment.Id} (Already completed, skipping)");
                    ProgressUpdated?.Invoke(completedGenerations, totalGenerations, $"{model}: {comment.Id}");
                    continue;
                }

                LogMessage?.Invoke($"[{completedGenerations}/{totalGenerations}] Model: {model} | Comment: {comment.Id} ({comment.Kind}, {comment.CharacterCount} chars)...");
                ProgressUpdated?.Invoke(completedGenerations, totalGenerations, $"{model}: {comment.Id}");

                var result = await _client.GenerateSummaryAsync(model, comment, cancellationToken).ConfigureAwait(false);

                if (existing != null)
                {
                    run.Results.Remove(existing);
                }
                run.Results.Add(result);

                if (result.Success)
                {
                    LogMessage?.Invoke($"  ✓ Done in {result.ElapsedMilliseconds} ms ({result.TokensPerSecond} tok/s) | Comp: {result.CompressionRatio:F1}% | Valid: {result.IsValidFormat}");
                }
                else
                {
                    LogMessage?.Invoke($"  ✗ Failed: {result.ErrorMessage}");
                }

                // Continuously persist to avoid losing progress
                await SaveRunAsync(run, runFilePath, cancellationToken).ConfigureAwait(false);
            }
        }

        // 2. Run Judge Evaluations
        if (!skipJudge && !string.IsNullOrWhiteSpace(judgeModel))
        {
            LogMessage?.Invoke($"\n=== Starting LLM Judge Evaluation with: {judgeModel} ===");

            var pendingJudges = run.Results
                .Where(r => r.Success && (r.Evaluation == null || !r.Evaluation.Success))
                .ToList();

            int judgeCount = pendingJudges.Count;
            int judgeDone = 0;

            LogMessage?.Invoke($"Evaluating {judgeCount} model outputs with judge...");

            foreach (var result in pendingJudges)
            {
                cancellationToken.ThrowIfCancellationRequested();
                judgeDone++;

                LogMessage?.Invoke($"[Judge {judgeDone}/{judgeCount}] Evaluating {result.ModelName} on {result.CommentId}...");

                var eval = await _client.EvaluateWithJudgeAsync(
                    judgeModel,
                    result.Kind,
                    result.OriginalText,
                    result.CleanedSummary,
                    cancellationToken).ConfigureAwait(false);

                result.Evaluation = eval;

                if (eval.Success)
                {
                    LogMessage?.Invoke($"  ✓ Score: {eval.OverallScore:F1}/10 (Acc: {eval.AccuracyScore:F1}, Conc: {eval.ConcisenessScore:F1}, Fmt: {eval.FormatComplianceScore:F1}) | {eval.ElapsedMilliseconds} ms");
                }
                else
                {
                    LogMessage?.Invoke($"  ✗ Judge failed: {eval.ErrorMessage}");
                }

                await SaveRunAsync(run, runFilePath, cancellationToken).ConfigureAwait(false);
            }
        }

        run.CompletedAt = DateTimeOffset.UtcNow;
        await SaveRunAsync(run, runFilePath, cancellationToken).ConfigureAwait(false);

        LogMessage?.Invoke($"\nBenchmark completed successfully! Results stored in {runFilePath}");
        return run;
    }

    /// <summary>
    /// Evaluates an existing run file with a judge model.
    /// </summary>
    public async Task<BenchmarkRun> JudgeExistingRunAsync(
        string runFilePath,
        string judgeModel,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(judgeModel);

        if (!File.Exists(runFilePath))
        {
            throw new FileNotFoundException($"Benchmark run file not found: {runFilePath}", runFilePath);
        }

        BenchmarkRun loadedRun;
        await using (var stream = File.OpenRead(runFilePath))
        {
            loadedRun = await JsonSerializer.DeserializeAsync<BenchmarkRun>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("Failed to deserialize run file.");
        }

        loadedRun.JudgeModel = judgeModel;

        var pendingJudges = loadedRun.Results
            .Where(r => r.Success)
            .ToList();

        int judgeCount = pendingJudges.Count;
        int judgeDone = 0;

        LogMessage?.Invoke($"Evaluating {judgeCount} model outputs with judge: {judgeModel}...");

        foreach (var result in pendingJudges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            judgeDone++;

            LogMessage?.Invoke($"[Judge {judgeDone}/{judgeCount}] Evaluating {result.ModelName} on {result.CommentId}...");

            var eval = await _client.EvaluateWithJudgeAsync(
                judgeModel,
                result.Kind,
                result.OriginalText,
                result.CleanedSummary,
                cancellationToken).ConfigureAwait(false);

            result.Evaluation = eval;

            if (eval.Success)
            {
                LogMessage?.Invoke($"  ✓ Score: {eval.OverallScore:F1}/10 (Acc: {eval.AccuracyScore:F1}, Conc: {eval.ConcisenessScore:F1}, Fmt: {eval.FormatComplianceScore:F1})");
            }
            else
            {
                LogMessage?.Invoke($"  ✗ Judge failed: {eval.ErrorMessage}");
            }

            await SaveRunAsync(loadedRun, runFilePath, cancellationToken).ConfigureAwait(false);
        }

        return loadedRun;
    }

    private static async Task SaveRunAsync(BenchmarkRun run, string filePath, CancellationToken ct)
    {
        var tempFile = filePath + ".tmp";
        await using (var stream = File.Create(tempFile))
        {
            await JsonSerializer.SerializeAsync(stream, run, JsonOptions, ct).ConfigureAwait(false);
        }
        File.Move(tempFile, filePath, overwrite: true);
    }
}
