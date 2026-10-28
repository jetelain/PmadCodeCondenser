using Microsoft.CodeAnalysis;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Predicates;

/// <summary>
/// Detects and groups duplicate conditions and matches inline conditions against existing boolean helpers.
/// </summary>
public sealed class PredicateCloneDetector : IPredicateCloneDetector
{
    private readonly IPredicateExtractor _extractor;

    public PredicateCloneDetector(
        IPredicateExtractor? extractor = null)
    {
        _extractor = extractor ?? new PredicateExtractor();
    }

    /// <inheritdoc />
    public IReadOnlyList<PredicateOccurrence> ExtractPredicates(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings,
        string? projectId = null,
        FileCategory fileCategory = FileCategory.Production)
    {
        return _extractor.ExtractPredicates(syntaxTree, filePath, settings, projectId, fileCategory);
    }

    /// <inheritdoc />
    public IReadOnlyList<PredicateCloneGroup> GroupPredicates(
        IEnumerable<PredicateOccurrence> occurrences,
        CodeCondenserSettings settings,
        Func<string, FileCategory>? fileClassifier = null)
    {
        ArgumentNullException.ThrowIfNull(occurrences);
        ArgumentNullException.ThrowIfNull(settings);

        var allList = occurrences.ToList();
        var helpers = allList.Where(o => o.IsHelperDeclaration).ToList();
        var inlines = allList.Where(o => !o.IsHelperDeclaration).ToList();

        // Index existing helpers by static and instance signatures
        var staticHelperMap = new Dictionary<string, List<PredicateOccurrence>>(StringComparer.Ordinal);
        var instanceHelperMap = new Dictionary<string, List<PredicateOccurrence>>(StringComparer.Ordinal);

        foreach (var helper in helpers)
        {
            if (helper.HelperInfo?.IsStatic == true)
            {
                if (!staticHelperMap.TryGetValue(helper.NormalizedSignature, out var list))
                {
                    list = new List<PredicateOccurrence>();
                    staticHelperMap[helper.NormalizedSignature] = list;
                }
                list.Add(helper);
            }
            else
            {
                var sig = helper.InstanceNormalizedSignature ?? helper.NormalizedSignature;
                if (!instanceHelperMap.TryGetValue(sig, out var list))
                {
                    list = new List<PredicateOccurrence>();
                    instanceHelperMap[sig] = list;
                }
                list.Add(helper);

                // If an instance helper does not use '$this', its signature matches standard (static) normalization
                // and it can be called within its declaring type.
                if (!sig.Contains("$this", StringComparison.Ordinal))
                {
                    if (!staticHelperMap.TryGetValue(helper.NormalizedSignature, out var staticList))
                    {
                        staticList = new List<PredicateOccurrence>();
                        staticHelperMap[helper.NormalizedSignature] = staticList;
                    }
                    staticList.Add(helper);
                }
            }
        }

        var result = new List<PredicateCloneGroup>();
        var matchedInlines = new HashSet<PredicateOccurrence>();

        // 1. Match inline occurrences against existing helpers (CC011)
        var helperOccurrences = new Dictionary<PredicateOccurrence, List<PredicateOccurrence>>();

        void AddMatch(PredicateOccurrence helperDecl, PredicateOccurrence callSite, PredicateOccurrence originalInline)
        {
            if (!helperOccurrences.TryGetValue(helperDecl, out var list))
            {
                list = new List<PredicateOccurrence>();
                helperOccurrences[helperDecl] = list;
            }
            list.Add(callSite);
            matchedInlines.Add(originalInline);
        }

        static bool CanAccessHelper(PredicateOccurrence helperDecl, PredicateOccurrence callSite, bool isInstance)
        {
            if (helperDecl.HelperInfo?.IsPrivate == true)
            {
                // Private helpers can only be accessed within the same declaring type
                bool sameType = callSite.EnclosingTypeName != null &&
                       string.Equals(helperDecl.HelperInfo.DeclaringTypeName, callSite.EnclosingTypeName, StringComparison.Ordinal) &&
                       string.Equals(helperDecl.Namespace, callSite.Namespace, StringComparison.Ordinal);
                if (!sameType) return false;
            }

            // Parameter count check: call site arguments must match helper parameter count
            if (helperDecl.HelperInfo != null)
            {
                int expectedArgs = helperDecl.HelperInfo.IsProperty ? 0 : helperDecl.HelperInfo.Parameters.Count;
                int actualArgs = isInstance
                    ? callSite.InstanceArgumentExpressions.Count
                    : (callSite.StaticArgumentExpressions.Count > 0
                        ? callSite.StaticArgumentExpressions.Count
                        : callSite.ArgumentExpressions.Count);

                if (actualArgs != expectedArgs)
                {
                    return false;
                }
            }

            return true;
        }

        static PredicateOccurrence? FindAccessibleHelper(
            List<PredicateOccurrence>? candidates,
            PredicateOccurrence callSite,
            bool isInstance)
        {
            if (candidates == null || candidates.Count == 0) return null;

            PredicateOccurrence? fallback = null;
            foreach (var candidate in candidates)
            {
                if (CanAccessHelper(candidate, callSite, isInstance))
                {
                    // Prioritize exact declaring type match (e.g. private helper in same class)
                    if (string.Equals(candidate.EnclosingTypeName, callSite.EnclosingTypeName, StringComparison.Ordinal) &&
                        string.Equals(candidate.Namespace, callSite.Namespace, StringComparison.Ordinal))
                    {
                        return candidate;
                    }

                    fallback ??= candidate;
                }
            }
            return fallback;
        }

        foreach (var inline in inlines)
        {
            // 1. Instance helper direct match
            if (inline.InstanceNormalizedSignature != null &&
                instanceHelperMap.TryGetValue(inline.InstanceNormalizedSignature, out var instCandidates) &&
                FindAccessibleHelper(instCandidates, inline, isInstance: true) is { } instHelper)
            {
                bool neg = instHelper.HelperInfo?.IsBodyNegated == true ? !inline.IsNegatedAtCallSite : inline.IsNegatedAtCallSite;
                AddMatch(instHelper, inline with { IsNegatedAtCallSite = neg }, inline);
                continue;
            }

            // 2. Instance helper De Morgan negated match
            if (inline.NegatedInstanceNormalizedSignature != null &&
                instanceHelperMap.TryGetValue(inline.NegatedInstanceNormalizedSignature, out var negInstCandidates) &&
                FindAccessibleHelper(negInstCandidates, inline, isInstance: true) is { } negInstHelper)
            {
                bool isHelperNegated = negInstHelper.HelperInfo?.IsBodyNegated == true;
                bool neg = isHelperNegated == inline.IsNegatedAtCallSite;
                AddMatch(negInstHelper, inline with { IsNegatedAtCallSite = neg }, inline);
                continue;
            }

            // 3. Static helper direct match
            if (staticHelperMap.TryGetValue(inline.NormalizedSignature, out var staticCandidates) &&
                FindAccessibleHelper(staticCandidates, inline, isInstance: false) is { } staticHelper)
            {
                bool neg = staticHelper.HelperInfo?.IsBodyNegated == true ? !inline.IsNegatedAtCallSite : inline.IsNegatedAtCallSite;
                AddMatch(staticHelper, inline with { IsNegatedAtCallSite = neg }, inline);
                continue;
            }

            // 4. Static helper De Morgan negated match
            if (inline.NegatedNormalizedSignature != null &&
                staticHelperMap.TryGetValue(inline.NegatedNormalizedSignature, out var negStaticCandidates) &&
                FindAccessibleHelper(negStaticCandidates, inline, isInstance: false) is { } negStaticHelper)
            {
                bool isHelperNegated = negStaticHelper.HelperInfo?.IsBodyNegated == true;
                bool neg = isHelperNegated == inline.IsNegatedAtCallSite;
                AddMatch(negStaticHelper, inline with { IsNegatedAtCallSite = neg }, inline);
                continue;
            }
        }

        // Build groups for existing helper matches
        foreach (var (helperDecl, matchedInlineList) in helperOccurrences)
        {
            if (matchedInlineList.Count == 0)
            {
                continue;
            }

            var groupOccurrences = new List<PredicateOccurrence> { helperDecl };
            groupOccurrences.AddRange(matchedInlineList);

            var category = DetermineCategory(groupOccurrences, fileClassifier);

            result.Add(new PredicateCloneGroup
            {
                NormalizedSignature = helperDecl.NormalizedSignature,
                Occurrences = groupOccurrences,
                Category = category,
                ExistingHelper = helperDecl.HelperInfo,
                SuggestedHelperName = helperDecl.HelperInfo?.HelperName ?? "IsValid",
                SuggestedClassName = helperDecl.HelperInfo?.DeclaringTypeName ?? "PredicateHelpers"
            });
        }

        // 2. Group remaining unmatched inline occurrences into clone clusters (CC010)
        var minOccurrences = Math.Max(2, settings.PredicateMinOccurrences);
        var remainingInlines = inlines.Where(o => !matchedInlines.Contains(o));

        var unextractedGroups = remainingInlines
            .GroupBy(o => GetDualPairKey(o), StringComparer.Ordinal);

        foreach (var group in unextractedGroups)
        {
            var rawList = group.ToList();
            if (rawList.Count < minOccurrences)
            {
                continue;
            }

            if (rawList.All(o => o.ExpressionText.Length < settings.PredicateLengthThreshold))
            {
                continue;
            }

            var distinctSignatures = rawList
                .Select(o => o.NormalizedSignature)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            string canonicalSignature;
            bool isDualCluster = distinctSignatures.Count > 1;

            if (isDualCluster)
            {
                canonicalSignature = ChooseCanonicalSignature(distinctSignatures);
            }
            else
            {
                canonicalSignature = distinctSignatures[0];
            }

            var canonicalHash = PredicateNormalizer.ComputeHash(canonicalSignature);

            var occList = new List<PredicateOccurrence>(rawList.Count);
            foreach (var occ in rawList)
            {
                if (isDualCluster && !string.Equals(occ.NormalizedSignature, canonicalSignature, StringComparison.Ordinal))
                {
                    occList.Add(occ with
                    {
                        NormalizedSignature = canonicalSignature,
                        NormalizedHash = canonicalHash,
                        IsNegatedAtCallSite = !occ.IsNegatedAtCallSite
                    });
                }
                else
                {
                    occList.Add(occ);
                }
            }

            // Order so non-negated occurrences appear first
            occList = occList.OrderBy(o => o.IsNegatedAtCallSite ? 1 : 0).ToList();

            var category = DetermineCategory(occList, fileClassifier);
            var sample = occList.FirstOrDefault(o => !o.IsNegatedAtCallSite) ?? occList[0];
            var suggestedName = DeriveSuggestedHelperName(sample);

            result.Add(new PredicateCloneGroup
            {
                NormalizedSignature = canonicalSignature,
                Occurrences = occList,
                Category = category,
                ExistingHelper = null,
                SuggestedHelperName = suggestedName,
                SuggestedClassName = "PredicateHelpers"
            });
        }

        // Sort by impact descending: existing helper matches with high inline count first, then clone clusters
        return result
            .OrderByDescending(g => g.HasExistingHelper)
            .ThenByDescending(g => g.InlineOccurrences.Count * g.Length)
            .ToList();
    }

