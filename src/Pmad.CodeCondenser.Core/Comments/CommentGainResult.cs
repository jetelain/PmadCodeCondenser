namespace Pmad.CodeCondenser.Core.Comments;

/// <summary>
/// Aggregated result of comment condensation gain calculations, including character and token metrics.
/// </summary>
public sealed record CommentGainResult
{
    /// <summary>
    /// Total characters across all original comments that had summaries.
    /// </summary>
    public int OriginalCharacters { get; init; }

    /// <summary>
    /// Total characters across all comments after applying condensation (kept comments retain original length).
    /// </summary>
    public int CondensedCharacters { get; init; }

    /// <summary>
    /// Net reduction in characters achieved by condensation.
    /// </summary>
    public int GainCharacters { get; init; }

    /// <summary>
    /// Overall character reduction percentage (0 to 100).
    /// </summary>
    public int ReductionPercent { get; init; }

    /// <summary>
    /// Number of verbose comments evaluated and summarized.
    /// </summary>
    public int SummarizedCount { get; init; }

    /// <summary>
    /// Estimated LLM tokens across all original comments that had summaries.
    /// </summary>
    public int OriginalTokens { get; init; }

    /// <summary>
    /// Estimated LLM tokens across all comments after applying condensation.
    /// </summary>
    public int CondensedTokens { get; init; }

    /// <summary>
    /// Net reduction in estimated LLM tokens achieved by condensation.
    /// </summary>
    public int GainTokens { get; init; }

    /// <summary>
    /// Overall token reduction percentage (0 to 100).
    /// </summary>
    public int TokenReductionPercent => OriginalTokens > 0
        ? Math.Clamp((int)Math.Round((1.0 - (double)CondensedTokens / OriginalTokens) * 100), 0, 100)
        : 0;

    /// <summary>
    /// An empty result indicating no comments were summarized.
    /// </summary>
    public static CommentGainResult Empty { get; } = new();

    /// <summary>
    /// Deconstructs the result into the standard 5 character-based metric components for backward compatibility.
    /// </summary>
    public void Deconstruct(out int originalCharacters, out int condensedCharacters, out int gainCharacters, out int reductionPercent, out int summarizedCount)
    {
        originalCharacters = OriginalCharacters;
        condensedCharacters = CondensedCharacters;
        gainCharacters = GainCharacters;
        reductionPercent = ReductionPercent;
        summarizedCount = SummarizedCount;
    }

    /// <summary>
    /// Deconstructs the result into all 8 character and token metric components.
    /// </summary>
    public void Deconstruct(
        out int originalCharacters,
        out int condensedCharacters,
        out int gainCharacters,
        out int reductionPercent,
        out int summarizedCount,
        out int originalTokens,
        out int condensedTokens,
        out int gainTokens)
    {
        originalCharacters = OriginalCharacters;
        condensedCharacters = CondensedCharacters;
        gainCharacters = GainCharacters;
        reductionPercent = ReductionPercent;
        summarizedCount = SummarizedCount;
        originalTokens = OriginalTokens;
        condensedTokens = CondensedTokens;
        gainTokens = GainTokens;
    }

    /// <summary>
    /// Implicit conversion to a 5-element tuple for seamless backward compatibility with existing callers.
    /// </summary>
    public static implicit operator (int OriginalCharacters, int CondensedCharacters, int GainCharacters, int ReductionPercent, int SummarizedCount)(CommentGainResult result) =>
        result == null
            ? (0, 0, 0, 0, 0)
            : (result.OriginalCharacters, result.CondensedCharacters, result.GainCharacters, result.ReductionPercent, result.SummarizedCount);
}
