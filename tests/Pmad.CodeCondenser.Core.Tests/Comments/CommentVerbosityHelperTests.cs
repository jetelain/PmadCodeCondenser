using FluentAssertions;
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
}
