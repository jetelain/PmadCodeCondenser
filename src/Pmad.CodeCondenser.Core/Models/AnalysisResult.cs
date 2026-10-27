using Pmad.CodeCondenser.Core.Comments;

namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Aggregates all results from a CodeCondenser analysis run.
/// </summary>
public sealed class AnalysisResult
{
    public required IReadOnlyList<VerboseComment> VerboseComments { get; init; }
    public required IReadOnlyList<CodeCloneGroup> CloneGroups { get; init; }
    public required IReadOnlyDictionary<string, string> SummarizedComments { get; init; }
    public required int AnalyzedFilesCount { get; init; }
    public required int TotalMethodsScanned { get; init; }
    public int ProductionFilesCount { get; init; }
    public int TestFilesCount { get; init; }
    public int SkippedGeneratedFilesCount { get; init; }
    public IReadOnlyList<SubMethodCloneGroup> SubMethodCloneGroups { get; init; } = [];
    public IReadOnlyList<RepeatedStringGroup> RepeatedStringGroups { get; init; } = [];
    public IReadOnlyList<TypeFileAnalysis> TypeFileIssues { get; init; } = [];
    public IReadOnlyList<LinesOfCodeViolation> LinesOfCodeViolations { get; init; } = [];

    /// <summary>
    /// Gets the total character gain achieved by compacting repeated inline strings into constants.
    /// </summary>
    public int TotalRepeatedStringGainCharacters =>
        Pmad.CodeCondenser.Core.Strings.RepeatedStringGainEstimator.EstimateTotalGainCharacters(RepeatedStringGroups);

    /// <summary>
    /// Gets the total estimated token gain achieved by compacting repeated inline strings into constants.
    /// </summary>
    public int TotalRepeatedStringGainTokens =>
        Pmad.CodeCondenser.Core.Strings.RepeatedStringGainEstimator.EstimateTotalGainTokens(RepeatedStringGroups);

    /// <summary>
    /// Gets the total character gain (reduction in characters) achieved by replacing all verbose comments with their summaries.
    /// Returns 0 if no comments were summarized.
    /// </summary>
    public int TotalCommentGainCharacters => CalculateCommentGain().GainCharacters;

    /// <summary>
    /// Gets the overall comment reduction percentage achieved by replacing all verbose comments with their summaries.
    /// Returns 0 if no comments were summarized.
    /// </summary>
    public int TotalCommentReductionPercent => CalculateCommentGain().ReductionPercent;

    /// <summary>
    /// Gets the total estimated token gain achieved by replacing all verbose comments with their summaries.
    /// Returns 0 if no comments were summarized.
    /// </summary>
    public int TotalCommentGainTokens => CalculateCommentGain().GainTokens;

    /// <summary>
    /// Calculates the total original characters, condensed characters, character gain, and reduction percentage
    /// for all verbose comments that have summaries, along with estimated token metrics.
    /// </summary>
    public CommentGainResult CalculateCommentGain(Pmad.AI.Tokens.HeuristicTokenEstimator? estimator = null)
    {
        return CommentVerbosityHelper.CalculateTotalGain(VerboseComments, SummarizedComments, estimator);
    }
}

