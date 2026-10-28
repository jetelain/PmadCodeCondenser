using Pmad.AI.Tokens;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Predicates;

/// <summary>
/// Estimates character and token savings from refactoring duplicate conditions into helper methods.
/// </summary>
public static class PredicateGainEstimator
{
    /// <summary>
    /// Estimates total character gain across all predicate clone groups.
    /// </summary>
    public static int EstimateTotalGainCharacters(IReadOnlyList<PredicateCloneGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        int total = 0;
        foreach (var group in groups)
        {
            total += group.EstimateGainCharacters();
        }
        return total;
    }

    /// <summary>
    /// Estimates total token gain across all predicate clone groups.
    /// </summary>
    public static int EstimateTotalGainTokens(
        IReadOnlyList<PredicateCloneGroup> groups,
        HeuristicTokenEstimator? estimator = null)
    {
        ArgumentNullException.ThrowIfNull(groups);
        int total = 0;
        foreach (var group in groups)
        {
            total += group.EstimateGainTokens(estimator);
        }
        return total;
    }
}
