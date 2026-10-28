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
        HeuristicTokenEstimator? estimator = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(settings);

        var effEstimator = estimator ?? HeuristicTokenEstimator.Default;

        bool isDuplicationAnalyzed = settings.EnableDuplicationDetection;
        bool isSubMethodAnalyzed = settings.EnableSubMethodCloneDetection;
        bool isStringsAnalyzed = settings.EnableRepeatedStringDetection;
        bool isPredicatesAnalyzed = settings.EnablePredicateCloneDetection;
        bool isCommentsAnalyzed = settings.GetEffectiveSummaryMode() != SummaryMode.Disabled && settings.EnableCommentSummarization;
        bool isTypeAnalyzed = settings.EnableTypeSplitting;
        bool isLocAnalyzed = settings.EnableLinesOfCodeDetection;

        var candidateCloneGroups = isDuplicationAnalyzed ? result.CloneGroups : [];
        var candidateSubMethodGroups = isSubMethodAnalyzed ? result.SubMethodCloneGroups : [];
        var candidateStringGroups = isStringsAnalyzed ? result.RepeatedStringGroups : [];
        var candidatePredicateGroups = isPredicatesAnalyzed ? result.PredicateCloneGroups : [];
        var candidateComments = isCommentsAnalyzed ? result.VerboseComments : [];
        var candidateTypeIssues = isTypeAnalyzed ? result.TypeFileIssues.Where(i => i.HasViolation).ToList() : [];
        var candidateLocViolations = isLocAnalyzed ? result.LinesOfCodeViolations : [];

        int totalCount = candidateCloneGroups.Count
            + candidateSubMethodGroups.Count
            + candidateStringGroups.Count
            + candidatePredicateGroups.Count
            + candidateComments.Count
            + candidateTypeIssues.Count
            + candidateLocViolations.Count;

        int? limit = settings.Limit;

        // If no limit or limit is non-positive or limit >= totalCount, display all analyzed items ordered by benefit descending
        if (!limit.HasValue || limit.Value <= 0 || limit.Value >= totalCount)
        {
            return new LimitedDisplayItems
            {
                CloneGroups = candidateCloneGroups.OrderByDescending(g => GetCloneBenefit(g, effEstimator, baseDirectory)).ToList(),
                SubMethodCloneGroups = candidateSubMethodGroups.OrderByDescending(g => GetSubMethodBenefit(g, effEstimator, baseDirectory)).ToList(),
                RepeatedStringGroups = candidateStringGroups.OrderByDescending(g => GetRepeatedStringBenefit(g, effEstimator)).ToList(),
                PredicateCloneGroups = candidatePredicateGroups.OrderByDescending(g => GetPredicateBenefit(g, effEstimator)).ToList(),
                VerboseComments = candidateComments.OrderByDescending(c => GetCommentBenefit(c, result.SummarizedComments, effEstimator)).ToList(),
                TypeFileIssues = candidateTypeIssues.OrderByDescending(GetTypeIssueBenefit).ToList(),
                LinesOfCodeViolations = candidateLocViolations.OrderByDescending(GetLocViolationBenefit).ToList(),
                TotalItemCount = totalCount,
                DisplayedItemCount = totalCount,
                IsLimited = false
            };
        }

        // Global limiting: gather all items across categories and prioritize by largest estimated benefit
        var allCandidates = new List<(object Item, int Benefit, int CategoryPriority, int Index)>(totalCount);

        for (int i = 0; i < candidateCloneGroups.Count; i++)
        {
            var item = candidateCloneGroups[i];
            allCandidates.Add((item, GetCloneBenefit(item, effEstimator, baseDirectory), 1, i));
        }

        for (int i = 0; i < candidateSubMethodGroups.Count; i++)
        {
            var item = candidateSubMethodGroups[i];
            allCandidates.Add((item, GetSubMethodBenefit(item, effEstimator, baseDirectory), 2, i));
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

        var topItems = allCandidates
            .OrderByDescending(c => c.Benefit)
            .ThenBy(c => c.CategoryPriority)
            .ThenBy(c => c.Index)
            .Take(limit.Value)
            .ToList();

        var selectedSet = new HashSet<object>(topItems.Select(t => t.Item), ReferenceEqualityComparer.Instance);

        var selectedClones = candidateCloneGroups.Where(selectedSet.Contains).OrderByDescending(g => GetCloneBenefit(g, effEstimator, baseDirectory)).ToList();
        var selectedSubMethods = candidateSubMethodGroups.Where(selectedSet.Contains).OrderByDescending(g => GetSubMethodBenefit(g, effEstimator, baseDirectory)).ToList();
        var selectedStrings = candidateStringGroups.Where(selectedSet.Contains).OrderByDescending(g => GetRepeatedStringBenefit(g, effEstimator)).ToList();
        var selectedPredicates = candidatePredicateGroups.Where(selectedSet.Contains).OrderByDescending(g => GetPredicateBenefit(g, effEstimator)).ToList();
        var selectedComments = candidateComments.Where(selectedSet.Contains).OrderByDescending(c => GetCommentBenefit(c, result.SummarizedComments, effEstimator)).ToList();
        var selectedTypes = candidateTypeIssues.Where(selectedSet.Contains).OrderByDescending(GetTypeIssueBenefit).ToList();
        var selectedLocs = candidateLocViolations.Where(selectedSet.Contains).OrderByDescending(GetLocViolationBenefit).ToList();

        int displayedCount = selectedClones.Count
            + selectedSubMethods.Count
            + selectedStrings.Count
            + selectedPredicates.Count
            + selectedComments.Count
            + selectedTypes.Count
            + selectedLocs.Count;

        return new LimitedDisplayItems
        {
            CloneGroups = selectedClones,
            SubMethodCloneGroups = selectedSubMethods,
            RepeatedStringGroups = selectedStrings,
            PredicateCloneGroups = selectedPredicates,
            VerboseComments = selectedComments,
            TypeFileIssues = selectedTypes,
            LinesOfCodeViolations = selectedLocs,
            TotalItemCount = totalCount,
            DisplayedItemCount = displayedCount,
            IsLimited = true
        };
    }

    public static int GetCloneBenefit(CodeCloneGroup group, HeuristicTokenEstimator estimator, string baseDirectory)
    {
        int gain = CloneGainEstimator.EstimateGainTokens(group, estimator, baseDirectory);
        if (gain > 0) return gain;
        return Math.Max(1, group.StatementCount * 10 * Math.Max(1, group.Methods.Count - 1));
    }

    public static int GetSubMethodBenefit(SubMethodCloneGroup group, HeuristicTokenEstimator estimator, string baseDirectory)
    {
        int gain = CloneGainEstimator.EstimateGainTokens(group, estimator, baseDirectory);
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
