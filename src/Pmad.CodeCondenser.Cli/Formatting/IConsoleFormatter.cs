using Pmad.AI.Tokens;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Cli.Formatting;

/// <summary>
/// Abstraction for rendering formatted console output, diff summaries, and alerts.
/// </summary>
public interface IConsoleFormatter
{
    void PrintBanner(TextWriter writer);

    void PrintCloneGroups(
        TextWriter writer,
        IReadOnlyList<CodeCloneGroup> cloneGroups,
        string baseDirectory,
        HeuristicTokenEstimator? estimator = null,
        int? totalCount = null,
        int? totalProductionCount = null,
        int? totalTestCount = null);

    void PrintSubMethodCloneGroups(
        TextWriter writer,
        IReadOnlyList<SubMethodCloneGroup> subMethodGroups,
        string baseDirectory,
        HeuristicTokenEstimator? estimator = null,
        int? totalCount = null);

    void PrintVerboseComments(
        TextWriter writer,
        IReadOnlyList<VerboseComment> comments,
        IReadOnlyDictionary<string, string> summaries,
        string baseDirectory,
        HeuristicTokenEstimator? estimator = null,
        int? totalCount = null);

    void PrintRepeatedStringGroups(
        TextWriter writer,
        IReadOnlyList<RepeatedStringGroup> repeatedStrings,
        string baseDirectory,
        HeuristicTokenEstimator? estimator = null,
        int? totalCount = null,
        int? totalProductionCount = null,
        int? totalTestCount = null);

    void PrintTypeFileIssues(
        TextWriter writer,
        IReadOnlyList<TypeFileAnalysis> typeIssues,
        string baseDirectory,
        int lineTolerance = 1,
        int? totalCount = null);

    void PrintLinesOfCodeViolations(
        TextWriter writer,
        IReadOnlyList<LinesOfCodeViolation> violations,
        string baseDirectory,
        int? totalCount = null);

    void PrintPredicateCloneGroups(
        TextWriter writer,
        IReadOnlyList<PredicateCloneGroup> predicateGroups,
        string baseDirectory,
        HeuristicTokenEstimator? estimator = null,
        int? totalCount = null,
        int? totalProductionCount = null,
        int? totalTestCount = null);

    void PrintCondensationCandidates(
        TextWriter writer,
        IReadOnlyList<CondensationCandidate> candidates,
        string baseDirectory,
        HeuristicTokenEstimator? estimator = null,
        int? totalCount = null);

    void PrintSummary(
        TextWriter writer,
        int filesAnalyzed,
        int? methodsScanned,
        int? cloneGroupsCount,
        int? verboseCommentsCount,
        string mode,
        int productionFiles = 0,
        int testFiles = 0,
        int skippedGenerated = 0,
        int? productionClones = null,
        int? testClones = null,
        int? subMethodClones = null,
        int? commentGainChars = null,
        int? commentReductionPercent = null,
        int? commentGainTokens = null,
        int? cloneGainTokens = null,
        int? repeatedStringsCount = null,
        int? repeatedStringGainChars = null,
        int? repeatedStringGainTokens = null,
        int? typeIssuesCount = null,
        int? linesOfCodeViolationsCount = null,
        int? predicateGroupsCount = null,
        int? predicateGainChars = null,
        int? predicateGainTokens = null,
        int? condensationCandidatesCount = null,
        int? condensationGainChars = null,
        int? condensationGainTokens = null);

    void PrintFileApplied(TextWriter writer, string filePath, string baseDirectory);

    void PrintSnapshotSaved(TextWriter writer, string snapshotPath);

    void PrintError(TextWriter writer, string message);

    void PrintSuccess(TextWriter writer, string message);

    void PrintWarning(TextWriter writer, string message);

    void PrintInfo(TextWriter writer, string message);

    void PrintCommentProgress(TextWriter writer, int completed, int total, string? currentItem = null);

    void PrintAiActionableIncrementalFeedback(
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
        bool hasRepeatedStringViolation = false,
        IReadOnlyList<TypeFileAnalysis>? typeIssues = null,
        int? typeTolerance = null,
        bool hasTypeViolation = false,
        IReadOnlyList<LinesOfCodeViolation>? linesOfCodeViolations = null,
        int? linesOfCodeTolerance = null,
        bool hasLinesOfCodeViolation = false,
        IReadOnlyList<PredicateCloneGroup>? predicateGroups = null,
        int? predicateTolerance = null,
        bool hasPredicateViolation = false);


    void PrintSkillFile(TextWriter writer, Skills.SkillFileResult fileResult, string baseDirectory);

    void PrintSkillsSummary(TextWriter writer, Skills.SkillInstallResult result, bool dryRun);

    void PrintMeasurementResult(
        TextWriter writer,
        CodeMeasurementResult result,
        string targetPath,
        string estimatorName);
}


