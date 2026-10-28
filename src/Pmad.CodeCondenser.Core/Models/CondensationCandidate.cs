using Pmad.AI.Tokens;

namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Represents a detected code pattern candidate that can be condensed using modern framework methods or idioms.
/// </summary>
public sealed record CondensationCandidate
{
    /// <summary>
    /// Unique identifier of the rule (e.g., "PCC401").
    /// </summary>
    public required string RuleId { get; init; }

    /// <summary>
    /// Human-readable title of the rule.
    /// </summary>
    public required string RuleName { get; init; }

    /// <summary>
    /// Category of the pattern rule.
    /// </summary>
    public required PatternCategory Category { get; init; }

    /// <summary>
    /// Relative or absolute path of the source file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// 1-based starting line number.
    /// </summary>
    public required int StartLine { get; init; }

    /// <summary>
    /// 1-based ending line number.
    /// </summary>
    public required int EndLine { get; init; }

    /// <summary>
    /// 1-based starting column.
    /// </summary>
    public required int StartColumn { get; init; }

    /// <summary>
    /// 1-based ending column.
    /// </summary>
    public required int EndColumn { get; init; }

    /// <summary>
    /// Original code snippet identified for condensation.
    /// </summary>
    public required string OriginalCode { get; init; }

    /// <summary>
    /// Suggested condensed code replacement.
    /// </summary>
    public required string SuggestedCode { get; init; }

    /// <summary>
    /// Whether this candidate is safe for automated code rewriting without manual intervention.
    /// </summary>
    public required bool CanAutoFix { get; init; }

    /// <summary>
    /// Estimated character savings (positive if the replacement is shorter).
    /// </summary>
    public int CharacterGain => Math.Max(0, OriginalCode.Length - SuggestedCode.Length);

    /// <summary>
    /// Estimates token savings achieved by applying this condensation candidate.
    /// </summary>
    /// <param name="estimator">Token estimator to use. Defaults to <see cref="HeuristicTokenEstimator.Default"/>.</param>
    public int EstimateGainTokens(HeuristicTokenEstimator? estimator = null)
    {
        var effEstimator = estimator ?? HeuristicTokenEstimator.Default;
        var originalTokens = effEstimator.Estimate(OriginalCode.AsSpan());
        var suggestedTokens = effEstimator.Estimate(SuggestedCode.AsSpan());
        return Math.Max(0, originalTokens - suggestedTokens);
    }
}
