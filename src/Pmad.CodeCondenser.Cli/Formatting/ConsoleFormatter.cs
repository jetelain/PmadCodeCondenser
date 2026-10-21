using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Cli.Formatting;

/// <summary>
/// Renders color-coded console output, diff summaries, and alerts with optional ANSI color suppression.
/// </summary>
public sealed class ConsoleFormatter : IConsoleFormatter
{
    private readonly bool _useColor;

    public const string AnsiReset = "\u001b[0m";
    public const string AnsiBold = "\u001b[1m";
    public const string AnsiDim = "\u001b[2m";
    public const string AnsiRed = "\u001b[31m";
    public const string AnsiGreen = "\u001b[32m";
    public const string AnsiYellow = "\u001b[33m";
    public const string AnsiBlue = "\u001b[34m";
    public const string AnsiMagenta = "\u001b[35m";
    public const string AnsiCyan = "\u001b[36m";

    public ConsoleFormatter(bool enableColor = true)
    {
        _useColor = enableColor;
    }

    public void PrintBanner(TextWriter writer)
    {
        writer.WriteLine(Bold("=== Pmad.CodeCondenser ==="));
        writer.WriteLine(Dim("Static analysis tool for C# structural clone detection and comment summarization."));
        writer.WriteLine();
    }

    public void PrintCloneGroups(TextWriter writer, IReadOnlyList<CodeCloneGroup> cloneGroups, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(cloneGroups);

        if (cloneGroups.Count == 0)
        {
            writer.WriteLine(Green("✓ No structural code duplication detected."));
            return;
        }

        var prodAndCrossGroups = cloneGroups.Where(g => g.Category != CloneCategory.Test).ToList();
        var testGroups = cloneGroups.Where(g => g.Category == CloneCategory.Test).ToList();

        if (prodAndCrossGroups.Count > 0)
        {
            writer.WriteLine(Yellow(Bold($"Found {prodAndCrossGroups.Count} Production Duplication Cluster(s) (Highest Impact):")));
            writer.WriteLine();

            for (int i = 0; i < prodAndCrossGroups.Count; i++)
            {
                var group = prodAndCrossGroups[i];
                var shortHash = group.NormalizedHash.Length > 12 ? group.NormalizedHash[..12] : group.NormalizedHash;
                var prefix = group.Category == CloneCategory.CrossBoundary ? "CROSS-BOUNDARY" : "PRODUCTION";

                writer.WriteLine(Yellow($"[{prefix} CLUSTER #{i + 1}] (Impact: {group.ImpactScore}, AST Hash: {shortHash}...)"));
                writer.WriteLine($"  Methods ({group.Methods.Count}):");

                foreach (var method in group.Methods)
                {
                    var relPath = FormatPath(method.FilePath, baseDirectory);
                    writer.WriteLine($"    • {Cyan(relPath)}:{method.StartLine}-{method.EndLine} in {Bold(method.MethodName)} ({method.StatementCount} statements)");
                    if (!string.IsNullOrWhiteSpace(method.Signature) && !string.Equals(method.Signature, method.MethodName, StringComparison.Ordinal))
                    {
                        writer.WriteLine($"      {Dim("Signature:")} {method.Signature}");
                    }
                }

                writer.WriteLine(Dim("  Recommendation: Extract duplicate structural logic into a shared helper method or service."));
                writer.WriteLine(Dim("  To suppress: Add '// [codecondenser:ignore-duplicate]' above method or run 'pmad-codecondenser snapshot'."));
                writer.WriteLine();
            }
        }

        if (testGroups.Count > 0)
        {
            if (prodAndCrossGroups.Count > 0)
            {
                writer.WriteLine(Dim("------------------------------------------------------------------------"));
                writer.WriteLine();
            }

            writer.WriteLine(Yellow(Bold($"Found {testGroups.Count} Test Duplication Cluster(s) (Review for DAMP vs DRY):")));
            writer.WriteLine();

            for (int i = 0; i < testGroups.Count; i++)
            {
                var group = testGroups[i];
                var shortHash = group.NormalizedHash.Length > 12 ? group.NormalizedHash[..12] : group.NormalizedHash;

                writer.WriteLine(Yellow($"[TEST CLUSTER #{i + 1}] (Impact: {group.ImpactScore}, AST Hash: {shortHash}...)"));
                writer.WriteLine($"  Methods ({group.Methods.Count}):");

                foreach (var method in group.Methods)
                {
                    var relPath = FormatPath(method.FilePath, baseDirectory);
                    writer.WriteLine($"    • {Cyan(relPath)}:{method.StartLine}-{method.EndLine} in {Bold(method.MethodName)} ({method.StatementCount} statements)");
                    if (!string.IsNullOrWhiteSpace(method.Signature) && !string.Equals(method.Signature, method.MethodName, StringComparison.Ordinal))
                    {
                        writer.WriteLine($"      {Dim("Signature:")} {method.Signature}");
                    }
                }

                writer.WriteLine(Dim("  Recommendation: Consider parameterized tests ([Theory] + [InlineData]) or test builder fixtures instead of production helpers."));
                writer.WriteLine(Dim("  To suppress: Add '// [codecondenser:ignore-duplicate]' above method or run 'pmad-codecondenser snapshot'."));
                writer.WriteLine();
            }
        }
    }

