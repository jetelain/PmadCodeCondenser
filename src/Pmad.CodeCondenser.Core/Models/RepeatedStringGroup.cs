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
    /// Suggested static class name containing the constant (default: "Constants").
    /// </summary>
    public string SuggestedClassName { get; init; } = "Constants";

    /// <summary>
    /// Suggested namespace for the constant declaration.
    /// </summary>
    public string? TargetNamespace { get; init; }

    /// <summary>
    /// Indicates whether an existing constant declaration with this string value was found in the codebase.
    /// </summary>
    public bool HasExistingConstant => ExistingConstantDeclarations.Count > 0;

    /// <summary>
    /// The primary occurrence representing the existing constant declaration, if one exists.
    /// </summary>
    public RepeatedStringOccurrence? ExistingConstantDeclaration =>
        ExistingConstantDeclarations.Count > 0 ? ExistingConstantDeclarations[0] : null;

    /// <summary>
    /// Indicates whether the primary existing constant was declared with private accessibility.
    /// </summary>
    public bool IsExistingConstantPrivate => ExistingConstantDeclaration?.IsPrivateConstant ?? false;

    /// <summary>
    /// All existing constant declarations found with this string value.
    /// </summary>
    public IReadOnlyList<RepeatedStringOccurrence> ExistingConstantDeclarations { get; init; } = [];

    /// <summary>
    /// Gets only the inline occurrences (excluding any existing constant declarations).
    /// </summary>
    public IReadOnlyList<RepeatedStringOccurrence> InlineOccurrences =>
        Occurrences.Where(o => !o.IsConstDeclaration).ToList();

    /// <summary>
    /// Estimates character savings if this repeated string is extracted into a constant or replaced by an existing constant.
    /// When an existing constant already exists, declaration cost is already paid.
    /// With a pre-existing class: declaration is ~60 chars + string size, each usage is ~30 chars.
    /// Without a pre-existing class: declaration is ~120 chars + string size, each usage is ~30 chars.
    /// </summary>
    /// <param name="preExistingClass">True if assuming a pre-existing constants class exists; false otherwise.</param>
    /// <returns>Estimated character gain (reduction in characters).</returns>
    public int EstimateGainCharacters(bool preExistingClass = true)
    {
        if (HasExistingConstant)
        {
            var inlineCount = InlineOccurrences.Count;
            if (inlineCount == 0)
            {
                return 0;
            }

            int existingCurrentChars = inlineCount * (Length + 2);
            int usageLength = GenerateTypicalUsage().Length;
            int existingNewCost = inlineCount * usageLength;
            return Math.Max(0, existingCurrentChars - existingNewCost);
        }

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
        var effEstimator = estimator ?? HeuristicTokenEstimator.Default;
        int literalTokens = effEstimator.Estimate(Value.AsSpan());

        if (HasExistingConstant)
        {
            var inlineCount = InlineOccurrences.Count;
            if (inlineCount == 0)
            {
                return 0;
            }

            int usageTokens = effEstimator.Estimate(GenerateTypicalUsage().AsSpan());
            return Math.Max(0, inlineCount * Math.Max(0, literalTokens - usageTokens));
        }

        int count = Occurrences.Count;
        if (count < 2)
        {
            return 0;
        }

        int currentTokens = count * literalTokens;
        // Declaration: literalTokens + ~8 overhead tokens for 'internal const string Name = "";'
        // Usage: ~3 tokens for 'Constants.Name' per occurrence
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
            .Replace("\0", "\\0")
            .Replace("\a", "\\a")
            .Replace("\b", "\\b")
            .Replace("\f", "\\f")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            .Replace("\v", "\\v");
    }
}
