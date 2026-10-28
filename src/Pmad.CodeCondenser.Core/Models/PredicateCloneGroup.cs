using Pmad.AI.Tokens;
using Pmad.CodeCondenser.Core.Predicates;

namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Represents a cluster of duplicate or helper-matching boolean conditions / predicates.
/// </summary>
public sealed record PredicateCloneGroup
{
    /// <summary>
    /// The canonical normalized structural signature identifying this predicate pattern.
    /// </summary>
    public required string NormalizedSignature { get; init; }

    /// <summary>
    /// 64-bit hash of the normalized signature.
    /// </summary>
    public ulong NormalizedHash => NormalizedSignature.Length > 0
        ? PredicateNormalizer.ComputeHash(NormalizedSignature)
        : (Occurrences.Count > 0 ? Occurrences[0].NormalizedHash : 0);

    /// <summary>
    /// Approximate length of the canonical condition in characters.
    /// </summary>
    public int Length => NormalizedSignature.Length;

    /// <summary>
    /// All occurrences of this predicate (both inline call sites and existing helper declarations).
    /// </summary>
    public required IReadOnlyList<PredicateOccurrence> Occurrences { get; init; }

    /// <summary>
    /// Category of the clone cluster (Production, Test, or CrossBoundary).
    /// </summary>
    public CloneCategory Category { get; init; } = CloneCategory.Production;

    /// <summary>
    /// Metadata about an existing helper declaration matching this condition, if one was found.
    /// </summary>
    public PredicateHelperInfo? ExistingHelper { get; init; }

    /// <summary>
    /// The primary occurrence representing the existing helper declaration, if one exists.
    /// </summary>
    public PredicateOccurrence? ExistingHelperOccurrence =>
        Occurrences.FirstOrDefault(o => o.IsHelperDeclaration);

    /// <summary>
    /// Indicates whether an existing helper method or property was found in the codebase.
    /// </summary>
    public bool HasExistingHelper => ExistingHelper != null;

    /// <summary>
    /// Indicates whether the existing helper was declared with private accessibility.
    /// </summary>
    public bool IsExistingHelperPrivate => ExistingHelper?.IsPrivate ?? false;

    /// <summary>
    /// All inline call-site occurrences (excluding the helper declaration itself).
    /// </summary>
    public IReadOnlyList<PredicateOccurrence> InlineOccurrences =>
        Occurrences.Where(o => !o.IsHelperDeclaration).ToList();

    /// <summary>
    /// Suggested identifier name for the helper method (existing helper name, or auto-generated name).
    /// </summary>
    public required string SuggestedHelperName { get; init; }

    /// <summary>
    /// Suggested class name for declaring a shared helper, if no helper currently exists.
    /// </summary>
    public string SuggestedClassName { get; init; } = "PredicateHelpers";

    /// <summary>
    /// Generates a typical replacement call for an occurrence.
    /// </summary>
    public string GenerateTypicalUsage(PredicateOccurrence? occurrence = null)
    {
        occurrence ??= InlineOccurrences.FirstOrDefault(o => !o.IsNegatedAtCallSite)
            ?? (InlineOccurrences.Count > 0 ? InlineOccurrences[0] : (Occurrences.Count > 0 ? Occurrences[0] : null));

        bool isProperty = HasExistingHelper && ExistingHelper != null && ExistingHelper.IsProperty;

        if (occurrence == null)
        {
            return isProperty ? SuggestedHelperName : $"{SuggestedHelperName}()";
        }

        bool isInstanceReceiverHelper = HasExistingHelper && ExistingHelper != null && !ExistingHelper.IsStatic
            && (NormalizedSignature.Contains("$this", StringComparison.Ordinal) || (ExistingHelper.Parameters.Count == 0 && !string.IsNullOrEmpty(occurrence.ReceiverExpression)));

        string prefix = string.Empty;
        IReadOnlyList<string> argsList;

        if (isInstanceReceiverHelper)
        {
            if (!string.IsNullOrEmpty(occurrence.ReceiverExpression))
            {
                prefix = occurrence.ReceiverExpression + ".";
            }
            argsList = occurrence.InstanceArgumentExpressions;
        }
        else
        {
            argsList = occurrence.StaticArgumentExpressions.Count > 0
                ? occurrence.StaticArgumentExpressions
                : occurrence.ArgumentExpressions;
        }

        string args = argsList.Count > 0
            ? string.Join(", ", argsList)
            : string.Empty;

        string call = isProperty
            ? $"{prefix}{SuggestedHelperName}"
            : $"{prefix}{SuggestedHelperName}({args})";

        return occurrence.IsNegatedAtCallSite ? $"!{call}" : call;
    }

    /// <summary>
    /// Estimates characters saved by replacing duplicate inline conditions with helper calls.
    /// </summary>
    public int EstimateGainCharacters()
    {
        if (HasExistingHelper)
        {
            // Zero declaration overhead since helper already exists
            int total = 0;
            foreach (var occ in InlineOccurrences)
            {
                var callLen = GenerateTypicalUsage(occ).Length;
                total += Math.Max(0, occ.ExpressionText.Length - callLen);
            }
            return total;
        }

        if (InlineOccurrences.Count < 2)
        {
            return 0;
        }

        // Declaration overhead ~ 70 chars for "private static bool Helper(...) => ...;"
        int declarationOverhead = 70 + Length;
        int inlineChars = InlineOccurrences.Sum(o => o.ExpressionText.Length);
        int replacementChars = InlineOccurrences.Sum(o => GenerateTypicalUsage(o).Length);
        int rawSavings = inlineChars - (declarationOverhead + replacementChars);
        return Math.Max(0, rawSavings);
    }

    /// <summary>
    /// Estimates LLM context tokens saved by replacing duplicate inline conditions with helper calls.
    /// </summary>
    public int EstimateGainTokens(HeuristicTokenEstimator? estimator = null)
    {
        var effEstimator = estimator ?? HeuristicTokenEstimator.Default;

        if (HasExistingHelper)
        {
            int total = 0;
            foreach (var occ in InlineOccurrences)
            {
                int conditionTokens = effEstimator.Estimate(occ.ExpressionText.AsSpan());
                int callTokens = effEstimator.Estimate(GenerateTypicalUsage(occ).AsSpan());
                total += Math.Max(0, conditionTokens - callTokens);
            }
            return total;
        }

        if (InlineOccurrences.Count < 2)
        {
            return 0;
        }

        int inlineTokens = 0;
        int replacementTokens = 0;
        foreach (var occ in InlineOccurrences)
        {
            inlineTokens += effEstimator.Estimate(occ.ExpressionText.AsSpan());
            replacementTokens += effEstimator.Estimate(GenerateTypicalUsage(occ).AsSpan());
        }

        string typicalDecl = $"private static bool {SuggestedHelperName}() => {NormalizedSignature};";
        int declarationTokens = effEstimator.Estimate(typicalDecl.AsSpan());

        int rawSavings = inlineTokens - (declarationTokens + replacementTokens);
        return Math.Max(0, rawSavings);
    }
}
