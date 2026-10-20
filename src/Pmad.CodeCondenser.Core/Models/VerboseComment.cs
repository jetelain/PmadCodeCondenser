namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Represents a comment extracted from source code that exceeds the verbosity threshold.
/// </summary>
public sealed record VerboseComment(
    string FilePath,
    int StartLine,
    int EndLine,
    int SpanStart,
    int SpanLength,
    CommentKind Kind,
    string OriginalText,
    string LeadingWhitespace)
{
    /// <summary>
    /// Gets the SHA256 hash of the trimmed original comment text, used for cache lookup and deduplication.
    /// </summary>
    public string ContentHash { get; init; } = ComputeSha256(OriginalText.Trim());

    /// <summary>
    /// Computes the SHA256 hex string of the given text.
    /// </summary>
    public static string ComputeSha256(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
