using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Comments;
using Pmad.CodeCondenser.Core.Configuration;

namespace Pmad.CodeCondenser.Core.Tests.Comments;

public class XmlDocumentationParserTests
{
    [Fact]
    public void ExtractXmlText_SingleLineComment_StripsSlashesAndPreservesXml()
    {
        var raw = """
            /// <summary>
            /// Line 1
            /// Line 2
            /// </summary>
            """;

        var xml = XmlDocumentationParser.ExtractXmlText(raw);

        xml.Should().Contain("<summary>");
        xml.Should().Contain("Line 1");
        xml.Should().Contain("Line 2");
        xml.Should().Contain("</summary>");
        xml.Should().NotContain("///");
    }

    [Fact]
    public void ExtractXmlText_MultiLineComment_StripsDelimitersAndAsterisks()
    {
        var raw = """
            /**
             * <summary>
             * Multi line summary
             * </summary>
             */
            """;

        var xml = XmlDocumentationParser.ExtractXmlText(raw);

        xml.Should().Contain("<summary>");
        xml.Should().Contain("Multi line summary");
        xml.Should().Contain("</summary>");
        xml.Should().NotContain("/**");
        xml.Should().NotContain("*/");
    }

    [Fact]
    public void TryParseXmlRootElements_MultipleRootTags_ParsesAllTags()
    {
        var xml = """
            <summary>
            The summary text.
            </summary>
            <param name="id">The identifier.</param>
            <returns>The result.</returns>
            """;

        var success = XmlDocumentationParser.TryParseXmlRootElements(xml, out var tags, out var outsideText);

        success.Should().BeTrue();
        tags.Should().HaveCount(3);
        tags[0].TagName.Should().Be("summary");
        tags[0].Content.Trim().Should().Be("The summary text.");
        tags[1].TagName.Should().Be("param");
        tags[1].Content.Trim().Should().Be("The identifier.");
        tags[2].TagName.Should().Be("returns");
        tags[2].Content.Trim().Should().Be("The result.");
    }

    [Fact]
    public void TryParseXmlRootElements_WithNestedSeeAndParamref_IncludesTargetNames()
    {
        var xml = """
            <summary>
            Gets the value from <see cref="MyService"/> or <c>null</c>.
            </summary>
            """;

        var success = XmlDocumentationParser.TryParseXmlRootElements(xml, out var tags, out _);

        success.Should().BeTrue();
        tags.Should().HaveCount(1);
        tags[0].TagName.Should().Be("summary");
        tags[0].Content.Should().Contain("MyService");
        tags[0].Content.Should().Contain("null");
    }

    [Fact]
    public void GetTagThreshold_Summary_UsesXmlSummaryOrCommentThreshold()
    {
        var defaultSettings = new CodeCondenserSettings { CommentLengthThreshold = 100 };
        XmlDocumentationParser.GetTagThreshold("summary", defaultSettings).Should().Be(100);

        var customSettings = new CodeCondenserSettings
        {
            CommentLengthThreshold = 100,
            XmlSummaryLengthThreshold = 80
        };
        XmlDocumentationParser.GetTagThreshold("summary", customSettings).Should().Be(80);
    }

    [Fact]
    public void GetTagThreshold_Remarks_UsesXmlRemarksOrXmlSummaryOrCommentThreshold()
    {
        var defaultSettings = new CodeCondenserSettings { CommentLengthThreshold = 100 };
        XmlDocumentationParser.GetTagThreshold("remarks", defaultSettings).Should().Be(100);

        var summarySettings = new CodeCondenserSettings
        {
            CommentLengthThreshold = 100,
            XmlSummaryLengthThreshold = 90
        };
        XmlDocumentationParser.GetTagThreshold("remarks", summarySettings).Should().Be(90);

        var customRemarksSettings = new CodeCondenserSettings
        {
            CommentLengthThreshold = 100,
            XmlSummaryLengthThreshold = 90,
            XmlRemarksLengthThreshold = 70
        };
        XmlDocumentationParser.GetTagThreshold("remarks", customRemarksSettings).Should().Be(70);
    }

    [Fact]
    public void GetTagThreshold_ParamAndTypeparam_UsesXmlParamOrHalfCommentThreshold()
    {
        var defaultSettings = new CodeCondenserSettings { CommentLengthThreshold = 100 };
        XmlDocumentationParser.GetTagThreshold("param", defaultSettings).Should().Be(50);
        XmlDocumentationParser.GetTagThreshold("typeparam", defaultSettings).Should().Be(50);

        var customSettings = new CodeCondenserSettings
        {
            CommentLengthThreshold = 100,
            XmlParamLengthThreshold = 35
        };
        XmlDocumentationParser.GetTagThreshold("param", customSettings).Should().Be(35);
        XmlDocumentationParser.GetTagThreshold("typeparam", customSettings).Should().Be(35);
    }

    [Fact]
    public void GetTagThreshold_ReturnsAndOtherTags_UsesCommentThreshold()
    {
        var settings = new CodeCondenserSettings
        {
            CommentLengthThreshold = 100,
            XmlSummaryLengthThreshold = 80,
            XmlParamLengthThreshold = 40
        };

        XmlDocumentationParser.GetTagThreshold("returns", settings).Should().Be(100);
        XmlDocumentationParser.GetTagThreshold("value", settings).Should().Be(100);
        XmlDocumentationParser.GetTagThreshold("exception", settings).Should().Be(100);
    }

