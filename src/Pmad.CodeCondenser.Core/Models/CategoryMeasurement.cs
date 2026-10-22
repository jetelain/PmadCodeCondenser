namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Aggregated LLM token metrics for a category of files (e.g. Production, Tests, or Total).
/// </summary>
public sealed record CategoryMeasurement
{
    /// <summary>
    /// Total number of files in this category.
    /// </summary>
    public int FilesCount { get; init; }

    /// <summary>
    /// Estimated LLM tokens attributable to code (excluding comments).
    /// </summary>
    public int CodeTokens { get; init; }

    /// <summary>
    /// Estimated LLM tokens attributable to comments (single-line, multi-line, and XML documentation).
    /// </summary>
    public int CommentTokens { get; init; }

    /// <summary>
    /// Estimated total LLM tokens for this category.
    /// </summary>
    public int TotalTokens { get; init; }
}
