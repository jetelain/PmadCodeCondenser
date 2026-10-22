using FluentAssertions;
using Pmad.AI.Tokens;
using Pmad.CodeCondenser.Core.Comments;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Tests.Comments;

public class CommentVerbosityHelperTests
{
    private static VerboseComment CreateComment(string text, CommentKind kind = CommentKind.SingleLine)
    {
        return new VerboseComment(
            FilePath: "Test.cs",
            StartLine: 1,
            EndLine: 1,
            SpanStart: 0,
            SpanLength: text.Length,
            Kind: kind,
            OriginalText: text,
            LeadingWhitespace: "    ");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsMoreVerbose_NullOrWhiteSpaceSuggested_ReturnsFalse(string? suggested)
    {
        var comment = CreateComment("// Original comment text");
        CommentVerbosityHelper.IsMoreVerbose(suggested, comment).Should().BeFalse();
        CommentVerbosityHelper.IsMoreVerbose(suggested, "// Original comment text", CommentKind.SingleLine).Should().BeFalse();
    }

    [Fact]
    public void IsMoreVerbose_SingleLine_SuggestedIsLonger_ReturnsTrue()
    {
        var comment = CreateComment("// Short comment");
        var suggested = "// This is a much longer and more verbose comment suggested by LLM";

        CommentVerbosityHelper.IsMoreVerbose(suggested, comment).Should().BeTrue();
    }

    [Fact]
    public void IsMoreVerbose_SingleLine_SuggestedIsShorter_ReturnsFalse()
    {
        var comment = CreateComment("// This is a verbose comment that should be condensed into fewer words");
        var suggested = "// Condensed text";

        CommentVerbosityHelper.IsMoreVerbose(suggested, comment).Should().BeFalse();
    }

    [Fact]
    public void IsMoreVerbose_SingleLine_SuggestedIsEqualLength_ReturnsFalse()
    {
        var comment = CreateComment("// Comment length 24");
        var suggested = "// Comment length 24";

        CommentVerbosityHelper.IsMoreVerbose(suggested, comment).Should().BeFalse();
    }

    [Fact]
    public void IsMoreVerbose_MultiLine_SuggestedIsLonger_ReturnsTrue()
    {
        var comment = CreateComment("/* Short block */", CommentKind.MultiLine);
        var suggested = "/* Much longer block comment that contains multiple detailed explanations */";

        CommentVerbosityHelper.IsMoreVerbose(suggested, comment).Should().BeTrue();
    }

    [Fact]
    public void IsMoreVerbose_XmlDocumentation_SuggestedTextLonger_ReturnsTrue()
    {
        var original = """
            /// <summary>
            /// Calculates total score.
            /// </summary>
            """;
        var comment = CreateComment(original, CommentKind.XmlDocumentation);
        var suggested = """
            /// <summary>
            /// Calculates the complete and exhaustive total score by aggregating all criteria values across evaluations.
            /// </summary>
            """;

        CommentVerbosityHelper.IsMoreVerbose(suggested, comment).Should().BeTrue();
    }

    [Fact]
    public void IsMoreVerbose_XmlDocumentation_SuggestedTextShorter_ReturnsFalse()
    {
        var original = """
            /// <summary>
            /// Calculates the complete and exhaustive total score by aggregating all criteria values across evaluations.
            /// </summary>
            /// <remarks>
            /// Ensure that all values are normalized to a 0-100 scale prior to calling this calculation method.
            /// </remarks>
            """;
        var comment = CreateComment(original, CommentKind.XmlDocumentation);
        var suggested = """
            /// <summary>
            /// Calculates total score.
            /// </summary>
            """;

        CommentVerbosityHelper.IsMoreVerbose(suggested, comment).Should().BeFalse();
    }

    [Fact]
    public void IsMoreVerbose_XmlDocumentation_WithIndentedOriginal_IgnoresIndentationWhenComparing()
    {
        // The original has 16 spaces of indentation on lines 2 and 3
        var original = "/// <summary>\r\n                /// Short summary.\r\n                /// </summary>";
        var comment = CreateComment(original, CommentKind.XmlDocumentation);

        // The suggested has no indentation, but its XML payload is longer
        var suggested = "/// <summary>\r\n/// An unnecessarily extended and more verbose summary.\r\n/// </summary>";

        CommentVerbosityHelper.IsMoreVerbose(suggested, comment).Should().BeTrue();
    }

    [Fact]
    public void CalculateGain_WhenSuggestedIsShorter_ReturnsCharacterDiff()
    {
        var original = "// This is a verbose comment that needs to be shortened"; // 55 chars
        var comment = CreateComment(original);
        var suggested = "// Shortened comment"; // 20 chars

        var gain = CommentVerbosityHelper.CalculateGain(comment, suggested);

        gain.Should().Be(35);
    }

    [Fact]
    public void CalculateGain_WhenSuggestedIsMoreVerbose_ReturnsZero()
    {
        var original = "// Short comment"; // 16 chars
        var comment = CreateComment(original);
        var suggested = "// Much longer comment produced by LLM"; // 38 chars

        var gain = CommentVerbosityHelper.CalculateGain(comment, suggested);

        gain.Should().Be(0);
    }

    [Fact]
    public void CalculateGain_WhenSuggestedIsEqual_ReturnsZero()
    {
        var original = "// Exact same size!"; // 19 chars
        var comment = CreateComment(original);
        var suggested = "// Exact same size!"; // 19 chars

        var gain = CommentVerbosityHelper.CalculateGain(comment, suggested);

        gain.Should().Be(0);
    }

    [Fact]
    public void CalculateGain_WhenSummaryIsNullOrWhitespace_ReturnsZero()
    {
        var comment = CreateComment("// Some comment");
        CommentVerbosityHelper.CalculateGain(comment, null).Should().Be(0);
        CommentVerbosityHelper.CalculateGain(comment, "   ").Should().Be(0);
    }

    [Fact]
    public void CalculateTotalGain_EmptyInputs_ReturnsZeroes()
    {
        var (orig, cond, gain, pct, count) = CommentVerbosityHelper.CalculateTotalGain([], new Dictionary<string, string>());

        orig.Should().Be(0);
        cond.Should().Be(0);
        gain.Should().Be(0);
        pct.Should().Be(0);
        count.Should().Be(0);
    }

    [Fact]
    public void CalculateTotalGain_MixedComments_ComputesAccurateTotalGainAndReduction()
    {
        var comment1 = CreateComment("// " + new string('a', 100)); // 103 chars
        var comment2 = CreateComment("// " + new string('b', 200)); // 203 chars

        var summaries = new Dictionary<string, string>
        {
            [comment1.ContentHash] = "// " + new string('a', 50), // 53 chars (gain: 50)
            [comment2.ContentHash] = "// " + new string('b', 100)  // 103 chars (gain: 100)
        };

        var (orig, cond, gain, pct, count) = CommentVerbosityHelper.CalculateTotalGain([comment1, comment2], summaries);

        orig.Should().Be(306);
        cond.Should().Be(156);
        gain.Should().Be(150);
        pct.Should().Be(49);
        count.Should().Be(2);
    }

    [Fact]
    public void CalculateTotalGain_WhenOneCommentIsMoreVerbose_TreatsMoreVerboseAsKeptWithZeroGain()
    {
        var comment1 = CreateComment("// " + new string('a', 100)); // 103 chars
        var comment2 = CreateComment("// Short");                  // 8 chars

        var summaries = new Dictionary<string, string>
        {
            [comment1.ContentHash] = "// " + new string('a', 50), // 53 chars (gain: 50)
            [comment2.ContentHash] = "// Much longer explanation" // 24 chars (more verbose -> gain 0)
        };

        var (orig, cond, gain, pct, count) = CommentVerbosityHelper.CalculateTotalGain([comment1, comment2], summaries);

        // comment1: 103 -> 53
        // comment2: 8 -> 8 (kept old comment)
        orig.Should().Be(111);
        cond.Should().Be(61);
        gain.Should().Be(50);
        pct.Should().Be(45);
        count.Should().Be(2);
    }

    [Fact]
    public void IsMoreVerbose_DifferentLineEndingsCrlfVsLf_NormalizesConsistently()
    {
        var crlfOriginal = "/* Line 1\r\nLine 2\r\nLine 3 */";
        var lfSuggested = "/* Line 1\nLine 2\nLine 3 */";

        var comment = CreateComment(crlfOriginal, CommentKind.MultiLine);

        // Identical content with different line endings should NOT be considered more verbose
        CommentVerbosityHelper.IsMoreVerbose(lfSuggested, comment).Should().BeFalse();
    }

    [Fact]
    public void IsMoreVerbose_XmlDocumentation_DifferentLineEndingsAndWrapping_ComparesNormalizedPayload()
    {
        // Original has multiple wrapped lines and CRLF
        var original = "/// <summary>\r\n/// Line one of summary.\r\n/// Line two of summary.\r\n/// </summary>";
        // Suggested has a single unwrapped line and LF, but same semantic text
        var suggested = "/// <summary>\n/// Line one of summary. Line two of summary.\n/// </summary>";

        var comment = CreateComment(original, CommentKind.XmlDocumentation);

        CommentVerbosityHelper.IsMoreVerbose(suggested, comment).Should().BeFalse();
    }

    [Fact]
    public void CalculateGain_DifferentLineEndingsCrlfVsLf_CalculatesGainConsistently()
    {
        var crlfOriginal = "// A comment with extra text\r\n";
        var lfSuggested = "// A comment\n";

        var comment = CreateComment(crlfOriginal);
        var gain = CommentVerbosityHelper.CalculateGain(comment, lfSuggested);

        gain.Should().Be(16);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CalculateGainWithTokens_NullOrWhiteSpace_ReturnsZeroGain(string? summary)
    {
        var comment = CreateComment("// A very long comment that exceeds verbosity threshold.");
        var (gainChars, gainTokens) = CommentVerbosityHelper.CalculateGainWithTokens(comment, summary);

        gainChars.Should().Be(0);
        gainTokens.Should().Be(0);
    }

    [Fact]
    public void CalculateGainWithTokens_MoreVerbose_ReturnsZeroGain()
    {
        var comment = CreateComment("// Short comment.");
        var (gainChars, gainTokens) = CommentVerbosityHelper.CalculateGainWithTokens(comment, "// A much longer and more detailed replacement comment.");

        gainChars.Should().Be(0);
        gainTokens.Should().Be(0);
    }

    [Fact]
    public void CalculateGainWithTokens_ValidSummary_ReturnsCorrectGainCharsAndTokens()
    {
        var original = "// " + new string('a', 100);
        var summary = "// " + new string('a', 40);
        var comment = CreateComment(original);

        var (gainChars, gainTokens) = CommentVerbosityHelper.CalculateGainWithTokens(comment, summary);

        gainChars.Should().Be(60);
        gainTokens.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateGainWithTokens_CustomEstimator_UsesSpecifiedEstimator()
    {
        var original = "// This is an extended comment with descriptive sentences.";
        var summary = "// Short summary.";
        var comment = CreateComment(original);

        var (_, defaultTokens) = CommentVerbosityHelper.CalculateGainWithTokens(comment, summary, HeuristicTokenEstimator.Default);
        var (_, qwenTokens) = CommentVerbosityHelper.CalculateGainWithTokens(comment, summary, HeuristicTokenEstimator.Qwen);

        defaultTokens.Should().BeGreaterThan(0);
        qwenTokens.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateTotalGain_WithTokens_ComputesBothCharacterAndTokenMetrics()
    {
        var comment1 = CreateComment("// " + new string('a', 100));
        var comment2 = CreateComment("// " + new string('b', 200));

        var summaries = new Dictionary<string, string>
        {
            [comment1.ContentHash] = "// " + new string('a', 50),
            [comment2.ContentHash] = "// " + new string('b', 100)
        };

        var result = CommentVerbosityHelper.CalculateTotalGain([comment1, comment2], summaries);

        result.OriginalCharacters.Should().Be(306);
        result.CondensedCharacters.Should().Be(156);
        result.GainCharacters.Should().Be(150);
        result.ReductionPercent.Should().Be(49);
        result.SummarizedCount.Should().Be(2);

        result.OriginalTokens.Should().BeGreaterThan(0);
        result.CondensedTokens.Should().BeGreaterThan(0);
        result.GainTokens.Should().BeGreaterThan(0);
        result.TokenReductionPercent.Should().BeGreaterThan(0);

        // Verify 8-parameter deconstruction
        var (origChars, condChars, gainChars, pctChars, count, origTok, condTok, gainTok) = result;
        origChars.Should().Be(306);
        condChars.Should().Be(156);
        gainChars.Should().Be(150);
        pctChars.Should().Be(49);
        count.Should().Be(2);
        origTok.Should().Be(result.OriginalTokens);
        condTok.Should().Be(result.CondensedTokens);
        gainTok.Should().Be(result.GainTokens);
    }

    [Fact]
    public void CalculateTotalGain_Empty_ReturnsEmptyResultWithZeroTokens()
    {
        var result = CommentVerbosityHelper.CalculateTotalGain([], new Dictionary<string, string>());

        result.OriginalCharacters.Should().Be(0);
        result.CondensedCharacters.Should().Be(0);
        result.GainCharacters.Should().Be(0);
        result.ReductionPercent.Should().Be(0);
        result.SummarizedCount.Should().Be(0);
        result.OriginalTokens.Should().Be(0);
        result.CondensedTokens.Should().Be(0);
        result.GainTokens.Should().Be(0);
        result.TokenReductionPercent.Should().Be(0);
    }
}
