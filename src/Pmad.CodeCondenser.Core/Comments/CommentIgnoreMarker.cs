using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Pmad.CodeCondenser.Core.Comments;

/// <summary>
/// Detects markers used to suppress or ignore comment verbosity checks.
/// </summary>
public static class CommentIgnoreMarker
{
    private static readonly string[] MarkerPatterns =
    [
        "codecondenser:ignore",
        "codecondenser:ignore-comment",
        "codecondenser-ignore",
        "nocondense"
    ];

    private static readonly Regex XmlAttributeIgnoreRegex = new(
        """(condense\s*=\s*["'](no|false|never|none|off)["']|codecondenser\s*=\s*["']ignore["']|nocondense\s*=\s*["'](true|yes|1)["'])""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Determines whether the given comment text contains an ignore marker.
    /// </summary>
    /// <param name="commentText">The raw comment text to check.</param>
    /// <returns>True if the comment contains an ignore marker; otherwise, false.</returns>
    public static bool IsIgnored(string commentText)
    {
        if (string.IsNullOrWhiteSpace(commentText))
        {
            return false;
        }

        foreach (var pattern in MarkerPatterns)
        {
            if (commentText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (XmlAttributeIgnoreRegex.IsMatch(commentText))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the trivia preceding a comment at the specified index in a trivia list contains an ignore marker.
    /// </summary>
    /// <param name="triviaList">The trivia list.</param>
    /// <param name="commentIndex">The index of the comment trivia.</param>
    /// <returns>True if an ignore marker immediately precedes the comment; otherwise, false.</returns>
    public static bool IsPrecedingTriviaIgnored(SyntaxTriviaList triviaList, int commentIndex)
    {
        for (int i = commentIndex - 1; i >= 0; i--)
        {
            var trivia = triviaList[i];
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                continue;
            }

            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                return IsIgnored(trivia.ToString());
            }

            break;
        }

        return false;
    }
}