    private static string GetDualPairKey(PredicateOccurrence o)
    {
        var norm = o.NormalizedSignature;
        var neg = o.NegatedNormalizedSignature;

        if (string.IsNullOrEmpty(neg) || string.Equals(norm, neg, StringComparison.Ordinal))
        {
            return norm;
        }

        return string.CompareOrdinal(norm, neg) <= 0
            ? $"{norm}<=>{neg}"
            : $"{neg}<=>{norm}";
    }

    private static string ChooseCanonicalSignature(List<string> signatures)
    {
        if (signatures.Count == 1)
        {
            return signatures[0];
        }

        string best = signatures[0];
        int bestPenalty = ComputePolarityPenalty(best);

        for (int i = 1; i < signatures.Count; i++)
        {
            var candidate = signatures[i];
            int penalty = ComputePolarityPenalty(candidate);

            if (penalty < bestPenalty || (penalty == bestPenalty && string.CompareOrdinal(candidate, best) < 0))
            {
                best = candidate;
                bestPenalty = penalty;
            }
        }

        return best;
    }

    private static int ComputePolarityPenalty(string signature)
    {
        int penalty = 0;
        for (int i = 0; i < signature.Length; i++)
        {
            char c = signature[i];
            if (c == '!')
            {
                penalty += 2;
            }
            else if (c == '|' && i + 1 < signature.Length && signature[i + 1] == '|')
            {
                penalty += 1;
                i++;
            }
        }
        return penalty;
    }

    private static CloneCategory DetermineCategory(
        IReadOnlyList<PredicateOccurrence> occurrences,
        Func<string, FileCategory>? fileClassifier)
    {
        var categories = occurrences.Select(o =>
        {
            if (fileClassifier != null)
            {
                return fileClassifier(o.FilePath);
            }
            return o.Category;
        }).Distinct().ToList();

        if (categories.Count == 1 && categories[0] == FileCategory.Test)
        {
            return CloneCategory.Test;
        }

        if (categories.Contains(FileCategory.Production) && categories.Contains(FileCategory.Test))
        {
            return CloneCategory.CrossBoundary;
        }

        return CloneCategory.Production;
    }

    private static string DeriveSuggestedHelperName(PredicateOccurrence sample)
    {
        if (!string.IsNullOrEmpty(sample.ReceiverExpression))
        {
            return "IsConditionMet";
        }

        if (!string.IsNullOrEmpty(sample.EnclosingMember))
        {
            return $"Is{sample.EnclosingMember}Valid";
        }

        return "IsConditionValid";
    }
}
