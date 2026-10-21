using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Cli.Formatting;

/// <summary>
/// Abstraction for rendering formatted console output, diff summaries, and alerts.
/// </summary>
public interface IConsoleFormatter
{
    void PrintBanner(TextWriter writer);

    void PrintCloneGroups(TextWriter writer, IReadOnlyList<CodeCloneGroup> cloneGroups, string baseDirectory);

    void PrintSubMethodCloneGroups(TextWriter writer, IReadOnlyList<SubMethodCloneGroup> subMethodGroups, string baseDirectory)
    {
    }

    void PrintVerboseComments(TextWriter writer, IReadOnlyList<VerboseComment> comments, IReadOnlyDictionary<string, string> summaries, string baseDirectory);

    void PrintSummary(TextWriter writer, int filesAnalyzed, int methodsScanned, int cloneGroupsCount, int verboseCommentsCount, string mode);

    void PrintSummary(
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
        int testClones,
        int subMethodClones = 0)
    {
        PrintSummary(writer, filesAnalyzed, methodsScanned, cloneGroupsCount, verboseCommentsCount, mode);
    }

    void PrintFileApplied(TextWriter writer, string filePath, string baseDirectory);

    void PrintSnapshotSaved(TextWriter writer, string snapshotPath);

    void PrintError(TextWriter writer, string message);

    void PrintSuccess(TextWriter writer, string message);

    void PrintWarning(TextWriter writer, string message);

    void PrintInfo(TextWriter writer, string message);

    void PrintCommentProgress(TextWriter writer, int completed, int total, string? currentItem = null);

    void PrintAiActionableDuplicationFeedback(TextWriter writer, IReadOnlyList<CodeCloneGroup> cloneGroups, string baseDirectory, int tolerance);

    void PrintAiActionableDuplicationFeedback(
        TextWriter writer,
        IReadOnlyList<CodeCloneGroup> cloneGroups,
        IReadOnlyList<SubMethodCloneGroup> subMethodGroups,
        string baseDirectory,
        int tolerance)
    {
        PrintAiActionableDuplicationFeedback(writer, cloneGroups, baseDirectory, tolerance);
    }

    void PrintAiActionableIncrementalFeedback(
        TextWriter writer,
        IReadOnlyList<CodeCloneGroup> cloneGroups,
        IReadOnlyList<SubMethodCloneGroup> subMethodGroups,
        IReadOnlyList<VerboseComment> verboseComments,
        string baseDirectory,
        int duplicationTolerance,
        bool hasDuplicationViolation = true,
        bool hasCommentViolation = false)
    {
        if (hasDuplicationViolation)
        {
            PrintAiActionableDuplicationFeedback(writer, cloneGroups, subMethodGroups, baseDirectory, duplicationTolerance);
        }
    }

    void PrintSkillFile(TextWriter writer, Skills.SkillFileResult fileResult, string baseDirectory)
    {
    }

    void PrintSkillsSummary(TextWriter writer, Skills.SkillInstallResult result, bool dryRun)
    {
    }
}

