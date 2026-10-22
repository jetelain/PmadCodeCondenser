using Pmad.AI.Tokens;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Comments;

/// <summary>
/// Provides utilities for analyzing comment verbosity, guarding against LLM summaries that are
/// more verbose than original comments, and calculating character and token gains from condensation.
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

        if (IsMoreVerbose(summary, comment))
        {
            return 0;
        }

        int originalLen = comment.OriginalText.Replace("\r\n", "\n").Trim().Length;
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

        if (IsMoreVerbose(summary, comment))
        {
            return (0, 0);
        }

        var effEstimator = estimator ?? HeuristicTokenEstimator.Default;
        var trimmedOrig = comment.OriginalText.Replace("\r\n", "\n").Trim();
        var trimmedSummary = summary.Replace("\r\n", "\n").Trim();

        int originalLen = trimmedOrig.Length;
        int condensedLen = trimmedSummary.Length;
        int gainChars = Math.Max(0, originalLen - condensedLen);

        int originalTokens = effEstimator.Estimate(trimmedOrig.AsSpan());
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

                if (kept)
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
}
