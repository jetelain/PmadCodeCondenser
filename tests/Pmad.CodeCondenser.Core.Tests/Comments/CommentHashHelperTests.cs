using FluentAssertions;
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
}
