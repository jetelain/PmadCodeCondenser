using Microsoft.CodeAnalysis;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Comments;

/// <summary>
/// Provides hashing and variation resolution for comments and their LLM-summarized representations,
/// ensuring consistent whitelisting across formatted syntax trivia and persistent stores.
/// </summary>
public static class CommentHashHelper
{
    /// <summary>
    /// Generates all valid SHA256 hashes for a summarized comment text (raw text, single-line trivia,
    /// multi-line trivia, and XML text fragments), allowing accurate lookup regardless of formatting.
    /// </summary>
    /// <param name="summaryText">The summarized comment text.</param>
    /// <param name="kind">Optional comment kind to prioritize matching formatting.</param>
    /// <returns>Sequence of unique SHA256 hashes corresponding to variations of the summary.</returns>
    public static IEnumerable<string> GetSummaryHashes(string summaryText, CommentKind? kind = null)
    {
        if (string.IsNullOrWhiteSpace(summaryText))
        {
            yield break;
        }

        var trimmed = summaryText.Trim();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var rawHash = VerboseComment.ComputeSha256(trimmed);
        if (seen.Add(rawHash))
        {
            yield return rawHash;
        }

        if (!trimmed.StartsWith("//") && !trimmed.StartsWith("/*") && !trimmed.StartsWith("///"))
        {
            if (trimmed.Contains('\n') || trimmed.Contains('\r'))
            {
                var sanitized = trimmed.Replace("*/", "* /");
                var blockHash = VerboseComment.ComputeSha256($"/* {sanitized} */");
                if (seen.Add(blockHash))
                {
                    yield return blockHash;
                }
            }
            else
            {
                var lineHash = VerboseComment.ComputeSha256($"// {trimmed}");
                if (seen.Add(lineHash))
                {
                    yield return lineHash;
                }

                var sanitized = trimmed.Replace("*/", "* /");
                var blockHash = VerboseComment.ComputeSha256($"/* {sanitized} */");
                if (seen.Add(blockHash))
                {
                    yield return blockHash;
                }
            }
        }
        else if (trimmed.StartsWith("//"))
        {
            var stripped = trimmed.TrimStart('/').Trim();
            if (!string.IsNullOrEmpty(stripped))
            {
                var strippedHash = VerboseComment.ComputeSha256(stripped);
                if (seen.Add(strippedHash))
                {
                    yield return strippedHash;
                }
            }
        }
        else if (trimmed.StartsWith("/*") && trimmed.EndsWith("*/"))
        {
            var stripped = trimmed[2..^2].Trim();
            if (!string.IsNullOrEmpty(stripped))
            {
                var strippedHash = VerboseComment.ComputeSha256(stripped);
                if (seen.Add(strippedHash))
                {
                    yield return strippedHash;
                }

                var unescapedHash = VerboseComment.ComputeSha256(stripped.Replace("* /", "*/"));
                if (seen.Add(unescapedHash))
                {
                    yield return unescapedHash;
                }
            }
        }

        var xmlText = XmlDocumentationParser.ExtractXmlText(summaryText).Trim();
        if (!string.IsNullOrWhiteSpace(xmlText) && !string.Equals(xmlText, trimmed, StringComparison.Ordinal))
        {
            var xmlHash = VerboseComment.ComputeSha256(xmlText);
            if (seen.Add(xmlHash))
            {
                yield return xmlHash;
            }
        }
    }

    /// <summary>
    /// Adds all variation hashes for a summarized comment into the target hash set.
    /// </summary>
    /// <param name="targetHashes">The hash set to receive variation hashes.</param>
    /// <param name="summaryText">The summarized comment text.</param>
    /// <param name="kind">Optional comment kind.</param>
    public static void AddSummaryVariations(ISet<string> targetHashes, string? summaryText, CommentKind? kind = null)
    {
        ArgumentNullException.ThrowIfNull(targetHashes);

        if (string.IsNullOrWhiteSpace(summaryText))
        {
            return;
        }

        foreach (var hash in GetSummaryHashes(summaryText, kind))
        {
            targetHashes.Add(hash);
        }
    }

    /// <summary>
    /// Determines whether a comment trivia should be ignored based on matching against known ignored comment hashes.
    /// Supports matching against raw trivia, trimmed text, stripped comment delimiters (//, /* */), and extracted XML doc text.
    /// </summary>
    /// <param name="trivia">The Roslyn syntax trivia representing the comment.</param>
    /// <param name="text">The string content of the trivia.</param>
    /// <param name="kind">The kind of comment.</param>
    /// <param name="ignoredHashes">The set of ignored comment hashes.</param>
    /// <returns><c>true</c> if the comment matches an ignored hash; otherwise, <c>false</c>.</returns>
    public static bool IsCommentIgnored(
        SyntaxTrivia trivia,
        string text,
        CommentKind kind,
        ISet<string> ignoredHashes)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(ignoredHashes);

        if (ignoredHashes.Count == 0)
        {
            return false;
        }

        var trimmed = text.Trim();
        var hash = VerboseComment.ComputeSha256(trimmed);
        if (ignoredHashes.Contains(hash))
        {
            return true;
        }

        var fullTrimmed = trivia.ToFullString().Trim();
        if (!string.Equals(trimmed, fullTrimmed, StringComparison.Ordinal))
        {
            var fullHash = VerboseComment.ComputeSha256(fullTrimmed);
            if (ignoredHashes.Contains(fullHash))
            {
                return true;
            }
        }

        if (kind == CommentKind.SingleLine && trimmed.StartsWith("//"))
        {
            var stripped = trimmed.TrimStart('/').Trim();
            if (!string.IsNullOrEmpty(stripped) &&
                ignoredHashes.Contains(VerboseComment.ComputeSha256(stripped)))
            {
                return true;
            }
        }
        else if (kind == CommentKind.MultiLine && trimmed.StartsWith("/*") && trimmed.EndsWith("*/"))
        {
            var stripped = trimmed[2..^2].Trim();
            if (!string.IsNullOrEmpty(stripped))
            {
                if (ignoredHashes.Contains(VerboseComment.ComputeSha256(stripped)) ||
                    ignoredHashes.Contains(VerboseComment.ComputeSha256(stripped.Replace("* /", "*/"))))
                {
                    return true;
                }
            }
        }
        else if (kind == CommentKind.XmlDocumentation)
        {
            var xmlText = XmlDocumentationParser.ExtractXmlText(text).Trim();
            if (!string.IsNullOrEmpty(xmlText) &&
                ignoredHashes.Contains(VerboseComment.ComputeSha256(xmlText)))
            {
                return true;
            }
        }

        return false;
    }
}
