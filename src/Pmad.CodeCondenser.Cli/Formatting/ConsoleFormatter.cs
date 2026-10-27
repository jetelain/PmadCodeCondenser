using System.Globalization;
using Pmad.AI.Tokens;
using Pmad.CodeCondenser.Core.Clones;
using Pmad.CodeCondenser.Core.Comments;
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

    private static string CloneGainText(int tokens) => tokens > 0 ? $", Potential Gain: ~{tokens} tokens" : string.Empty;

    public void PrintCloneGroups(TextWriter writer, IReadOnlyList<CodeCloneGroup> cloneGroups, string baseDirectory, HeuristicTokenEstimator? estimator = null)
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

                var gainText = CloneGainText(CloneGainEstimator.EstimateGainTokens(group, estimator, baseDirectory));
                writer.WriteLine(Yellow($"[{prefix} CLUSTER #{i + 1}] (Impact: {group.ImpactScore}{gainText}, AST Hash: {shortHash}...)"));
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

                if (group.IsComplementaryPair)
                {
                    writer.WriteLine(Dim("  Recommendation: Symmetric opposite methods. Extracting a shared helper may impair readability; evaluate whether keeping explicit paired methods is preferable."));
                }
                else
                {
                    writer.WriteLine(Dim("  Recommendation: Extract duplicate structural logic into a shared helper method or service."));
                }
                writer.WriteLine(Dim("  To suppress: Add '[SuppressMessage(\"CodeCondenser\", \"Duplication\", Justification = \"<why>\")]' on method."));
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

                var gainText = CloneGainText(CloneGainEstimator.EstimateGainTokens(group, estimator, baseDirectory));
                writer.WriteLine(Yellow($"[TEST CLUSTER #{i + 1}] (Impact: {group.ImpactScore}{gainText}, AST Hash: {shortHash}...)"));
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

                if (group.IsComplementaryPair)
                {
                    writer.WriteLine(Dim("  Recommendation: Symmetric opposite methods. Extracting a shared helper may impair readability; evaluate whether keeping explicit paired methods is preferable."));
                }
                else
                {
                    writer.WriteLine(Dim("  Recommendation: Consider parameterized tests ([Theory] + [InlineData]) or test builder fixtures instead of production helpers."));
                }
                writer.WriteLine(Dim("  To suppress: Add '[SuppressMessage(\"CodeCondenser\", \"Duplication\", Justification = \"<why>\")]' on method."));
                writer.WriteLine();
            }
        }
    }

    public void PrintSubMethodCloneGroups(TextWriter writer, IReadOnlyList<SubMethodCloneGroup> subMethodGroups, string baseDirectory, HeuristicTokenEstimator? estimator = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(subMethodGroups);

        if (subMethodGroups.Count == 0)
        {
            return;
        }

        writer.WriteLine(Yellow(Bold($"Found {subMethodGroups.Count} Sub-Method Duplication Cluster(s):")));
        writer.WriteLine();

        for (int i = 0; i < subMethodGroups.Count; i++)
        {
            var group = subMethodGroups[i];
            var gainText = CloneGainText(CloneGainEstimator.EstimateGainTokens(group, estimator, baseDirectory));
            writer.WriteLine(Yellow($"[SUB-METHOD DUPLICATION CLUSTER #{i + 1}] ({group.StatementCount} consecutive statements, Impact: {group.ImpactScore}{gainText})"));
            writer.WriteLine($"  Occurrences ({group.Occurrences.Count}):");

            foreach (var occ in group.Occurrences)
            {
                var relPath = FormatPath(occ.FilePath, baseDirectory);
                writer.WriteLine($"    • {Cyan(relPath)}:{occ.StartLine}-{occ.EndLine} in {Bold(occ.MethodName)}");
            }

            var rec = !string.IsNullOrWhiteSpace(group.Recommendation)
                ? group.Recommendation
                : $"Extract duplicate {group.StatementCount}-statement block into a shared helper method.";
            writer.WriteLine(Dim($"  Recommendation: {rec}"));
            writer.WriteLine(Dim("  To suppress: Add '[SuppressMessage(\"CodeCondenser\", \"Duplication\", Justification = \"<why>\")]' or '// codecondenser:ignore-duplicate' on method."));
            writer.WriteLine();
        }
    }

    public void PrintRepeatedStringGroups(TextWriter writer, IReadOnlyList<RepeatedStringGroup> repeatedStrings, string baseDirectory, HeuristicTokenEstimator? estimator = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(repeatedStrings);

        if (repeatedStrings.Count == 0)
        {
            return;
        }

        var prodAndCrossGroups = repeatedStrings.Where(g => g.Category != CloneCategory.Test).ToList();
        var testGroups = repeatedStrings.Where(g => g.Category == CloneCategory.Test).ToList();

        if (prodAndCrossGroups.Count > 0)
        {
            writer.WriteLine(Yellow(Bold($"Found {prodAndCrossGroups.Count} Repeated Inline String Cluster(s) (Highest Impact):")));
            writer.WriteLine();

            for (int i = 0; i < prodAndCrossGroups.Count; i++)
            {
                var group = prodAndCrossGroups[i];
                var prefix = group.Category == CloneCategory.CrossBoundary ? "CROSS-BOUNDARY" : "PRODUCTION";
                var gainChars = group.EstimateGainCharacters(preExistingClass: true);
                var gainTokens = group.EstimateGainTokens(estimator);
                var gainParts = new List<string>();
                if (gainChars > 0) gainParts.Add($"~{gainChars} chars");
                if (gainTokens > 0) gainParts.Add($"~{gainTokens} tokens");
                var gainText = gainParts.Count > 0 ? $", Potential Gain: {string.Join(", ", gainParts)}" : string.Empty;

                writer.WriteLine(Yellow($"[{prefix} REPEATED STRING #{i + 1}] ({group.Length} chars, {group.Occurrences.Count} occurrences{gainText}):"));
                var preview = TruncatePreview(group.Value.Replace("\r\n", " ").Replace("\n", " "), 100);
                writer.WriteLine($"  Value: \"{Bold(preview)}\"");
                writer.WriteLine($"  Occurrences ({group.Occurrences.Count}):");

                foreach (var occ in group.Occurrences)
                {
                    var relPath = FormatPath(occ.FilePath, baseDirectory);
                    var memberInfo = !string.IsNullOrEmpty(occ.EnclosingMember) ? $" in {Bold(occ.EnclosingMember)}" : string.Empty;
                    writer.WriteLine($"    • {Cyan(relPath)}:{occ.StartLine}:{occ.StartColumn}{memberInfo}");
                }

                writer.WriteLine(Dim($"  Suggested Constant: {group.GenerateTypicalUsage()}"));
                writer.WriteLine(Dim("  Minimal Declaration:"));
                writer.WriteLine(Dim($"    namespace {group.TargetNamespace ?? "ApplicationNamespace"};"));
                writer.WriteLine(Dim($"    static class {group.SuggestedClassName}"));
                writer.WriteLine(Dim("    {"));
                writer.WriteLine(Dim($"        internal const string {group.SuggestedConstantName} = \"...\";"));
                writer.WriteLine(Dim("    }"));
                writer.WriteLine(Dim("  Recommendation: Extract repeated inline string into a shared constant."));
                writer.WriteLine(Dim("  To suppress: Add '[SuppressMessage(\"CodeCondenser\", \"RepeatedString\")]' or '// codecondenser:ignore-string'."));
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

            writer.WriteLine(Yellow(Bold($"Found {testGroups.Count} Test Repeated String Cluster(s):")));
            writer.WriteLine();

            for (int i = 0; i < testGroups.Count; i++)
            {
                var group = testGroups[i];
                var gainChars = group.EstimateGainCharacters(preExistingClass: true);
                var gainTokens = group.EstimateGainTokens(estimator);
                var gainParts = new List<string>();
                if (gainChars > 0) gainParts.Add($"~{gainChars} chars");
                if (gainTokens > 0) gainParts.Add($"~{gainTokens} tokens");
                var gainText = gainParts.Count > 0 ? $", Potential Gain: {string.Join(", ", gainParts)}" : string.Empty;

                writer.WriteLine(Yellow($"[TEST REPEATED STRING #{i + 1}] ({group.Length} chars, {group.Occurrences.Count} occurrences{gainText}):"));
                var preview = TruncatePreview(group.Value.Replace("\r\n", " ").Replace("\n", " "), 100);
                writer.WriteLine($"  Value: \"{Bold(preview)}\"");
                writer.WriteLine($"  Occurrences ({group.Occurrences.Count}):");

                foreach (var occ in group.Occurrences)
                {
                    var relPath = FormatPath(occ.FilePath, baseDirectory);
                    var memberInfo = !string.IsNullOrEmpty(occ.EnclosingMember) ? $" in {Bold(occ.EnclosingMember)}" : string.Empty;
                    writer.WriteLine($"    • {Cyan(relPath)}:{occ.StartLine}:{occ.StartColumn}{memberInfo}");
                }

                writer.WriteLine(Dim($"  Suggested Constant: {group.GenerateTypicalUsage()}"));
                writer.WriteLine(Dim("  Recommendation: Extract repeated inline string into a shared test constant or fixture."));
                writer.WriteLine(Dim("  To suppress: Add '[SuppressMessage(\"CodeCondenser\", \"RepeatedString\")]' or '// codecondenser:ignore-string'."));
                writer.WriteLine();
            }
        }
    }

    public void PrintVerboseComments(
        TextWriter writer,
        IReadOnlyList<VerboseComment> comments,
        IReadOnlyDictionary<string, string> summaries,
        string baseDirectory,
        HeuristicTokenEstimator? estimator = null)
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

        var effEstimator = estimator ?? HeuristicTokenEstimator.Default;

        int totalOriginalLen = 0;
        int totalCondensedLen = 0;
        int totalOriginalTokens = 0;
        int totalCondensedTokens = 0;
        int totalSummarized = 0;

        for (int i = 0; i < comments.Count; i++)
        {
            var comment = comments[i];
            var relPath = FormatPath(comment.FilePath, baseDirectory);
            var trimmedOrig = comment.OriginalText.Trim();
            var originalLen = trimmedOrig.Length;
            var originalTokens = effEstimator.Estimate(trimmedOrig.AsSpan());

            writer.WriteLine(Cyan($"[VERBOSE COMMENT #{i + 1}] {relPath}:{comment.StartLine}-{comment.EndLine} [{comment.Kind}] ({originalLen} chars, ~{originalTokens} tokens)"));

            var originalPreview = TruncatePreview(trimmedOrig, 120);
            writer.WriteLine(Red($"  - Original:  {originalPreview}"));

            if (summaries.TryGetValue(comment.ContentHash, out var summary) && !string.IsNullOrWhiteSpace(summary))
            {
                var trimmedSummary = summary.Trim();
                var summaryPreview = TruncatePreview(trimmedSummary, 120);
                bool isUseless = CommentVerbosityHelper.IsUselessComment(trimmedSummary);
                bool isKept = !isUseless && (string.Equals(trimmedSummary, trimmedOrig, StringComparison.Ordinal) ||
                              CommentVerbosityHelper.IsMoreVerbose(summary, comment));

                if (isUseless)
                {
                    writer.WriteLine(Yellow("  x Useless:   Comment is useless (will be removed)"));
                    writer.WriteLine(Green($"    Diff: {originalLen} -> 0 chars, ~{originalTokens} -> ~0 tokens (100% reduction, comment removed)"));
                    totalOriginalLen += originalLen;
                    totalCondensedLen += 0;
                    totalOriginalTokens += originalTokens;
                    totalCondensedTokens += 0;
                    totalSummarized++;
                }
                else if (isKept)
                {
                    writer.WriteLine(Yellow($"  = Kept:      {summaryPreview}"));
                    writer.WriteLine(Dim($"    Diff: {originalLen} -> {originalLen} chars, ~{originalTokens} -> ~{originalTokens} tokens (0% reduction, kept original comment)"));
                    totalOriginalLen += originalLen;
                    totalCondensedLen += originalLen;
                    totalOriginalTokens += originalTokens;
                    totalCondensedTokens += originalTokens;
                    totalSummarized++;
                }
                else
                {
                    var condensedLen = trimmedSummary.Length;
                    var condensedTokens = effEstimator.Estimate(trimmedSummary.AsSpan());
                    int reduction = originalLen > 0 ? (int)Math.Round((1.0 - (double)condensedLen / originalLen) * 100) : 0;
                    reduction = Math.Clamp(reduction, 0, 100);

                    writer.WriteLine(Green($"  + Condensed: {summaryPreview}"));
                    writer.WriteLine(Dim($"    Diff: {originalLen} -> {condensedLen} chars, ~{originalTokens} -> ~{condensedTokens} tokens ({reduction}% reduction)"));
                    totalOriginalLen += originalLen;
                    totalCondensedLen += condensedLen;
                    totalOriginalTokens += originalTokens;
                    totalCondensedTokens += condensedTokens;
                    totalSummarized++;
                }
            }
            else
            {
                writer.WriteLine(Dim("  Recommendation: Condense verbose comment to be concise (max 120 columns), or suppress if extensive documentation is intentional."));
                if (comment.Kind == CommentKind.XmlDocumentation)
                {
                    writer.WriteLine(Dim("  To suppress: Add condense=\"no\" to XML doc tag (e.g. <summary condense=\"no\">)."));
                }
                else
                {
                    writer.WriteLine(Dim("  To suppress: Add '// codecondenser:ignore' or '// nocondense' on preceding line or inline."));
                }
            }

            writer.WriteLine();
        }

        if (totalSummarized > 0)
        {
            int totalGain = Math.Max(0, totalOriginalLen - totalCondensedLen);
            int totalTokenGain = Math.Max(0, totalOriginalTokens - totalCondensedTokens);
            int totalReduction = totalOriginalLen > 0 ? (int)Math.Round((1.0 - (double)totalCondensedLen / totalOriginalLen) * 100) : 0;
            totalReduction = Math.Clamp(totalReduction, 0, 100);

            var gainText = totalGain > 0
                ? Green($"Total Gain: {totalGain} chars, ~{totalTokenGain} tokens ({totalReduction}% reduction, {totalOriginalLen} -> {totalCondensedLen} chars across {totalSummarized} comment(s))")
                : Dim($"Total Gain: 0 chars, ~0 tokens (0% reduction across {totalSummarized} comment(s))");

            writer.WriteLine(Bold(gainText));
            writer.WriteLine();
        }
    }

    public void PrintSummary(
        TextWriter writer,
        int filesAnalyzed,
        int methodsScanned,
        int cloneGroupsCount,
        int verboseCommentsCount,
        string mode,
        int productionFiles = 0,
        int testFiles = 0,
        int skippedGenerated = 0,
        int productionClones = 0,
        int testClones = 0,
        int subMethodClones = 0,
        int? commentGainChars = null,
        int? commentReductionPercent = null,
        int? commentGainTokens = null,
        int? cloneGainTokens = null,
        int? repeatedStringsCount = null,
        int? repeatedStringGainChars = null,
        int? repeatedStringGainTokens = null)
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

        if (subMethodClones > 0)
        {
            writer.WriteLine($"  Sub-Method Clones: {Yellow(subMethodClones.ToString())}");
        }

        if (cloneGainTokens.HasValue && cloneGainTokens.Value > 0)
        {
            writer.WriteLine($"  Clone Gain:        {Green($"~{cloneGainTokens.Value} tokens (potential)")}");
        }

        if (repeatedStringsCount.HasValue)
        {
            writer.WriteLine($"  Repeated Strings:  {(repeatedStringsCount.Value > 0 ? Yellow(repeatedStringsCount.Value.ToString()) : Green("0"))}");
        }

        if (repeatedStringGainChars.HasValue && repeatedStringGainChars.Value > 0)
        {
            var tokensSuffix = repeatedStringGainTokens.HasValue ? $", ~{repeatedStringGainTokens.Value} tokens" : string.Empty;
            writer.WriteLine($"  String Gain:       {Green($"+{repeatedStringGainChars.Value} chars{tokensSuffix} (potential)")}");
        }

        writer.WriteLine($"  Verbose Comments:  {(verboseCommentsCount > 0 ? Cyan(verboseCommentsCount.ToString()) : Green("0"))}");

        if (commentGainChars.HasValue)
        {
            var tokensSuffix = commentGainTokens.HasValue ? $", ~{commentGainTokens.Value} tokens" : string.Empty;
            var gainDisplay = commentGainChars.Value > 0
                ? Green($"+{commentGainChars.Value} chars{tokensSuffix} ({commentReductionPercent ?? 0}% reduction)")
                : Dim("0 chars (0% reduction)");
            writer.WriteLine($"  Comment Gain:      {gainDisplay}");
        }

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
            int statementCount = group.StatementCount;

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
        writer.WriteLine("  4. To suppress: Add '[SuppressMessage(\"CodeCondenser\", \"Duplication\", Justification = \"<why>\")]' or '// codecondenser:ignore-duplicate' on the method.");
        writer.WriteLine("  5. Re-run 'codecondenser --incremental' to confirm the violation is resolved.");
        writer.WriteLine(Red(Bold("========================================================================")));
        writer.WriteLine();
    }

    public void PrintAiActionableDuplicationFeedback(
        TextWriter writer,
        IReadOnlyList<CodeCloneGroup> cloneGroups,
        IReadOnlyList<SubMethodCloneGroup> subMethodGroups,
        string baseDirectory,
        int tolerance)
    {
        ArgumentNullException.ThrowIfNull(writer);
        cloneGroups ??= [];
        subMethodGroups ??= [];

        int totalCount = cloneGroups.Count + subMethodGroups.Count;

        writer.WriteLine();
        writer.WriteLine(Red(Bold("========================================================================")));
        writer.WriteLine(Red(Bold("[AI AGENT ACTION REQUIRED] Code Duplication Policy Violation (Exit Code: 2)")));
        writer.WriteLine(Red(Bold("========================================================================")));
        writer.WriteLine($"Incremental analysis detected {Bold(totalCount.ToString())} new code duplication cluster(s) exceeding tolerance margin (allowed: {tolerance}).");
        writer.WriteLine();

        int clusterIdx = 1;

        if (cloneGroups.Count > 0)
        {
            for (int i = 0; i < cloneGroups.Count; i++)
            {
                var group = cloneGroups[i];
                var shortHash = group.NormalizedHash.Length > 12 ? group.NormalizedHash[..12] : group.NormalizedHash;
                int statementCount = group.StatementCount;

                writer.WriteLine(Yellow($"Cluster #{clusterIdx++} [Whole-Method, AST Hash: {shortHash}..., {statementCount} statements]:"));
                foreach (var method in group.Methods)
                {
                    var relPath = FormatPath(method.FilePath, baseDirectory);
                    var signature = !string.IsNullOrWhiteSpace(method.Signature) ? method.Signature : method.MethodName;
                    writer.WriteLine($"  • {Cyan(relPath)}:{method.StartLine}-{method.EndLine} in {Bold(signature)}");
                }
                writer.WriteLine();
            }
        }

        if (subMethodGroups.Count > 0)
        {
            for (int i = 0; i < subMethodGroups.Count; i++)
            {
                var group = subMethodGroups[i];
                int statementCount = group.StatementCount;

                writer.WriteLine(Yellow($"Cluster #{clusterIdx++} [Sub-Method, {statementCount} consecutive statements]:"));
                foreach (var occ in group.Occurrences)
                {
                    var relPath = FormatPath(occ.FilePath, baseDirectory);
                    writer.WriteLine($"  • {Cyan(relPath)}:{occ.StartLine}-{occ.EndLine} in {Bold(occ.MethodName)}");
                }
                writer.WriteLine();
            }
        }

        writer.WriteLine(Bold("Actionable Instructions for AI Agent:"));
        writer.WriteLine("  1. Inspect the duplicate methods / statement blocks listed in each cluster above.");
        writer.WriteLine("  2. Extract the identical structural logic into a shared helper method or service.");
        writer.WriteLine("  3. Replace the duplicate code with a call to the extracted helper.");
        writer.WriteLine("  4. To suppress: Add '[SuppressMessage(\"CodeCondenser\", \"Duplication\", Justification = \"<why>\")]' or '// codecondenser:ignore-duplicate' on the method.");
        writer.WriteLine("  5. Re-run 'codecondenser --incremental' to confirm the violation is resolved.");
        writer.WriteLine(Red(Bold("========================================================================")));
        writer.WriteLine();
    }

    public void PrintAiActionableIncrementalFeedback(
        TextWriter writer,
        IReadOnlyList<CodeCloneGroup> cloneGroups,
        IReadOnlyList<SubMethodCloneGroup> subMethodGroups,
        IReadOnlyList<VerboseComment> verboseComments,
        string baseDirectory,
        int duplicationTolerance,
        bool hasDuplicationViolation = true,
        bool hasCommentViolation = false,
        IReadOnlyList<RepeatedStringGroup>? repeatedStrings = null,
        int? repeatedStringTolerance = null,
        bool hasRepeatedStringViolation = false)
    {
        ArgumentNullException.ThrowIfNull(writer);
        cloneGroups ??= [];
        subMethodGroups ??= [];
        verboseComments ??= [];
        repeatedStrings ??= [];

        bool hasComments = verboseComments.Count > 0;
        bool hasStrings = repeatedStrings.Count > 0;
        int totalCloneCount = cloneGroups.Count + subMethodGroups.Count;

        if (hasRepeatedStringViolation && !hasDuplicationViolation && !hasComments)
        {
            writer.WriteLine();
            writer.WriteLine(Red(Bold("========================================================================")));
            writer.WriteLine(Red(Bold("[AI AGENT ACTION REQUIRED] Repeated Inline String Policy Violation (Exit Code: 2)")));
            writer.WriteLine(Red(Bold("========================================================================")));
            writer.WriteLine($"Incremental analysis detected {Bold(repeatedStrings.Count.ToString())} new repeated string cluster(s) exceeding tolerance margin (allowed: {repeatedStringTolerance ?? 0}).");
            writer.WriteLine();

            for (int i = 0; i < repeatedStrings.Count; i++)
            {
                var group = repeatedStrings[i];
                writer.WriteLine(Yellow($"Cluster #{i + 1} [{group.Length} chars, {group.Occurrences.Count} occurrences]:"));
                writer.WriteLine($"  Value: \"{Bold(TruncatePreview(group.Value.Replace("\r\n", " ").Replace("\n", " "), 80))}\"");
                foreach (var occ in group.Occurrences)
                {
                    var relPath = FormatPath(occ.FilePath, baseDirectory);
                    var memberInfo = !string.IsNullOrEmpty(occ.EnclosingMember) ? $" in {Bold(occ.EnclosingMember)}" : string.Empty;
                    writer.WriteLine($"  • {Cyan(relPath)}:{occ.StartLine}:{occ.StartColumn}{memberInfo}");
                }
                writer.WriteLine(Dim($"  Suggested Constant: {group.GenerateTypicalUsage()}"));
                writer.WriteLine();
            }

            writer.WriteLine(Bold("Actionable Instructions for AI Agent:"));
            writer.WriteLine("  1. Inspect the repeated inline string literals listed above.");
            writer.WriteLine("  2. Extract duplicate strings into a shared constant (e.g. in a static constants class).");
            writer.WriteLine("  3. Replace the inline string literals with references to the constant.");
            writer.WriteLine("  4. To suppress: Add '[SuppressMessage(\"CodeCondenser\", \"RepeatedString\")]' or '// codecondenser:ignore-string'.");
            writer.WriteLine("  5. Re-run 'codecondenser --incremental' to confirm the violation is resolved.");
            writer.WriteLine(Red(Bold("========================================================================")));
            writer.WriteLine();
            return;
        }

        if (hasDuplicationViolation && !hasComments && !hasStrings)
        {
            PrintAiActionableDuplicationFeedback(writer, cloneGroups, subMethodGroups, baseDirectory, duplicationTolerance);
            return;
        }

        if (!hasDuplicationViolation && hasComments && !hasStrings)
        {
            writer.WriteLine();
            writer.WriteLine(Red(Bold("========================================================================")));
            var title = hasCommentViolation
                ? "[AI AGENT ACTION REQUIRED] Verbose Comments Policy Violation (Exit Code: 2)"
                : "[AI AGENT ACTION REQUIRED] Verbose Comments Detected";
            writer.WriteLine(Red(Bold(title)));
            writer.WriteLine(Red(Bold("========================================================================")));
            writer.WriteLine($"Incremental analysis detected {Bold(verboseComments.Count.ToString())} new verbose comment(s) exceeding length thresholds.");
            writer.WriteLine();

            for (int i = 0; i < verboseComments.Count; i++)
            {
                var comment = verboseComments[i];
                var relPath = FormatPath(comment.FilePath, baseDirectory);
                writer.WriteLine(Yellow($"Comment #{i + 1} [{comment.Kind}, {comment.OriginalText.Trim().Length} chars]:"));
                writer.WriteLine($"  • {Cyan(relPath)}:{comment.StartLine}-{comment.EndLine}");
                writer.WriteLine($"    {Red(TruncatePreview(comment.OriginalText.Trim(), 100))}");
                writer.WriteLine();
            }

            writer.WriteLine(Bold("Actionable Instructions for AI Agent:"));
            writer.WriteLine("  1. Inspect the verbose comment(s) listed above.");
            writer.WriteLine("  2. Condense: Shorten or rewrite the comment text concisely to fit within 120 columns.");
            writer.WriteLine("  3. Suppress: If extensive documentation is intentional:");
            writer.WriteLine("     • XML doc comments: Add 'condense=\"no\"' attribute to XML tag (e.g. <summary condense=\"no\">).");
            writer.WriteLine("     • Regular comments: Add '// codecondenser:ignore' or '// nocondense' on the preceding line or inline.");
            writer.WriteLine("  4. Re-run 'codecondenser --incremental' to confirm the violation is resolved.");
            writer.WriteLine(Red(Bold("========================================================================")));
            writer.WriteLine();
            return;
        }

        writer.WriteLine();
        writer.WriteLine(Red(Bold("========================================================================")));
        var multiTitle = (hasDuplicationViolation || hasCommentViolation || hasRepeatedStringViolation)
            ? "[AI AGENT ACTION REQUIRED] Incremental Policy Violations (Exit Code: 2)"
            : "[AI AGENT ACTION REQUIRED] Incremental Policy Violations";
        writer.WriteLine(Red(Bold(multiTitle)));
        writer.WriteLine(Red(Bold("========================================================================")));
        var counts = new List<string>();
        if (totalCloneCount > 0) counts.Add($"{Bold(totalCloneCount.ToString())} new duplication cluster(s)");
        if (verboseComments.Count > 0) counts.Add($"{Bold(verboseComments.Count.ToString())} new verbose comment(s)");
        if (repeatedStrings.Count > 0) counts.Add($"{Bold(repeatedStrings.Count.ToString())} new repeated string cluster(s)");

        string summaryText = counts.Count switch
        {
            1 => counts[0],
            2 => $"{counts[0]} and {counts[1]}",
            _ => $"{string.Join(", ", counts.Take(counts.Count - 1))} and {counts[^1]}"
        };
        writer.WriteLine($"Incremental analysis detected {summaryText}.");
        writer.WriteLine();

        int clusterIdx = 1;
        if (cloneGroups.Count > 0)
        {
            writer.WriteLine(Bold("Code Duplication Clusters:"));
            for (int i = 0; i < cloneGroups.Count; i++)
            {
                var group = cloneGroups[i];
                var shortHash = group.NormalizedHash.Length > 12 ? group.NormalizedHash[..12] : group.NormalizedHash;
                writer.WriteLine(Yellow($"  Cluster #{clusterIdx++} [Whole-Method, AST Hash: {shortHash}..., {group.StatementCount} statements]:"));
                foreach (var method in group.Methods)
                {
                    var relPath = FormatPath(method.FilePath, baseDirectory);
                    var signature = !string.IsNullOrWhiteSpace(method.Signature) ? method.Signature : method.MethodName;
                    writer.WriteLine($"    • {Cyan(relPath)}:{method.StartLine}-{method.EndLine} in {Bold(signature)}");
                }
            }
            writer.WriteLine();
        }

        if (subMethodGroups.Count > 0)
        {
            if (cloneGroups.Count == 0) writer.WriteLine(Bold("Sub-Method Duplication Clusters:"));
            for (int i = 0; i < subMethodGroups.Count; i++)
            {
                var group = subMethodGroups[i];
                writer.WriteLine(Yellow($"  Cluster #{clusterIdx++} [Sub-Method, {group.StatementCount} consecutive statements]:"));
                foreach (var occ in group.Occurrences)
                {
                    var relPath = FormatPath(occ.FilePath, baseDirectory);
                    writer.WriteLine($"    • {Cyan(relPath)}:{occ.StartLine}-{occ.EndLine} in {Bold(occ.MethodName)}");
                }
            }
            writer.WriteLine();
        }

        if (repeatedStrings.Count > 0)
        {
            writer.WriteLine(Bold("Repeated Inline Strings:"));
            for (int i = 0; i < repeatedStrings.Count; i++)
            {
                var strGroup = repeatedStrings[i];
                writer.WriteLine(Yellow($"  String #{i + 1} [{strGroup.Length} chars, {strGroup.Occurrences.Count} occurrences]:"));
                writer.WriteLine($"    • Value: \"{TruncatePreview(strGroup.Value.Replace("\r\n", " ").Replace("\n", " "), 80)}\"");
                foreach (var occ in strGroup.Occurrences)
                {
                    var relPath = FormatPath(occ.FilePath, baseDirectory);
                    writer.WriteLine($"      - {Cyan(relPath)}:{occ.StartLine}:{occ.StartColumn}");
                }
            }
            writer.WriteLine();
        }

        if (verboseComments.Count > 0)
        {
            writer.WriteLine(Bold("Verbose Comments:"));
            for (int i = 0; i < verboseComments.Count; i++)
            {
                var comment = verboseComments[i];
                var relPath = FormatPath(comment.FilePath, baseDirectory);
                writer.WriteLine(Yellow($"  Comment #{i + 1} [{comment.Kind}, {comment.OriginalText.Trim().Length} chars]:"));
                writer.WriteLine($"    • {Cyan(relPath)}:{comment.StartLine}-{comment.EndLine}");
                writer.WriteLine($"      {Red(TruncatePreview(comment.OriginalText.Trim(), 100))}");
            }
            writer.WriteLine();
        }

        writer.WriteLine(Bold("Actionable Instructions for AI Agent:"));
        if (totalCloneCount > 0)
        {
            writer.WriteLine("  Code Duplication:");
            writer.WriteLine("    1. Inspect the duplicate methods / statement blocks listed in each cluster above.");
            writer.WriteLine("    2. Extract the identical structural logic into a shared helper method or service.");
            writer.WriteLine("    3. Replace the duplicate code with a call to the extracted helper.");
            writer.WriteLine("    4. To suppress: Add '[SuppressMessage(\"CodeCondenser\", \"Duplication\", Justification = \"<why>\")]' or '// codecondenser:ignore-duplicate' on the method.");
        }
        if (repeatedStrings.Count > 0)
        {
            writer.WriteLine("  Repeated Inline Strings:");
            writer.WriteLine("    1. Inspect the repeated strings listed above.");
            writer.WriteLine("    2. Extract each into a shared constant (e.g. 'internal const string Name = \"...\";' in a static constants class).");
            writer.WriteLine("    3. Replace inline occurrences with references to the constant.");
            writer.WriteLine("    4. To suppress: Add '[SuppressMessage(\"CodeCondenser\", \"RepeatedString\")]' or '// codecondenser:ignore-string'.");
        }
        if (verboseComments.Count > 0)
        {
            writer.WriteLine("  Verbose Comments:");
            writer.WriteLine("    1. Inspect the verbose comment(s) listed above.");
            writer.WriteLine("    2. Condense: Shorten or rewrite comment text concisely to fit within 120 columns.");
            writer.WriteLine("    3. To suppress: Add 'condense=\"no\"' attribute (XML doc) or '// codecondenser:ignore' (preceding/inline).");
        }
        writer.WriteLine("  Verification:");
        writer.WriteLine("    • Re-run 'codecondenser --incremental' to confirm all violations are resolved.");
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

    public void PrintSkillFile(TextWriter writer, Skills.SkillFileResult fileResult, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(fileResult);

        var relPath = FormatPath(fileResult.FullPath, baseDirectory);
        var statusTag = fileResult.Status switch
        {
            Skills.SkillFileStatus.Created => Green("[Created]   "),
            Skills.SkillFileStatus.Updated => Cyan("[Updated]   "),
            Skills.SkillFileStatus.UpToDate => Dim("[Up to date]"),
            Skills.SkillFileStatus.Removed => Yellow("[Removed]   "),
            _ => $"[{fileResult.Status}]"
        };

        writer.WriteLine($"  {statusTag} {relPath}");
    }

    public void PrintSkillsSummary(TextWriter writer, Skills.SkillInstallResult result, bool dryRun)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(result);

        writer.WriteLine();
        if (dryRun)
        {
            writer.WriteLine(Yellow("(Dry run mode - no files were written to disk)"));
        }

        if (result.IsAllUpToDate)
        {
            writer.WriteLine(Green($"✓ Skills are already up to date ({result.UpToDateCount} file(s) checked)."));
        }
        else
        {
            var parts = new List<string>();
            if (result.CreatedCount > 0) parts.Add($"{result.CreatedCount} created");
            if (result.UpdatedCount > 0) parts.Add($"{result.UpdatedCount} updated");
            if (result.UpToDateCount > 0) parts.Add($"{result.UpToDateCount} up to date");
            if (result.RemovedCount > 0) parts.Add($"{result.RemovedCount} removed");

            var summaryText = string.Join(", ", parts);
            writer.WriteLine(Green($"✓ Skills successfully {(dryRun ? "evaluated" : "synchronized")} ({summaryText})."));
        }
    }

    public void PrintMeasurementResult(
        TextWriter writer,
        CodeMeasurementResult result,
        string targetPath,
        string estimatorName)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(result);

        writer.WriteLine(Dim("--------------------------------------------------------------------------------"));
        writer.WriteLine(Bold($"Code Token Measurement (Estimator: {estimatorName}):"));
        writer.WriteLine($"  Target:            {Path.GetFullPath(targetPath)}");
        if (result.SkippedGeneratedFilesCount > 0)
        {
            writer.WriteLine($"  Skipped Generated: {result.SkippedGeneratedFilesCount}");
        }
        writer.WriteLine();

        writer.WriteLine(Bold($"{"Category".PadRight(16)} {"Files".PadLeft(8)} {"Code Tokens".PadLeft(16)} {"Comment Tokens".PadLeft(18)} {"Total Tokens".PadLeft(16)}"));
        writer.WriteLine(Dim("--------------------------------------------------------------------------------"));

        var inv = CultureInfo.InvariantCulture;
        writer.WriteLine($"{Cyan("Production".PadRight(16))} {result.Production.FilesCount.ToString("N0", inv).PadLeft(8)} {result.Production.CodeTokens.ToString("N0", inv).PadLeft(16)} {result.Production.CommentTokens.ToString("N0", inv).PadLeft(18)} {Bold(result.Production.TotalTokens.ToString("N0", inv).PadLeft(16))}");

        writer.WriteLine($"{Cyan("Tests".PadRight(16))} {result.Tests.FilesCount.ToString("N0", inv).PadLeft(8)} {result.Tests.CodeTokens.ToString("N0", inv).PadLeft(16)} {result.Tests.CommentTokens.ToString("N0", inv).PadLeft(18)} {Bold(result.Tests.TotalTokens.ToString("N0", inv).PadLeft(16))}");

        writer.WriteLine(Dim("--------------------------------------------------------------------------------"));

        writer.WriteLine($"{Bold("Total".PadRight(16))} {Bold(result.Total.FilesCount.ToString("N0", inv).PadLeft(8))} {Bold(result.Total.CodeTokens.ToString("N0", inv).PadLeft(16))} {Bold(result.Total.CommentTokens.ToString("N0", inv).PadLeft(18))} {Green(Bold(result.Total.TotalTokens.ToString("N0", inv).PadLeft(16)))}");

        writer.WriteLine(Dim("--------------------------------------------------------------------------------"));
    }
}


