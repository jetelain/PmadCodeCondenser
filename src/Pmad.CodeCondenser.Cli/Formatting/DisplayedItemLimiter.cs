using Pmad.AI.Tokens;
using Pmad.CodeCondenser.Core.Clones;
using Pmad.CodeCondenser.Core.Comments;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Cli.Formatting;

/// <summary>
/// Encapsulates filtered items for console display according to caller-defined limits.
/// </summary>
public sealed class LimitedDisplayItems
{
    public IReadOnlyList<CodeCloneGroup> CloneGroups { get; init; } = [];
    public IReadOnlyList<SubMethodCloneGroup> SubMethodCloneGroups { get; init; } = [];
    public IReadOnlyList<RepeatedStringGroup> RepeatedStringGroups { get; init; } = [];
    public IReadOnlyList<PredicateCloneGroup> PredicateCloneGroups { get; init; } = [];
    public IReadOnlyList<VerboseComment> VerboseComments { get; init; } = [];
    public IReadOnlyList<TypeFileAnalysis> TypeFileIssues { get; init; } = [];
    public IReadOnlyList<LinesOfCodeViolation> LinesOfCodeViolations { get; init; } = [];
    public IReadOnlyList<CondensationCandidate> CondensationCandidates { get; init; } = [];

    public int TotalItemCount { get; init; }
    public int DisplayedItemCount { get; init; }
    public bool IsLimited { get; init; }
}

/// <summary>
/// Limits and prioritizes displayed items across all analysis categories by largest estimated benefit.
/// </summary>
public static class DisplayedItemLimiter
{
    public static LimitedDisplayItems SelectItems(
        AnalysisResult result,
        CodeCondenserSettings settings,
        string baseDirectory,
        HeuristicTokenEstimator? estimator = null,
        bool includeComments = true)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(settings);

        var effEstimator = estimator ?? HeuristicTokenEstimator.Default;

        bool isDuplicationAnalyzed = settings.EnableDuplicationDetection;
        bool isSubMethodAnalyzed = settings.EnableSubMethodCloneDetection;
        bool isStringsAnalyzed = settings.EnableRepeatedStringDetection;
        bool isPredicatesAnalyzed = settings.EnablePredicateCloneDetection;
        bool isCommentsAnalyzed = includeComments && settings.GetEffectiveSummaryMode() != SummaryMode.Disabled && settings.EnableCommentSummarization;
        bool isTypeAnalyzed = settings.EnableTypeSplitting;
        bool isLocAnalyzed = settings.EnableLinesOfCodeDetection;
        bool isPatternsAnalyzed = settings.EnablePatternCondensation;

        var candidateCloneGroups = isDuplicationAnalyzed ? result.CloneGroups : [];
        var candidateSubMethodGroups = isSubMethodAnalyzed ? result.SubMethodCloneGroups : [];
        var candidateStringGroups = isStringsAnalyzed ? result.RepeatedStringGroups : [];
        var candidatePredicateGroups = isPredicatesAnalyzed ? result.PredicateCloneGroups : [];
        var candidateComments = isCommentsAnalyzed ? result.VerboseComments : [];
        var candidateTypeIssues = isTypeAnalyzed ? result.TypeFileIssues.Where(i => i.HasViolation).ToList() : [];
        var candidateLocViolations = isLocAnalyzed ? result.LinesOfCodeViolations : [];
        var candidatePatterns = isPatternsAnalyzed ? result.CondensationCandidates : [];

        int totalCount = candidateCloneGroups.Count
            + candidateSubMethodGroups.Count
            + candidateStringGroups.Count
            + candidatePredicateGroups.Count
            + candidateComments.Count
            + candidateTypeIssues.Count
            + candidateLocViolations.Count
            + candidatePatterns.Count;

        int? limit = settings.Limit;
        bool isLimited = limit.HasValue && limit.Value >= 0 && limit.Value < totalCount;

        var fileCache = new Dictionary<string, string[]?>(StringComparer.OrdinalIgnoreCase);
        var allCandidates = new List<(object Item, int Benefit, int CategoryPriority, int Index)>(totalCount);

        for (int i = 0; i < candidateCloneGroups.Count; i++)
        {
            var item = candidateCloneGroups[i];
            allCandidates.Add((item, GetCloneBenefit(item, effEstimator, baseDirectory, fileCache), 1, i));
        }

        for (int i = 0; i < candidateSubMethodGroups.Count; i++)
        {
            var item = candidateSubMethodGroups[i];
            allCandidates.Add((item, GetSubMethodBenefit(item, effEstimator, baseDirectory, fileCache), 2, i));
        }

        for (int i = 0; i < candidateStringGroups.Count; i++)
        {
            var item = candidateStringGroups[i];
            allCandidates.Add((item, GetRepeatedStringBenefit(item, effEstimator), 3, i));
        }

        for (int i = 0; i < candidatePredicateGroups.Count; i++)
        {
            var item = candidatePredicateGroups[i];
            allCandidates.Add((item, GetPredicateBenefit(item, effEstimator), 4, i));
        }

        for (int i = 0; i < candidateComments.Count; i++)
        {
            var item = candidateComments[i];
            allCandidates.Add((item, GetCommentBenefit(item, result.SummarizedComments, effEstimator), 5, i));
        }

        for (int i = 0; i < candidateTypeIssues.Count; i++)
        {
            var item = candidateTypeIssues[i];
            allCandidates.Add((item, GetTypeIssueBenefit(item), 6, i));
        }

        for (int i = 0; i < candidateLocViolations.Count; i++)
        {
            var item = candidateLocViolations[i];
            allCandidates.Add((item, GetLocViolationBenefit(item), 7, i));
        }

