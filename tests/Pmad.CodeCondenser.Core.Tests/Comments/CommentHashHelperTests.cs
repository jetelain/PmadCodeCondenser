using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Comments;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Tests.Comments;

public class CommentHashHelperTests
{
    [Fact]
    public void GetSummaryHashes_PlainText_ReturnsRawLineAndBlockHash()
    {
        var summary = "Value is truncated by AgentTranscript.MaxEntryChars; newlines are escaped for single-line readability.";
        var hashes = CommentHashHelper.GetSummaryHashes(summary).ToList();

        var rawHash = VerboseComment.ComputeSha256(summary.Trim());
        var lineHash = VerboseComment.ComputeSha256($"// {summary.Trim()}");
        var blockHash = VerboseComment.ComputeSha256($"/* {summary.Trim()} */");

        hashes.Should().Contain(rawHash);
        hashes.Should().Contain(lineHash);
        hashes.Should().Contain(blockHash);
    }

    [Fact]
    public void GetSummaryHashes_PrefixedWithSlashes_ReturnsBothWithAndWithoutSlashes()
    {
        var rawText = "Value is truncated by AgentTranscript.MaxEntryChars; newlines are escaped for single-line readability.";
        var comment = $"// {rawText}";
        var hashes = CommentHashHelper.GetSummaryHashes(comment).ToList();

        var withSlashesHash = VerboseComment.ComputeSha256(comment.Trim());
        var strippedHash = VerboseComment.ComputeSha256(rawText.Trim());

        hashes.Should().Contain(withSlashesHash);
        hashes.Should().Contain(strippedHash);
    }

    [Fact]
    public void GetSummaryHashes_PrefixedWithBlockMarkers_ReturnsBothWithAndWithoutMarkers()
    {
        var rawText = "Value is truncated by AgentTranscript.MaxEntryChars; newlines are escaped for single-line readability.";
        var comment = $"/* {rawText} */";
        var hashes = CommentHashHelper.GetSummaryHashes(comment).ToList();

        var withMarkersHash = VerboseComment.ComputeSha256(comment.Trim());
        var strippedHash = VerboseComment.ComputeSha256(rawText.Trim());

        hashes.Should().Contain(withMarkersHash);
        hashes.Should().Contain(strippedHash);
    }

    [Fact]
    public void GetSummaryHashes_MultiLineText_ReturnsBlockHash()
    {
        var summary = "Line 1 of comment\nLine 2 of comment";
        var hashes = CommentHashHelper.GetSummaryHashes(summary).ToList();

        var rawHash = VerboseComment.ComputeSha256(summary.Trim());
        var blockHash = VerboseComment.ComputeSha256($"/* {summary.Trim()} */");

        hashes.Should().Contain(rawHash);
        hashes.Should().Contain(blockHash);
    }

    [Fact]
    public void GetSummaryHashes_XmlDocComment_ReturnsXmlTextHash()
    {
        var summary = "/// <summary>\n/// Concise summary exceeding fifty characters.\n/// </summary>";
        var hashes = CommentHashHelper.GetSummaryHashes(summary).ToList();

        var rawHash = VerboseComment.ComputeSha256(summary.Trim());
        var xmlText = XmlDocumentationParser.ExtractXmlText(summary).Trim();
        var xmlHash = VerboseComment.ComputeSha256(xmlText);

        hashes.Should().Contain(rawHash);
        hashes.Should().Contain(xmlHash);
    }

    [Fact]
    public void GetSummaryHashes_EmptyOrWhitespace_ReturnsEmpty()
    {
        CommentHashHelper.GetSummaryHashes("").Should().BeEmpty();
        CommentHashHelper.GetSummaryHashes("   \t\n").Should().BeEmpty();
    }

    [Fact]
    public void IsCommentIgnored_SingleLineComment_MatchesBareSummaryHash()
    {
        var bareSummary = "Value is truncated by AgentTranscript.MaxEntryChars; newlines are escaped for single-line readability.";
        var bareHash = VerboseComment.ComputeSha256(bareSummary);
        var ignoredHashes = new HashSet<string> { bareHash };

        var trivia = SyntaxFactory.Comment($"// {bareSummary}");
        bool isIgnored = CommentHashHelper.IsCommentIgnored(trivia, trivia.ToString(), CommentKind.SingleLine, ignoredHashes);

        isIgnored.Should().BeTrue();
    }