    [Fact]
    public void IsVerbose_WhenAllRootTagsAreBelowThreshold_ReturnsFalse()
    {
        // Combined raw length will be around 170 characters, but each tag individually is concise
        var comment = """
            /// <summary>
            /// Gets the user display name.
            /// </summary>
            /// <param name="userId">The user ID.</param>
            /// <param name="tenantId">The tenant ID.</param>
            /// <returns>The formatted name.</returns>
            """;

        var tree = CSharpSyntaxTree.ParseText($"public class C {{\n{comment}\npublic void F() {{}} }}");
        var trivia = tree.GetRoot().DescendantTrivia().First(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };

        var isVerbose = XmlDocumentationParser.IsVerbose(trivia, comment, settings);

        isVerbose.Should().BeFalse();
    }

    [Fact]
    public void IsVerbose_WhenSummaryExceedsThreshold_ReturnsTrue()
    {
        var longSummary = new string('x', 110);
        var comment = $"""
            /// <summary>
            /// {longSummary}
            /// </summary>
            /// <param name="id">Short.</param>
            """;

        var tree = CSharpSyntaxTree.ParseText($"public class C {{\n{comment}\npublic void F() {{}} }}");
        var trivia = tree.GetRoot().DescendantTrivia().First(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };

        var isVerbose = XmlDocumentationParser.IsVerbose(trivia, comment, settings);

        isVerbose.Should().BeTrue();
    }

    [Fact]
    public void IsVerbose_WhenParamExceedsThreshold_ReturnsTrue()
    {
        // Summary is short (< 100), but param is 60 characters (>= 50 default param threshold)
        var longParam = new string('p', 60);
        var comment = $"""
            /// <summary>
            /// Short summary.
            /// </summary>
            /// <param name="id">{longParam}</param>
            """;

        var tree = CSharpSyntaxTree.ParseText($"public class C {{\n{comment}\npublic void F() {{}} }}");
        var trivia = tree.GetRoot().DescendantTrivia().First(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };

        var isVerbose = XmlDocumentationParser.IsVerbose(trivia, comment, settings);

        isVerbose.Should().BeTrue();
    }

    [Fact]
    public void IsVerbose_WhenRemarksExceedsThreshold_ReturnsTrue()
    {
        var longRemarks = new string('r', 110);
        var comment = $"""
            /// <summary>
            /// Short summary.
            /// </summary>
            /// <remarks>
            /// {longRemarks}
            /// </remarks>
            """;

        var tree = CSharpSyntaxTree.ParseText($"public class C {{\n{comment}\npublic void F() {{}} }}");
        var trivia = tree.GetRoot().DescendantTrivia().First(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };

        var isVerbose = XmlDocumentationParser.IsVerbose(trivia, comment, settings);

        isVerbose.Should().BeTrue();
    }

    [Fact]
    public void IsVerbose_WhenNoXmlTagsPresent_ChecksPlainTextLength()
    {
        var shortPlainComment = "/// Just a short documentation comment without any tags.";
        var longPlainComment = "/// " + new string('t', 120);

        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };

        XmlDocumentationParser.IsVerbose(default, shortPlainComment, settings).Should().BeFalse();
        XmlDocumentationParser.IsVerbose(default, longPlainComment, settings).Should().BeTrue();
    }

    [Fact]
    public void IsVerbose_WithCustomThresholds_RespectsDistinctSettings()
    {
        // Summary has 60 chars, param has 30 chars
        var summary = new string('s', 60);
        var param = new string('p', 30);
        var comment = $"""
            /// <summary>
            /// {summary}
            /// </summary>
            /// <param name="x">{param}</param>
            """;

        var tree = CSharpSyntaxTree.ParseText($"public class C {{\n{comment}\npublic void F() {{}} }}");
        var trivia = tree.GetRoot().DescendantTrivia().First(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));

        // When thresholds are higher than content lengths: not verbose
        var lenientSettings = new CodeCondenserSettings
        {
            CommentLengthThreshold = 100,
            XmlSummaryLengthThreshold = 80,
            XmlParamLengthThreshold = 40
        };
        XmlDocumentationParser.IsVerbose(trivia, comment, lenientSettings).Should().BeFalse();

        // When summary threshold is lower (50): verbose because summary >= 50
        var strictSummarySettings = new CodeCondenserSettings
        {
            CommentLengthThreshold = 100,
            XmlSummaryLengthThreshold = 50,
            XmlParamLengthThreshold = 40
        };
        XmlDocumentationParser.IsVerbose(trivia, comment, strictSummarySettings).Should().BeTrue();

        // When param threshold is lower (25): verbose because param >= 25
        var strictParamSettings = new CodeCondenserSettings
        {
            CommentLengthThreshold = 100,
            XmlSummaryLengthThreshold = 80,
            XmlParamLengthThreshold = 25
        };
        XmlDocumentationParser.IsVerbose(trivia, comment, strictParamSettings).Should().BeTrue();
    }
}
