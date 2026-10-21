using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Comments;

/// <summary>
/// Provides utilities for analyzing comment verbosity, guarding against LLM summaries that are
/// more verbose than original comments, and calculating character gains from condensation.
/// </summary>
public static class CommentVerbosityHelper
{
    /// <summary>
    /// Determines whether a suggested summary is more verbose than the original comment.
    /// </summary>
    /// <param name="suggested">The proposed comment summary.</param>
    /// <param name="comment">The original verbose comment.</param>
    /// <returns><c>true</c> if the suggested summary has greater verbosity than the original; otherwise, <c>false</c>.</returns>
    public static bool IsMoreVerbose(string? suggested, VerboseComment comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        return IsMoreVerbose(suggested, comment.OriginalText, comment.Kind);
    }

    /// <summary>
    /// Determines whether a suggested summary is more verbose than the original comment text.
    /// </summary>
    /// <param name="suggested">The proposed comment summary.</param>
    /// <param name="original">The original comment text.</param>
    /// <param name="kind">The comment kind (single-line, multi-line, or XML documentation).</param>
    /// <returns><c>true</c> if the suggested summary has greater verbosity than the original; otherwise, <c>false</c>.</returns>
    public static bool IsMoreVerbose(string? suggested, string original, CommentKind kind)
    {
        if (string.IsNullOrWhiteSpace(suggested))
        {
            return false;
        }

        var trimmedSuggested = suggested.Trim();
        var trimmedOriginal = (original ?? string.Empty).Trim();

        if (kind == CommentKind.XmlDocumentation)
        {
            var origXml = XmlDocumentationParser.ExtractXmlText(trimmedOriginal).Trim();
            var suggXml = XmlDocumentationParser.ExtractXmlText(trimmedSuggested).Trim();

            if (!string.IsNullOrEmpty(origXml) && !string.IsNullOrEmpty(suggXml))
            {
                return suggXml.Length > origXml.Length;
            }
        }

        return trimmedSuggested.Length > trimmedOriginal.Length;
    }

    /// <summary>
    /// Calculates the character gain (reduction in characters) from replacing a comment with its summary.
    /// Returns 0 if no summary exists, or if the summary was more verbose than or identical to the original comment.
    /// </summary>
    /// <param name="comment">The original verbose comment.</param>
    /// <param name="summary">The summarized comment text.</param>
    /// <returns>The non-negative character gain.</returns>
    public static int CalculateGain(VerboseComment comment, string? summary)
    {
        ArgumentNullException.ThrowIfNull(comment);

        if (string.IsNullOrWhiteSpace(summary))
        {
            return 0;
        }

        if (IsMoreVerbose(summary, comment))
        {
            return 0;
        }

        int originalLen = comment.OriginalText.Trim().Length;
        int condensedLen = summary.Trim().Length;

        return Math.Max(0, originalLen - condensedLen);
    }

    /// <summary>
    /// Calculates the aggregated comment gain across a set of comments and summaries.
    /// </summary>
    /// <param name="comments">The list of verbose comments.</param>
    /// <param name="summaries">The dictionary of summarized comment replacements.</param>
    /// <returns>A tuple containing total original characters, condensed characters, character gain, reduction percentage, and summarized count.</returns>
    public static (int OriginalCharacters, int CondensedCharacters, int GainCharacters, int ReductionPercent, int SummarizedCount) CalculateTotalGain(
        IReadOnlyList<VerboseComment> comments,
        IReadOnlyDictionary<string, string> summaries)
    {
        ArgumentNullException.ThrowIfNull(comments);
        ArgumentNullException.ThrowIfNull(summaries);

        if (comments.Count == 0 || summaries.Count == 0)
        {
            return (0, 0, 0, 0, 0);
        }

        int totalOriginal = 0;
        int totalCondensed = 0;
        int summarizedCount = 0;

        foreach (var comment in comments)
        {
            if (summaries.TryGetValue(comment.ContentHash, out var summary) &&
                !string.IsNullOrWhiteSpace(summary))
            {
                int origLen = comment.OriginalText.Trim().Length;
                int condLen = IsMoreVerbose(summary, comment)
                    ? origLen
                    : summary.Trim().Length;

                totalOriginal += origLen;
                totalCondensed += condLen;
                summarizedCount++;
            }
        }

        if (summarizedCount == 0)
        {
            return (0, 0, 0, 0, 0);
        }

        int gain = Math.Max(0, totalOriginal - totalCondensed);
        int percent = totalOriginal > 0
            ? (int)Math.Round((1.0 - (double)totalCondensed / totalOriginal) * 100)
            : 0;
        percent = Math.Clamp(percent, 0, 100);

        return (totalOriginal, totalCondensed, gain, percent, summarizedCount);
    }
}
