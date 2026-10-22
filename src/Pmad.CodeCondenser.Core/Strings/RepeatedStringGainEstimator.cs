using Pmad.AI.Tokens;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Strings;

/// <summary>
/// Estimates character and token savings from compacting repeated inline strings into constants.
/// </summary>
public static class RepeatedStringGainEstimator
{
    /// <summary>
    /// Estimates total character gain across all repeated string groups.
    /// </summary>
    /// <param name="groups">The repeated string groups.</param>
    /// <param name="preExistingClass">Whether to assume a pre-existing constants class exists.</param>
    /// <returns>Estimated total character gain.</returns>
    public static int EstimateTotalGainCharacters(
        IReadOnlyList<RepeatedStringGroup> groups,
        bool preExistingClass = true)
    {
        ArgumentNullException.ThrowIfNull(groups);
        int total = 0;
        foreach (var group in groups)
        {
            total += group.EstimateGainCharacters(preExistingClass);
        }
        return total;
    }

    /// <summary>
    /// Estimates total token gain across all repeated string groups.
    /// </summary>
    /// <param name="groups">The repeated string groups.</param>
    /// <param name="estimator">The token estimator to use.</param>
    /// <returns>Estimated total tokens saved.</returns>
    public static int EstimateTotalGainTokens(
        IReadOnlyList<RepeatedStringGroup> groups,
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