    public void PrintVerboseComments(
        TextWriter writer,
        IReadOnlyList<VerboseComment> comments,
        IReadOnlyDictionary<string, string> summaries,
        string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(comments);
        ArgumentNullException.ThrowIfNull(summaries);

        if (comments.Count == 0)
        {
            writer.WriteLine(Green("✓ No verbose comments detected."));
            return;
        }

        writer.WriteLine(Cyan(Bold($"Found {comments.Count} verbose comment(s) exceeding threshold:")));
        writer.WriteLine();

        for (int i = 0; i < comments.Count; i++)
        {
            var comment = comments[i];
            var relPath = FormatPath(comment.FilePath, baseDirectory);
            var originalLen = comment.OriginalText.Trim().Length;

            writer.WriteLine(Cyan($"[VERBOSE COMMENT #{i + 1}] {relPath}:{comment.StartLine}-{comment.EndLine} [{comment.Kind}] ({originalLen} chars)"));

            var originalPreview = TruncatePreview(comment.OriginalText.Trim(), 120);
            writer.WriteLine(Red($"  - Original:  {originalPreview}"));

            if (summaries.TryGetValue(comment.ContentHash, out var summary))
            {
                var summaryPreview = TruncatePreview(summary.Trim(), 120);
                var condensedLen = summary.Trim().Length;
                int reduction = originalLen > 0 ? (int)Math.Round((1.0 - (double)condensedLen / originalLen) * 100) : 0;
                reduction = Math.Clamp(reduction, 0, 100);

                writer.WriteLine(Green($"  + Condensed: {summaryPreview}"));
                writer.WriteLine(Dim($"    Diff: {originalLen} -> {condensedLen} chars ({reduction}% reduction)"));
            }
            else
            {
                writer.WriteLine(Dim("  (No summary generated yet; will be summarized via LLM)"));
            }

            writer.WriteLine();
        }
    }

    public void PrintSummary(
        TextWriter writer,
        int filesAnalyzed,
        int methodsScanned,
        int cloneGroupsCount,
        int verboseCommentsCount,
        string mode)
    {
        PrintSummary(writer, filesAnalyzed, methodsScanned, cloneGroupsCount, verboseCommentsCount, mode, 0, 0, 0, 0, 0);
    }

    public void PrintSummary(
        TextWriter writer,
        int filesAnalyzed,
        int methodsScanned,
        int cloneGroupsCount,
        int verboseCommentsCount,
        string mode,
        int productionFiles,
        int testFiles,
        int skippedGenerated,
        int productionClones,
        int testClones)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine(Dim("------------------------------------------------------------------------"));
        writer.WriteLine(Bold("Analysis Summary:"));
        writer.WriteLine($"  Mode:              {mode}");

        if (productionFiles > 0 || testFiles > 0 || skippedGenerated > 0)
        {
            var details = new List<string>();
            if (productionFiles > 0) details.Add($"Production: {productionFiles}");
            if (testFiles > 0) details.Add($"Tests: {testFiles}");
            if (skippedGenerated > 0) details.Add($"Skipped Generated: {skippedGenerated}");
            var breakdown = details.Count > 0 ? $" ({string.Join(", ", details)})" : string.Empty;
            writer.WriteLine($"  Files Analyzed:    {filesAnalyzed}{breakdown}");
        }
        else
        {
            writer.WriteLine($"  Files Analyzed:    {filesAnalyzed}");
        }

        writer.WriteLine($"  Methods Scanned:   {methodsScanned}");

        if (productionClones > 0 || testClones > 0)
        {
            writer.WriteLine($"  Production Clones: {(productionClones > 0 ? Yellow(productionClones.ToString()) : Green("0"))}");
            writer.WriteLine($"  Test Clones:       {(testClones > 0 ? Yellow(testClones.ToString()) : Green("0"))}");
        }
        else
        {
            writer.WriteLine($"  Clone Clusters:    {(cloneGroupsCount > 0 ? Yellow(cloneGroupsCount.ToString()) : Green("0"))}");
        }