    [Fact]
    public void IsCommentIgnored_MultiLineComment_MatchesBareSummaryHash()
    {
        var bareSummary = "Value is truncated by AgentTranscript.MaxEntryChars; newlines are escaped for single-line readability.";
        var bareHash = VerboseComment.ComputeSha256(bareSummary);
        var ignoredHashes = new HashSet<string> { bareHash };

        var trivia = SyntaxFactory.Comment($"/* {bareSummary} */");
        bool isIgnored = CommentHashHelper.IsCommentIgnored(trivia, trivia.ToString(), CommentKind.MultiLine, ignoredHashes);

        isIgnored.Should().BeTrue();
    }

    [Fact]
    public void IsCommentIgnored_XmlDocComment_MatchesXmlTextHash()
    {
        var xmlDoc = "/// <summary>\n/// Concise summary exceeding fifty characters.\n/// </summary>";
        var xmlText = XmlDocumentationParser.ExtractXmlText(xmlDoc).Trim();
        var xmlHash = VerboseComment.ComputeSha256(xmlText);
        var ignoredHashes = new HashSet<string> { xmlHash };

        var tree = CSharpSyntaxTree.ParseText(xmlDoc + "\npublic class C {}");
        var trivia = tree.GetRoot().GetLeadingTrivia().First(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));

        bool isIgnored = CommentHashHelper.IsCommentIgnored(trivia, trivia.ToString(), CommentKind.XmlDocumentation, ignoredHashes);

