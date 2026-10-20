using System.Text;
using System.Text.Json;
using Pmad.CodeCondenser.Benchmark.Models;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Benchmark.Reporting;

/// <summary>
/// Generates comparative performance summaries, ANSI console tables, and markdown benchmark reports.
/// </summary>
public sealed class BenchmarkReporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    /// <summary>
    /// Computes aggregated performance statistics for each candidate model in a benchmark run.
    /// </summary>
    public List<ModelAggregateStats> ComputeStats(BenchmarkRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        var list = new List<ModelAggregateStats>();

        var grouped = run.Results.GroupBy(r => r.ModelName, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            var results = group.ToList();
            var successResults = results.Where(r => r.Success).ToList();

            var stats = new ModelAggregateStats
            {
                ModelName = group.Key,
                TotalEvaluated = results.Count,
                SuccessCount = successResults.Count,
                ErrorCount = results.Count - successResults.Count,
                SuccessRate = results.Count > 0 ? (double)successResults.Count / results.Count * 100.0 : 0.0
            };

            if (successResults.Count > 0)
            {
                var latencies = successResults.Select(r => (double)r.ElapsedMilliseconds).OrderBy(l => l).ToList();
                stats.AverageLatencyMs = Math.Round(latencies.Average(), 1);
                stats.MedianLatencyMs = Math.Round(ComputePercentile(latencies, 0.50), 1);
                stats.P95LatencyMs = Math.Round(ComputePercentile(latencies, 0.95), 1);
                stats.MinLatencyMs = Math.Round(latencies.First(), 1);
                stats.MaxLatencyMs = Math.Round(latencies.Last(), 1);

                var tokPerSec = successResults.Where(r => r.TokensPerSecond > 0).Select(r => r.TokensPerSecond).ToList();
                stats.AverageTokensPerSecond = tokPerSec.Count > 0 ? Math.Round(tokPerSec.Average(), 1) : 0.0;

                stats.AverageCompressionRatio = Math.Round(successResults.Average(r => r.CompressionRatio), 1);
                stats.FormatComplianceRate = Math.Round((double)successResults.Count(r => r.IsValidFormat) / successResults.Count * 100.0, 1);

                var evaluated = successResults.Where(r => r.Evaluation != null && r.Evaluation.Success).ToList();
                if (evaluated.Count > 0)
                {
                    stats.AverageOverallScore = Math.Round(evaluated.Average(r => r.Evaluation!.OverallScore), 2);
                    stats.AverageAccuracyScore = Math.Round(evaluated.Average(r => r.Evaluation!.AccuracyScore), 2);
                    stats.AverageConcisenessScore = Math.Round(evaluated.Average(r => r.Evaluation!.ConcisenessScore), 2);
                    stats.AverageFormatScore = Math.Round(evaluated.Average(r => r.Evaluation!.FormatComplianceScore), 2);
                }

                // By category
                foreach (var kindGroup in successResults.GroupBy(r => r.Kind))
                {
                    var kResults = kindGroup.ToList();
                    var kEvaluated = kResults.Where(r => r.Evaluation != null && r.Evaluation.Success).ToList();

                    var catStats = new CategoryStats
                    {
                        Count = kResults.Count,
                        AverageLatencyMs = Math.Round(kResults.Average(r => r.ElapsedMilliseconds), 1),
                        AverageCompressionRatio = Math.Round(kResults.Average(r => r.CompressionRatio), 1),
                        FormatComplianceRate = Math.Round((double)kResults.Count(r => r.IsValidFormat) / kResults.Count * 100.0, 1),
                        AverageOverallScore = kEvaluated.Count > 0 ? Math.Round(kEvaluated.Average(r => r.Evaluation!.OverallScore), 2) : 0.0,
                        AverageAccuracyScore = kEvaluated.Count > 0 ? Math.Round(kEvaluated.Average(r => r.Evaluation!.AccuracyScore), 2) : 0.0,
                        AverageConcisenessScore = kEvaluated.Count > 0 ? Math.Round(kEvaluated.Average(r => r.Evaluation!.ConcisenessScore), 2) : 0.0,
                        AverageFormatScore = kEvaluated.Count > 0 ? Math.Round(kEvaluated.Average(r => r.Evaluation!.FormatComplianceScore), 2) : 0.0
                    };

                    stats.ByCategory[kindGroup.Key] = catStats;
                }
            }

            list.Add(stats);
        }

        // Rank by Overall Score descending, then by Latency ascending
        return list
            .OrderByDescending(s => s.AverageOverallScore)
            .ThenBy(s => s.AverageLatencyMs)
            .ToList();
    }

    /// <summary>
    /// Renders a color-coded ANSI console summary table.
    /// </summary>
    public void RenderConsoleSummary(TextWriter writer, BenchmarkRun run, List<ModelAggregateStats> stats, bool useColor = true)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(stats);

        string Bold(string text) => useColor ? $"\u001b[1m{text}\u001b[0m" : text;
        string Green(string text) => useColor ? $"\u001b[32m{text}\u001b[0m" : text;
        string Yellow(string text) => useColor ? $"\u001b[33m{text}\u001b[0m" : text;
        string Cyan(string text) => useColor ? $"\u001b[36m{text}\u001b[0m" : text;

        writer.WriteLine();
        writer.WriteLine(Bold("========================================================================================="));
        writer.WriteLine(Bold("                         COMMENT SUMMARIZATION BENCHMARK REPORT                          "));
        writer.WriteLine(Bold("========================================================================================="));
        writer.WriteLine($"Judge Model:    {Cyan(run.JudgeModel)}");
        writer.WriteLine($"Comments:       {run.CommentCount} total");
        writer.WriteLine($"Ollama Host:    {run.Endpoint}");
        writer.WriteLine($"Completed At:   {run.CompletedAt:yyyy-MM-dd HH:mm:ss UTC}");
        writer.WriteLine();

        writer.WriteLine(Bold("--- MODEL RANKING & PERFORMANCE SUMMARY ---"));
        writer.WriteLine();

        var header = string.Format("{0,-4} {1,-20} {2,10} {3,12} {4,10} {5,10} {6,10} {7,8}",
            "Rank", "Model", "Score (/10)", "Avg Lat (ms)", "Tok/sec", "Comp %", "Format %", "Success");
        writer.WriteLine(Bold(header));
        writer.WriteLine(new string('-', header.Length));

        for (int i = 0; i < stats.Count; i++)
        {
            var s = stats[i];
            var rank = (i + 1).ToString();
            var line = string.Format("{0,-4} {1,-20} {2,10:F2} {3,12:F0} {4,10:F1} {5,10:F1}% {6,9:F1}% {7,7:F0}%",
                rank,
                s.ModelName,
                s.AverageOverallScore,
                s.AverageLatencyMs,
                s.AverageTokensPerSecond,
                s.AverageCompressionRatio,
                s.FormatComplianceRate,
                s.SuccessRate);

            if (i == 0)
            {
                writer.WriteLine(Green(line));
            }
            else
            {
                writer.WriteLine(line);
            }
        }

        writer.WriteLine(new string('-', header.Length));
        writer.WriteLine();

        // Detailed Criteria
        writer.WriteLine(Bold("--- SCORING BREAKDOWN (LLM-AS-A-JUDGE) ---"));
        writer.WriteLine();
        var critHeader = string.Format("{0,-20} {1,12} {2,12} {3,12} {4,12}",
            "Model", "Overall (/10)", "Accuracy", "Conciseness", "Formatting");
        writer.WriteLine(Bold(critHeader));
        writer.WriteLine(new string('-', critHeader.Length));

        foreach (var s in stats)
        {
            writer.WriteLine(string.Format("{0,-20} {1,12:F2} {2,12:F2} {3,12:F2} {4,12:F2}",
                s.ModelName,
                s.AverageOverallScore,
                s.AverageAccuracyScore,
                s.AverageConcisenessScore,
                s.AverageFormatScore));
        }

        writer.WriteLine(new string('-', critHeader.Length));
        writer.WriteLine();

        // Recommendations
        if (stats.Count > 0)
        {
            var bestQuality = stats.First();
            var fastest = stats.OrderBy(s => s.AverageLatencyMs).First();
            var bestBalanced = stats
                .OrderByDescending(s => s.AverageOverallScore / Math.Log10(Math.Max(100, s.AverageLatencyMs)))
                .First();

            writer.WriteLine(Bold("--- RECOMMENDATIONS ---"));
            writer.WriteLine($"  🏆 {Bold("Best Quality:")}       {Green(bestQuality.ModelName)} (Score: {bestQuality.AverageOverallScore:F2}/10, Latency: {bestQuality.AverageLatencyMs:F0} ms)");
            writer.WriteLine($"  ⚡ {Bold("Fastest Model:")}      {Cyan(fastest.ModelName)} (Latency: {fastest.AverageLatencyMs:F0} ms, Score: {fastest.AverageOverallScore:F2}/10)");
            writer.WriteLine($"  ⚖️ {Bold("Best Balanced:")}      {Yellow(bestBalanced.ModelName)} (Score: {bestBalanced.AverageOverallScore:F2}/10, Latency: {bestBalanced.AverageLatencyMs:F0} ms)");
            writer.WriteLine();
        }
    }

    /// <summary>
    /// Generates a comprehensive Markdown report file.
    /// </summary>
    public async Task GenerateMarkdownReportAsync(
        BenchmarkRun run,
        List<ModelAggregateStats> stats,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var sb = new StringBuilder();

        sb.AppendLine("# Comment Summarization Model Benchmark Report");
        sb.AppendLine();
        sb.AppendLine($"> Generated on {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC} using **{run.JudgeModel}** as LLM Judge.");
        sb.AppendLine();

        // 1. Executive Summary & Recommendations
        sb.AppendLine("## 1. Executive Summary & Recommendations");
        sb.AppendLine();

        string Inv(FormattableString formattable) => FormattableString.Invariant(formattable);

        if (stats.Count > 0)
        {
            var bestQuality = stats.First();
            var fastest = stats.OrderBy(s => s.AverageLatencyMs).First();
            var bestBalanced = stats
                .OrderByDescending(s => s.AverageOverallScore / Math.Log10(Math.Max(100, s.AverageLatencyMs)))
                .First();

            sb.AppendLine("| Category | Recommended Model | Overall Score | Avg Latency | Tokens/sec | Key Advantage |");
            sb.AppendLine("| :--- | :--- | :---: | :---: | :---: | :--- |");
            sb.AppendLine(Inv($"| 🏆 **Top Quality** | `{bestQuality.ModelName}` | **{bestQuality.AverageOverallScore:F2} / 10** | {bestQuality.AverageLatencyMs:F0} ms | {bestQuality.AverageTokensPerSecond:F1} | Highest accuracy, faithful XML tag preservation, clean C# syntax |"));
            sb.AppendLine(Inv($"| ⚡ **Fastest / Throughput** | `{fastest.ModelName}` | {fastest.AverageOverallScore:F2} / 10 | **{fastest.AverageLatencyMs:F0} ms** | **{fastest.AverageTokensPerSecond:F1}** | Minimal GPU overhead and rapid generation |"));
            sb.AppendLine(Inv($"| ⚖️ **Best Balanced** | `{bestBalanced.ModelName}` | {bestBalanced.AverageOverallScore:F2} / 10 | {bestBalanced.AverageLatencyMs:F0} ms | {bestBalanced.AverageTokensPerSecond:F1} | Optimal quality-to-latency trade-off for interactive CLI workflows |"));
            sb.AppendLine();
        }

        // 2. Benchmark Setup
        sb.AppendLine("## 2. Benchmark Environment & Configuration");
        sb.AppendLine();
        sb.AppendLine($"- **Ollama Endpoint**: `{run.Endpoint}`");
        sb.AppendLine($"- **LLM Judge**: `{run.JudgeModel}` (Temperature: `0.0`, strict JSON scoring)");
        sb.AppendLine($"- **Evaluated Comments**: `{run.CommentCount}` curated comments across reference repositories");
        sb.AppendLine($"- **Candidate Models**: {string.Join(", ", run.CandidateModels.Select(m => $"`{m}`"))}");
        sb.AppendLine();

        // 3. Performance Summary Table
        sb.AppendLine("## 3. Overall Performance Rankings");
        sb.AppendLine();
        sb.AppendLine("| Rank | Model | Overall Score | Avg Latency | Median Latency | P95 Latency | Tok/s | Comp % | Format % | Success % |");
        sb.AppendLine("| :---: | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |");

        for (int i = 0; i < stats.Count; i++)
        {
            var s = stats[i];
            sb.AppendLine(Inv($"| {i + 1} | `{s.ModelName}` | **{s.AverageOverallScore:F2}** | {s.AverageLatencyMs:F0} ms | {s.MedianLatencyMs:F0} ms | {s.P95LatencyMs:F0} ms | {s.AverageTokensPerSecond:F1} | {s.AverageCompressionRatio:F1}% | {s.FormatComplianceRate:F1}% | {s.SuccessRate:F0}% |"));
        }
        sb.AppendLine();

        // 4. Detailed Evaluation Criteria
        sb.AppendLine("## 4. Evaluation Criteria Breakdown");
        sb.AppendLine();
        sb.AppendLine("| Model | Overall Score | Technical Accuracy | Conciseness | Format Compliance |");
        sb.AppendLine("| :--- | :---: | :---: | :---: | :---: |");

        foreach (var s in stats)
        {
            sb.AppendLine(Inv($"| `{s.ModelName}` | **{s.AverageOverallScore:F2}** | {s.AverageAccuracyScore:F2} | {s.AverageConcisenessScore:F2} | {s.AverageFormatScore:F2} |"));
        }
        sb.AppendLine();

        // 5. Category Breakdown (XML vs Non-XML)
        sb.AppendLine("## 5. Performance by Comment Kind");
        sb.AppendLine();

        sb.AppendLine("### XML Documentation Comments (`/// <summary>...`)");
        sb.AppendLine();
        sb.AppendLine("| Model | Count | Score | Avg Latency | Compression % | Valid XML Format % |");
        sb.AppendLine("| :--- | :---: | :---: | :---: | :---: | :---: |");

        foreach (var s in stats)
        {
            if (s.ByCategory.TryGetValue(CommentKind.XmlDocumentation, out var xmlStats))
            {
                sb.AppendLine(Inv($"| `{s.ModelName}` | {xmlStats.Count} | **{xmlStats.AverageOverallScore:F2}** | {xmlStats.AverageLatencyMs:F0} ms | {xmlStats.AverageCompressionRatio:F1}% | {xmlStats.FormatComplianceRate:F1}% |"));
            }
        }
        sb.AppendLine();

        sb.AppendLine("### Regular Comments (`//` and `/* */`)");
        sb.AppendLine();
        sb.AppendLine("| Model | Count | Score | Avg Latency | Compression % | Format % |");
        sb.AppendLine("| :--- | :---: | :---: | :---: | :---: | :---: |");

        foreach (var s in stats)
        {
            var regularCount = 0;
            var regLatency = 0.0;
            var regComp = 0.0;
            var regFormat = 0.0;
            var regScore = 0.0;
            var count = 0;

            foreach (var kvp in s.ByCategory)
            {
                if (kvp.Key != CommentKind.XmlDocumentation)
                {
                    regularCount += kvp.Value.Count;
                    regLatency += kvp.Value.AverageLatencyMs * kvp.Value.Count;
                    regComp += kvp.Value.AverageCompressionRatio * kvp.Value.Count;
                    regFormat += kvp.Value.FormatComplianceRate * kvp.Value.Count;
                    regScore += kvp.Value.AverageOverallScore * kvp.Value.Count;
                    count += kvp.Value.Count;
                }
            }

            if (count > 0)
            {
                sb.AppendLine(Inv($"| `{s.ModelName}` | {regularCount} | **{regScore / count:F2}** | {regLatency / count:F0} ms | {regComp / count:F1}% | {regFormat / count:F1}% |"));
            }
        }
        sb.AppendLine();

        // 6. Qualitative Examples Comparison
        sb.AppendLine("## 6. Qualitative Case Studies (Side-by-Side Comparison)");
        sb.AppendLine();

        var sampleComments = run.Results
            .GroupBy(r => r.CommentId)
            .Take(4)
            .ToList();

        foreach (var commentGroup in sampleComments)
        {
            var first = commentGroup.First();
            sb.AppendLine($"### Comment `{first.CommentId}` ({first.Project} - {first.Kind})");
            sb.AppendLine();
            sb.AppendLine("**Original Comment:**");
            sb.AppendLine("```csharp");
            sb.AppendLine(first.OriginalText.Trim());
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("| Model | Summarized Output | Latency | Judge Score | Judge Critique |");
            sb.AppendLine("| :--- | :--- | :---: | :---: | :--- |");

            foreach (var res in commentGroup.OrderByDescending(r => r.Evaluation?.OverallScore ?? 0))
            {
                var summaryEscaped = res.CleanedSummary.Replace("\r\n", "<br>").Replace("\n", "<br>").Replace("|", "\\|");
                var critique = res.Evaluation?.Critique.Replace("|", "\\|") ?? "N/A";
                var score = res.Evaluation != null ? FormattableString.Invariant($"{res.Evaluation.OverallScore:F1}/10") : "N/A";

                sb.AppendLine($"| `{res.ModelName}` | <pre>{summaryEscaped}</pre> | {res.ElapsedMilliseconds} ms | **{score}** | {critique} |");
            }
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(outputPath, sb.ToString(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves the aggregate statistics to a JSON file.
    /// </summary>
    public async Task SaveSummaryJsonAsync(List<ModelAggregateStats> stats, string outputPath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(stream, stats, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static double ComputePercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0.0;
        if (sortedValues.Count == 1) return sortedValues[0];

        double rank = percentile * (sortedValues.Count - 1);
        int lowerIndex = (int)Math.Floor(rank);
        int upperIndex = (int)Math.Ceiling(rank);

        if (lowerIndex == upperIndex) return sortedValues[lowerIndex];

        double weight = rank - lowerIndex;
        return sortedValues[lowerIndex] * (1.0 - weight) + sortedValues[upperIndex] * weight;
    }
}