        writer.WriteLine($"  Verbose Comments:  {(verboseCommentsCount > 0 ? Cyan(verboseCommentsCount.ToString()) : Green("0"))}");
        writer.WriteLine(Dim("------------------------------------------------------------------------"));
    }

    public void PrintFileApplied(TextWriter writer, string filePath, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var relPath = FormatPath(filePath, baseDirectory);
        writer.WriteLine(Green($"✓ Applied: {relPath}"));
    }

    public void PrintSnapshotSaved(TextWriter writer, string snapshotPath)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine(Cyan($"✓ Snapshot baseline saved: {snapshotPath}"));
    }

    public void PrintError(TextWriter writer, string message)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine(Red($"[Error] {message}"));
    }

    public void PrintSuccess(TextWriter writer, string message)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine(Green($"[Success] {message}"));
    }

    public void PrintWarning(TextWriter writer, string message)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine(Yellow($"[Warning] {message}"));
    }

    public void PrintInfo(TextWriter writer, string message)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine(Dim($"[Info] {message}"));
    }

    public void PrintCommentProgress(TextWriter writer, int completed, int total, string? currentItem = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (total <= 0) return;

        int percent = (int)Math.Round((double)completed / total * 100);
        percent = Math.Clamp(percent, 0, 100);

        var itemDetail = !string.IsNullOrWhiteSpace(currentItem) ? $" ({currentItem})" : string.Empty;
        var tag = _useColor ? Cyan("[Progress]") : "[Progress]";
        var message = $"{tag} Summarizing comments: {completed}/{total} ({percent}%){itemDetail}";

        writer.Write($"\r{message}   ");
        writer.Flush();

        if (completed >= total)
        {
            writer.WriteLine();
        }
    }

    public void PrintAiActionableDuplicationFeedback(
        TextWriter writer,
        IReadOnlyList<CodeCloneGroup> cloneGroups,
        string baseDirectory,
        int tolerance)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(cloneGroups);

        writer.WriteLine();
        writer.WriteLine(Red(Bold("========================================================================")));
        writer.WriteLine(Red(Bold("[AI AGENT ACTION REQUIRED] Code Duplication Policy Violation (Exit Code: 2)")));
        writer.WriteLine(Red(Bold("========================================================================")));
        writer.WriteLine($"Incremental analysis detected {Bold(cloneGroups.Count.ToString())} new code duplication cluster(s) exceeding tolerance margin (allowed: {tolerance}).");
        writer.WriteLine();

        for (int i = 0; i < cloneGroups.Count; i++)
        {
            var group = cloneGroups[i];
            var shortHash = group.NormalizedHash.Length > 12 ? group.NormalizedHash[..12] : group.NormalizedHash;
            int statementCount = group.Methods.FirstOrDefault()?.StatementCount ?? 0;

            writer.WriteLine(Yellow($"Cluster #{i + 1} [AST Hash: {shortHash}..., {statementCount} statements]:"));
            foreach (var method in group.Methods)
            {
                var relPath = FormatPath(method.FilePath, baseDirectory);
                var signature = !string.IsNullOrWhiteSpace(method.Signature) ? method.Signature : method.MethodName;
                writer.WriteLine($"  • {Cyan(relPath)}:{method.StartLine}-{method.EndLine} in {Bold(signature)}");
            }
            writer.WriteLine();
        }

        writer.WriteLine(Bold("Actionable Instructions for AI Agent:"));
        writer.WriteLine("  1. Inspect the duplicate methods listed in each cluster above.");
        writer.WriteLine("  2. Extract the identical structural logic into a shared helper method or service.");
        writer.WriteLine("  3. Replace the bodies of the duplicate methods with a call to the extracted helper.");
        writer.WriteLine("  4. Re-run 'pmad-codecondenser --incremental' to confirm the violation is resolved.");
        writer.WriteLine(Red(Bold("========================================================================")));
        writer.WriteLine();
    }

    private string Bold(string text) => _useColor ? $"{AnsiBold}{text}{AnsiReset}" : text;
    private string Dim(string text) => _useColor ? $"{AnsiDim}{text}{AnsiReset}" : text;
    private string Red(string text) => _useColor ? $"{AnsiRed}{text}{AnsiReset}" : text;
    private string Green(string text) => _useColor ? $"{AnsiGreen}{text}{AnsiReset}" : text;
    private string Yellow(string text) => _useColor ? $"{AnsiYellow}{text}{AnsiReset}" : text;
    private string Cyan(string text) => _useColor ? $"{AnsiCyan}{text}{AnsiReset}" : text;

    private static string FormatPath(string path, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            if (Path.IsPathRooted(path) && !string.IsNullOrWhiteSpace(baseDirectory))
            {
                var rel = Path.GetRelativePath(baseDirectory, path);
                return rel.Replace('\\', '/');
            }
        }
        catch
        {
            // fallback
        }

        return path.Replace('\\', '/');
    }

    private static string TruncatePreview(string text, int maxLength)
    {
        var singleLine = text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        if (singleLine.Length <= maxLength)
        {
            return singleLine;
        }

        return singleLine[..(maxLength - 3)] + "...";
    }
}