        isIgnored.Should().BeTrue();
    }

    [Fact]
    public void IsCommentIgnored_WhenNoMatch_ReturnsFalse()
    {
        var ignoredHashes = new HashSet<string> { "nonexistent-hash" };
        var trivia = SyntaxFactory.Comment("// Unrelated comment");

        bool isIgnored = CommentHashHelper.IsCommentIgnored(trivia, trivia.ToString(), CommentKind.SingleLine, ignoredHashes);

        isIgnored.Should().BeFalse();
    }

    [Fact]
    public void IsCommentIgnored_WhenEmptyHashes_ReturnsFalse()
    {
        var ignoredHashes = new HashSet<string>();
        var trivia = SyntaxFactory.Comment("// Any comment");

        bool isIgnored = CommentHashHelper.IsCommentIgnored(trivia, trivia.ToString(), CommentKind.SingleLine, ignoredHashes);

        isIgnored.Should().BeFalse();
    }

    [Fact]
    public void IsCommentIgnored_IndentedAndWrappedXmlDocComment_MatchesUnindentedSummaryVariation()
    {
        // Unindented summary text that was stored in .codecondenser/summarized-comments.json
        var unindentedSummary = """
            /// <summary>
            /// Draws images from a prompt artifact.
            /// </summary>
            /// <remarks>
            /// File extension matches the model output format (e.g. .png for SD, .jpg for flux). For local GGUF SD models, use SDXL architecture with 1024x1024 resolution.
            /// </remarks>
            """;

        var ignoredHashes = new HashSet<string>(StringComparer.Ordinal);
        CommentHashHelper.AddSummaryVariations(ignoredHashes, unindentedSummary, CommentKind.XmlDocumentation);

        // On-disk indented and wrapped trivia (e.g. 4 spaces indentation, narrower wrapping)
        var code = """
            public class ImageDrawer
            {
                /// <summary>
                /// Draws images from a prompt artifact.
                /// </summary>
                /// <remarks>
                /// File extension matches the model output format (e.g. .png for SD, .jpg
                /// for flux). For local GGUF SD models, use SDXL architecture with
                /// 1024x1024 resolution.
                /// </remarks>
                public void Draw() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var trivia = tree.GetRoot().DescendantTrivia().First(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));

        bool isIgnored = CommentHashHelper.IsCommentIgnored(trivia, trivia.ToString(), CommentKind.XmlDocumentation, ignoredHashes);

        isIgnored.Should().BeTrue();
    }

    [Fact]
    public void IsCommentIgnored_SingleLineComment_MatchesWhitespaceCollapsedVariation()
    {
        var summary = "// Summary with concise content.";
        var ignoredHashes = new HashSet<string>(StringComparer.Ordinal);
        CommentHashHelper.AddSummaryVariations(ignoredHashes, summary, CommentKind.SingleLine);

        // Multiple spaces between words
        var trivia = SyntaxFactory.Comment("//  Summary   with   concise   content. ");

        bool isIgnored = CommentHashHelper.IsCommentIgnored(trivia, trivia.ToString(), CommentKind.SingleLine, ignoredHashes);

        isIgnored.Should().BeTrue();
    }

    [Fact]
    public void GetSummaryHashes_MultiLineSingleLineComments_ReturnsStrippedHash()
    {
        var comment = "// Line 1 of comment\r\n// Line 2 of comment";
        var hashes = CommentHashHelper.GetSummaryHashes(comment, CommentKind.SingleLine).ToList();

        var rawHash = VerboseComment.ComputeSha256(comment.Trim());
        var strippedText = "Line 1 of comment\nLine 2 of comment";
        var strippedHash = VerboseComment.ComputeSha256(strippedText);

        hashes.Should().Contain(rawHash);
        hashes.Should().Contain(strippedHash);
    }

    [Fact]
    public void GetSummaryHashes_MultiLineSingleLineComments_DoesNotContainHalfStrippedHash()
    {
        var comment = "// Line 1 of comment\r\n// Line 2 of comment";
        var hashes = CommentHashHelper.GetSummaryHashes(comment, CommentKind.SingleLine).ToList();

        // The half-stripped variation where only line 1 is stripped should NOT be produced
        var halfStrippedText = "Line 1 of comment\r\n// Line 2 of comment";
        var halfStrippedHash = VerboseComment.ComputeSha256(halfStrippedText);

        hashes.Should().NotContain(halfStrippedHash);
    }

    [Fact]
    public void GetSummaryHashes_BareMultiLineSummary_ReturnsBothSingleLineAndMultiLineVariations()
    {
        var summary = "Line 1 of comment\r\nLine 2 of comment";
        var hashes = CommentHashHelper.GetSummaryHashes(summary, CommentKind.SingleLine).ToList();

        var singleLineVariationLf = "// Line 1 of comment\n// Line 2 of comment";
        var singleLineVariationCrlf = "// Line 1 of comment\r\n// Line 2 of comment";
        var strippedVariationLf = "Line 1 of comment\nLine 2 of comment";

        hashes.Should().Contain(VerboseComment.ComputeSha256(singleLineVariationLf));
        hashes.Should().Contain(VerboseComment.ComputeSha256(singleLineVariationCrlf));
        hashes.Should().Contain(VerboseComment.ComputeSha256(strippedVariationLf));
    }

    [Fact]
    public void GetSummaryHashes_MultilineComment_YieldsBothLfAndCrlfVariations()
    {
        var comment = "// Line 1 of comment\r\n// Line 2 of comment";
        var hashes = CommentHashHelper.GetSummaryHashes(comment).ToList();

        var lfHash = VerboseComment.ComputeSha256("// Line 1 of comment\n// Line 2 of comment");
        var crlfHash = VerboseComment.ComputeSha256("// Line 1 of comment\r\n// Line 2 of comment");

        hashes.Should().Contain(lfHash);
        hashes.Should().Contain(crlfHash);
    }

    [Fact]
    public void IsCommentIgnored_MultiLineSingleLineComment_MatchesBareSummaryWithCrlf()
    {
        var summary = "Line 1 of comment\r\nLine 2 of comment";
        var ignoredHashes = new HashSet<string>(StringComparer.Ordinal);
        CommentHashHelper.AddSummaryVariations(ignoredHashes, summary);

        var trivia = SyntaxFactory.Comment("// Line 1 of comment");
        // Source text has LF
        var text = "// Line 1 of comment\n// Line 2 of comment";

        bool isIgnored = CommentHashHelper.IsCommentIgnored(trivia, text, CommentKind.SingleLine, ignoredHashes);

        isIgnored.Should().BeTrue();
    }

    [Fact]
    public void IsCommentIgnored_MultiLineSingleLineComment_DoesNotIgnoreGroupWhenOnlyFirstLineIsWhitelisted()
    {
        var firstLineOnly = "// Line 1 of comment";
        var ignoredHashes = new HashSet<string>(StringComparer.Ordinal)
        {
            VerboseComment.ComputeSha256(firstLineOnly)
        };

        var trivia = SyntaxFactory.Comment("// Line 1 of comment");
        var groupText = "// Line 1 of comment\r\n// Line 2 of long verbose comment that must not be ignored";

        bool isIgnored = CommentHashHelper.IsCommentIgnored(trivia, groupText, CommentKind.SingleLine, ignoredHashes);

        isIgnored.Should().BeFalse();
    }
}

