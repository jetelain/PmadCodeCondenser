using Pmad.AI.Tokens;

namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Represents a cluster of duplicate inline string literals that could be compacted into a constant.
/// </summary>
public sealed record RepeatedStringGroup
{
    /// <summary>
    /// The literal string content value.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Length of the string literal in characters.
    /// </summary>
    public int Length => Value.Length;

    /// <summary>
    /// Occurrences of this string literal across the codebase.
    /// </summary>
    public required IReadOnlyList<RepeatedStringOccurrence> Occurrences { get; init; }

    /// <summary>
    /// Category of the clone cluster (Production, Test, or CrossBoundary).
    /// </summary>
    public CloneCategory Category { get; init; } = CloneCategory.Production;

    /// <summary>
    /// Suggested PascalCase identifier name for the constant.
    /// </summary>
    public required string SuggestedConstantName { get; init; }

    /// <summary>
    /// Suggested static class name containing the constant (default: "MyConstants").
    /// </summary>
    public string SuggestedClassName { get; init; } = "MyConstants";

    /// <summary>
    /// Suggested namespace for the constant declaration.
    /// </summary>
    public string? TargetNamespace { get; init; }

    /// <summary>
    /// Estimates character savings if this repeated string is extracted into a constant.
    /// With a pre-existing class: declaration is ~60 chars + string size, each usage is ~30 chars.
    /// Without a pre-existing class: declaration is ~120 chars + string size, each usage is ~30 chars.
    /// </summary>
    /// <param name="preExistingClass">True if assuming a pre-existing constants class exists; false otherwise.</param>
    /// <returns>Estimated character gain (reduction in characters).</returns>
    public int EstimateGainCharacters(bool preExistingClass = true)
    {
        int count = Occurrences.Count;
        if (count < 2)
        {
            return 0;
        }

        // Current characters: count occurrences of the string literal (including quotes)
        int currentChars = count * (Length + 2);

        // Replacement cost:
        int declarationOverhead = preExistingClass ? 60 : 120;
        int newCost = declarationOverhead + Length + 2 + (count * 30);

        return currentChars - newCost;
    }

    /// <summary>
    /// Estimates token savings using the provided token estimator.
    /// </summary>
    /// <param name="estimator">The token estimator, or default if null.</param>
    /// <returns>Estimated tokens saved.</returns>
    public int EstimateGainTokens(HeuristicTokenEstimator? estimator = null)
    {
        int count = Occurrences.Count;
        if (count < 2)
        {
            return 0;
        }

        var effEstimator = estimator ?? HeuristicTokenEstimator.Default;
        int literalTokens = effEstimator.Estimate(Value.AsSpan());

        int currentTokens = count * literalTokens;
        // Declaration: literalTokens + ~8 overhead tokens for 'internal const string Name = "";'
        // Usage: ~3 tokens for 'MyConstants.Name' per occurrence
        int extractedTokens = (literalTokens + 8) + (count * 3);

        return Math.Max(0, currentTokens - extractedTokens);
    }

    /// <summary>
    /// Generates a minimal C# code snippet declaring the constant.
    /// </summary>
    public string GenerateMinimalDeclaration(string? ns = null)
    {
        var targetNs = ns ?? TargetNamespace ?? "ApplicationNamespace";
        return $$"""
        namespace {{targetNs}};

        static class {{SuggestedClassName}}
        {
            internal const string {{SuggestedConstantName}} = "{{EscapeForCSharp(Value)}}";
        }
        """;
    }

    /// <summary>
    /// Generates the typical usage expression in code.
    /// </summary>
    public string GenerateTypicalUsage() => $"{SuggestedClassName}.{SuggestedConstantName}";

    private static string EscapeForCSharp(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
}
