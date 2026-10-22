namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Measured LLM token counts for an individual source file.
/// </summary>
public sealed record FileMeasurement
{
    public FileMeasurement() { }

    public FileMeasurement(string filePath, FileCategory category, int codeTokens, int commentTokens, int totalTokens)
    {
        FilePath = filePath;
        Category = category;
        CodeTokens = codeTokens;
        CommentTokens = commentTokens;
        TotalTokens = totalTokens;
    }

    /// <summary>
    /// Relative or absolute path to the analyzed file.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// Category of the file (Production, Test, or Generated).
    /// </summary>
    public FileCategory Category { get; init; }

    /// <summary>
    /// Estimated LLM tokens attributable to code (excluding comments).
    /// </summary>
    public int CodeTokens { get; init; }

    /// <summary>
    /// Estimated LLM tokens attributable to comments.
    /// </summary>
    public int CommentTokens { get; init; }

    /// <summary>
    /// Estimated total LLM tokens for the file.
    /// </summary>
    public int TotalTokens { get; init; }
}
