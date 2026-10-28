using Pmad.AI.Tokens;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Patterns;

/// <summary>
/// Estimates character and token savings from applying condensation patterns.
/// </summary>
public static class PatternGainEstimator
{
    /// <summary>
    /// Estimates total character gain across all condensation pattern candidates.
    /// </summary>
    public static int EstimateTotalGainCharacters(IReadOnlyList<CondensationCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        int total = 0;
        foreach (var candidate in candidates)
        {
            total += candidate.CharacterGain;
        }
        return total;
    }

    /// <summary>
    /// Estimates total token gain across all condensation pattern candidates.
    /// </summary>
    public static int EstimateTotalGainTokens(
        IReadOnlyList<CondensationCandidate> candidates,
        HeuristicTokenEstimator? estimator = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        int total = 0;
        foreach (var candidate in candidates)
        {
            total += candidate.EstimateGainTokens(estimator);
        }
        return total;
    }
}