        for (int i = 0; i < candidatePatterns.Count; i++)
        {
            var item = candidatePatterns[i];
            allCandidates.Add((item, GetPatternBenefit(item, effEstimator), 8, i));
        }

        var sorted = allCandidates
            .OrderByDescending(c => c.Benefit)
            .ThenBy(c => c.CategoryPriority)
            .ThenBy(c => c.Index);

        var chosenItems = isLimited
            ? sorted.Take(limit!.Value).ToList()
            : sorted.ToList();

        var selectedClones = chosenItems.Where(t => t.Item is CodeCloneGroup).Select(t => (CodeCloneGroup)t.Item).ToList();
        var selectedSubMethods = chosenItems.Where(t => t.Item is SubMethodCloneGroup).Select(t => (SubMethodCloneGroup)t.Item).ToList();
        var selectedStrings = chosenItems.Where(t => t.Item is RepeatedStringGroup).Select(t => (RepeatedStringGroup)t.Item).ToList();
        var selectedPredicates = chosenItems.Where(t => t.Item is PredicateCloneGroup).Select(t => (PredicateCloneGroup)t.Item).ToList();
        var selectedComments = chosenItems.Where(t => t.Item is VerboseComment).Select(t => (VerboseComment)t.Item).ToList();
        var selectedTypes = chosenItems.Where(t => t.Item is TypeFileAnalysis).Select(t => (TypeFileAnalysis)t.Item).ToList();
        var selectedLocs = chosenItems.Where(t => t.Item is LinesOfCodeViolation).Select(t => (LinesOfCodeViolation)t.Item).ToList();
        var selectedPatterns = chosenItems.Where(t => t.Item is CondensationCandidate).Select(t => (CondensationCandidate)t.Item).ToList();

        int displayedCount = chosenItems.Count;

        return new LimitedDisplayItems
        {
            CloneGroups = selectedClones,
            SubMethodCloneGroups = selectedSubMethods,
            RepeatedStringGroups = selectedStrings,
            PredicateCloneGroups = selectedPredicates,
            VerboseComments = selectedComments,
            TypeFileIssues = selectedTypes,
            LinesOfCodeViolations = selectedLocs,
            CondensationCandidates = selectedPatterns,
            TotalItemCount = totalCount,
            DisplayedItemCount = displayedCount,
            IsLimited = isLimited
        };
    }

    public static int GetPatternBenefit(CondensationCandidate candidate, HeuristicTokenEstimator estimator)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        int gain = candidate.EstimateGainTokens(estimator);
        if (gain > 0) return gain;
        return Math.Max(1, candidate.CharacterGain / 4);
    }

    public static int GetCloneBenefit(
        CodeCloneGroup group,
        HeuristicTokenEstimator estimator,
        string baseDirectory,
        Dictionary<string, string[]?>? cache = null)
    {
        int gain = CloneGainEstimator.EstimateGainTokens(group, estimator, baseDirectory, cache);
        if (gain > 0) return gain;
        return Math.Max(1, group.StatementCount * 10 * Math.Max(1, group.Methods.Count - 1));
    }

    public static int GetSubMethodBenefit(
        SubMethodCloneGroup group,
        HeuristicTokenEstimator estimator,
        string baseDirectory,
        Dictionary<string, string[]?>? cache = null)
    {
        int gain = CloneGainEstimator.EstimateGainTokens(group, estimator, baseDirectory, cache);
        if (gain > 0) return gain;
        return Math.Max(1, group.StatementCount * 10 * Math.Max(1, group.Occurrences.Count - 1));
    }

    public static int GetRepeatedStringBenefit(RepeatedStringGroup group, HeuristicTokenEstimator estimator)
    {
        int gain = group.EstimateGainTokens(estimator);
        if (gain > 0) return gain;
        return Math.Max(1, group.EstimateGainCharacters() / 4);
    }

    public static int GetPredicateBenefit(PredicateCloneGroup group, HeuristicTokenEstimator estimator)
    {
        int gain = group.EstimateGainTokens(estimator);
        if (gain > 0) return gain;
        return Math.Max(1, group.EstimateGainCharacters() / 4);
    }

    public static int GetCommentBenefit(VerboseComment comment, IReadOnlyDictionary<string, string> summaries, HeuristicTokenEstimator estimator)
    {
        if (summaries.TryGetValue(comment.ContentHash, out var summary) && !string.IsNullOrWhiteSpace(summary))
        {
            if (CommentVerbosityHelper.IsUselessComment(summary))
            {
                return Math.Max(1, estimator.Estimate(comment.OriginalText.Trim().AsSpan()));
            }
            if (CommentVerbosityHelper.IsMoreVerbose(summary, comment))
            {
                return 0;
            }
            int origTokens = estimator.Estimate(comment.OriginalText.Trim().AsSpan());
            int condTokens = estimator.Estimate(summary.Trim().AsSpan());
            return Math.Max(0, origTokens - condTokens);
        }
        return Math.Max(1, estimator.Estimate(comment.OriginalText.Trim().AsSpan()));
    }

    public static int GetTypeIssueBenefit(TypeFileAnalysis issue)
    {
        if (!issue.HasViolation) return 0;
        int loc = issue.TypesToExtract.Sum(t => t.LinesOfCode);
        return Math.Max(1, loc * 5);
    }

    public static int GetLocViolationBenefit(LinesOfCodeViolation violation)
    {
        int excess = Math.Max(1, violation.LinesOfCode - violation.Threshold);
        return excess * 5;
    }
}
