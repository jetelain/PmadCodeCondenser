using Pmad.AI.Tokens;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Comments;

/// <summary>
/// Provides utilities for analyzing comment verbosity, guarding against LLM summaries that are
/// more verbose than original comments, and calculating character and token gains from condensation.
/// </summary>
public static class CommentVerbosityHelper
{
    /// <summary>
    /// Determines whether the given text is the marker indicating a comment is useless and should be removed.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <returns><c>true</c> if the text matches the useless comment marker; otherwise, <c>false</c>.</returns>
    public static bool IsUselessComment(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim().Trim('`', '"', '\'', ' ', '\t', '\r', '\n');

        if (trimmed.StartsWith("///", StringComparison.Ordinal))
        {
            trimmed = trimmed[3..].Trim();
        }
        else if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            trimmed = trimmed[2..].Trim();
        }
        else if (trimmed.StartsWith("/*", StringComparison.Ordinal))
        {
            trimmed = trimmed[2..].TrimEnd('*', '/').Trim();
        }

        return trimmed.Equals("Comment is useless", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("Comment is usless", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("Comment is useless", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("Comment is usless", StringComparison.OrdinalIgnoreCase);
    }

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
        if (string.IsNullOrWhiteSpace(suggested) || IsUselessComment(suggested))
        {
            return false;
        }

        var trimmedSuggested = suggested.Replace("\r\n", "\n").Trim();
        var trimmedOriginal = (original ?? string.Empty).Replace("\r\n", "\n").Trim();

        if (kind == CommentKind.XmlDocumentation)
        {
            var origXml = XmlDocumentationParser.NormalizeXmlText(trimmedOriginal);
            var suggXml = XmlDocumentationParser.NormalizeXmlText(trimmedSuggested);

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

        int originalLen = comment.OriginalText.Replace("\r\n", "\n").Trim().Length;

        if (IsUselessComment(summary))
        {
            return originalLen;
        }

        if (IsMoreVerbose(summary, comment))
        {
            return 0;
        }

        int condensedLen = summary.Replace("\r\n", "\n").Trim().Length;

        return Math.Max(0, originalLen - condensedLen);
    }

    /// <summary>
    /// Calculates both character and estimated token gains from replacing a comment with its summary.
    /// Returns (0, 0) if no summary exists, or if the summary was more verbose than or identical to the original comment.
    /// </summary>
    /// <param name="comment">The original verbose comment.</param>
    /// <param name="summary">The summarized comment text.</param>
    /// <param name="estimator">The token estimator to use. Defaults to <see cref="HeuristicTokenEstimator.Default"/>.</param>
    /// <returns>A tuple containing the character gain and estimated token gain.</returns>
    public static (int GainCharacters, int GainTokens) CalculateGainWithTokens(
        VerboseComment comment,
        string? summary,
        HeuristicTokenEstimator? estimator = null)
    {
        ArgumentNullException.ThrowIfNull(comment);

        if (string.IsNullOrWhiteSpace(summary))
        {
            return (0, 0);
        }

        var effEstimator = estimator ?? HeuristicTokenEstimator.Default;
        var trimmedOrig = comment.OriginalText.Replace("\r\n", "\n").Trim();
        int originalLen = trimmedOrig.Length;
        int originalTokens = effEstimator.Estimate(trimmedOrig.AsSpan());

        if (IsUselessComment(summary))
        {
            return (originalLen, originalTokens);
        }

        if (IsMoreVerbose(summary, comment))
        {
            return (0, 0);
        }
        var trimmedSummary = summary.Replace("\r\n", "\n").Trim();
        int condensedLen = trimmedSummary.Length;
        int gainChars = Math.Max(0, originalLen - condensedLen);

        int condensedTokens = effEstimator.Estimate(trimmedSummary.AsSpan());
        int gainTokens = Math.Max(0, originalTokens - condensedTokens);

        return (gainChars, gainTokens);
    }

    /// <summary>
    /// Calculates the aggregated comment gain across a set of comments and summaries,
    /// evaluating both character gains and estimated token gains.
    /// </summary>
    /// <param name="comments">The list of verbose comments.</param>
    /// <param name="summaries">The dictionary of summarized comment replacements.</param>
    /// <param name="estimator">The token estimator to use. Defaults to <see cref="HeuristicTokenEstimator.Default"/>.</param>
    /// <returns>A <see cref="CommentGainResult"/> containing character and token gain metrics.</returns>
    public static CommentGainResult CalculateTotalGain(
        IReadOnlyList<VerboseComment> comments,
        IReadOnlyDictionary<string, string> summaries,
        HeuristicTokenEstimator? estimator = null)
    {
        ArgumentNullException.ThrowIfNull(comments);
        ArgumentNullException.ThrowIfNull(summaries);

        if (comments.Count == 0 || summaries.Count == 0)
        {
            return CommentGainResult.Empty;
        }

        var effEstimator = estimator ?? HeuristicTokenEstimator.Default;

        int totalOriginalChars = 0;
        int totalCondensedChars = 0;
        int totalOriginalTokens = 0;
        int totalCondensedTokens = 0;
        int summarizedCount = 0;

        foreach (var comment in comments)
        {
            if (summaries.TryGetValue(comment.ContentHash, out var summary) &&
                !string.IsNullOrWhiteSpace(summary))
            {
                var trimmedOrig = comment.OriginalText.Replace("\r\n", "\n").Trim();
                int origLen = trimmedOrig.Length;
                int origTokens = effEstimator.Estimate(trimmedOrig.AsSpan());

                bool kept = IsMoreVerbose(summary, comment);
                int condLen;
                int condTokens;

                if (IsUselessComment(summary))
                {
                    condLen = 0;
                    condTokens = 0;
                }
                else if (kept)
                {
                    condLen = origLen;
                    condTokens = origTokens;
                }
                else
                {
                    var trimmedSummary = summary.Replace("\r\n", "\n").Trim();
                    condLen = trimmedSummary.Length;
                    condTokens = effEstimator.Estimate(trimmedSummary.AsSpan());
                }

                totalOriginalChars += origLen;
                totalCondensedChars += condLen;
                totalOriginalTokens += origTokens;
                totalCondensedTokens += condTokens;
                summarizedCount++;
            }
        }

        if (summarizedCount == 0)
        {
            return CommentGainResult.Empty;
        }

        int gainChars = Math.Max(0, totalOriginalChars - totalCondensedChars);
        int gainTokens = Math.Max(0, totalOriginalTokens - totalCondensedTokens);
        int percent = totalOriginalChars > 0
            ? (int)Math.Round((1.0 - (double)totalCondensedChars / totalOriginalChars) * 100)
            : 0;
        percent = Math.Clamp(percent, 0, 100);

        return new CommentGainResult
        {
            OriginalCharacters = totalOriginalChars,
            CondensedCharacters = totalCondensedChars,
            GainCharacters = gainChars,
            ReductionPercent = percent,
            SummarizedCount = summarizedCount,
            OriginalTokens = totalOriginalTokens,
            CondensedTokens = totalCondensedTokens,
            GainTokens = gainTokens
        };
    }

    /// <summary>
    /// Computes the effective content character length of a comment, extracting tag content
    /// for XML documentation comments or trimmed text for regular comments.
    /// </summary>
    /// <param name="text">The comment text.</param>
    /// <param name="kind">The comment kind.</param>
    /// <returns>The non-negative content character length.</returns>
    public static int GetCommentContentLength(string? text, CommentKind kind)
    {
        if (string.IsNullOrWhiteSpace(text) || IsUselessComment(text))
        {
            return 0;
        }

        if (kind == CommentKind.XmlDocumentation)
        {
            var xmlText = XmlDocumentationParser.ExtractXmlText(text);
            if (XmlDocumentationParser.TryParseXmlRootElements(xmlText, out var rootTags, out var outside) && rootTags.Count > 0)
            {
                int totalLen = outside.Trim().Length;
                foreach (var (_, content) in rootTags)
                {
                    totalLen += content.Trim().Length;
                }
                if (totalLen > 0)
                {
                    return totalLen;
                }
            }

            var normalizedXml = XmlDocumentationParser.NormalizeXmlText(text);
            if (!string.IsNullOrEmpty(normalizedXml))
            {
                return normalizedXml.Length;
            }
        }

        return text.Replace("\r\n", "\n").Trim().Length;
    }

    /// <summary>
    /// Determines whether an LLM comment summary requires a retry.
    /// A retry is needed if the summary is longer than before (or invalid/empty),
    /// or if the reduction is above target threshold and above 50% of the initial comment length.
    /// </summary>
    /// <param name="summary">The suggested comment summary.</param>
    /// <param name="comment">The original verbose comment.</param>
    /// <param name="settings">The code condenser settings.</param>
    /// <returns><c>true</c> if a retry is recommended; otherwise, <c>false</c>.</returns>
    public static bool ShouldRetrySummary(
        string? summary,
        VerboseComment comment,
        CodeCondenserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(comment);
        ArgumentNullException.ThrowIfNull(settings);

        return ShouldRetrySummary(summary, comment.OriginalText, comment.Kind, settings);
    }

    /// <summary>
    /// Determines whether an LLM comment summary requires a retry.
    /// A retry is needed if the summary is longer than before (or invalid/empty),
    /// or if the reduction is above target threshold and above 50% of the initial comment length.
    /// </summary>
    /// <param name="summary">The suggested comment summary.</param>
    /// <param name="originalText">The original comment text.</param>
    /// <param name="kind">The comment kind.</param>
    /// <param name="settings">The code condenser settings.</param>
    /// <returns><c>true</c> if a retry is recommended; otherwise, <c>false</c>.</returns>
    public static bool ShouldRetrySummary(
        string? summary,
        string originalText,
        CommentKind kind,
        CodeCondenserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(summary))
        {
            return true;
        }

        if (IsUselessComment(summary))
        {
            return false;
        }

        if (IsMoreVerbose(summary, originalText, kind))
        {
            return true;
        }

        int initialLength = GetCommentContentLength(originalText, kind);
        int summaryLength = GetCommentContentLength(summary, kind);

        // Retry if summary is longer than before
        if (summaryLength >= initialLength)
        {
            return true;
        }

        // Retry if reduction is above target and above 50% of the initial
        int targetThreshold = kind == CommentKind.XmlDocumentation
            ? settings.GetEffectiveXmlSummaryThreshold()
            : settings.CommentLengthThreshold;

        if (summaryLength > targetThreshold && summaryLength > 0.5 * initialLength)
        {
            return true;
        }

        return false;
    }
}
