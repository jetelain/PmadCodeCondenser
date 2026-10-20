using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Comments;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Comments;

public class CommentIgnoreMarkerTests
{
    [Theory]
    [InlineData("// codecondenser:ignore")]
    [InlineData("// codecondenser:ignore-comment")]
    [InlineData("// codecondenser-ignore")]
    [InlineData("// nocondense")]
    [InlineData("/* nocondense */")]
    [InlineData("/// <summary condense=\"no\">Summary text</summary>")]
    [InlineData("/// <summary condense=\"false\">Summary text</summary>")]
    [InlineData("/// <summary condense=\"never\">Summary text</summary>")]
    [InlineData("/// <summary condense=\"none\">Summary text</summary>")]
    [InlineData("/// <summary condense=\"off\">Summary text</summary>")]
    [InlineData("/// <summary condense='no'>Summary text</summary>")]
    [InlineData("/// <summary condense = \"no\">Summary text</summary>")]
    [InlineData("/// <summary nocondense=\"true\">Summary text</summary>")]
    [InlineData("/// <summary nocondense=\"yes\">Summary text</summary>")]
    [InlineData("/// <summary nocondense=\"1\">Summary text</summary>")]
    [InlineData("/// <summary nocondense>Summary text</summary>")]
    [InlineData("/// <summary codecondenser=\"ignore\">Summary text</summary>")]
    [InlineData("/// <remarks condense=\"no\">Remarks text</remarks>")]
    [InlineData("/// <param name=\"param1\" condense=\"no\">Description</param>")]
    public void IsIgnored_MatchingMarkers_ReturnsTrue(string commentText)
    {
        CommentIgnoreMarker.IsIgnored(commentText).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("// Normal single-line comment.")]
    [InlineData("/* Normal multi-line comment. */")]
    [InlineData("/// <summary>Normal XML summary.</summary>")]
    [InlineData("/// <summary condense=\"yes\">Summary text</summary>")]
    [InlineData("/// <summary condense=\"true\">Summary text</summary>")]
    [InlineData("// This comment mentions condensing code but has no marker.")]
    public void IsIgnored_NonMatchingMarkers_ReturnsFalse(string commentText)
    {
        CommentIgnoreMarker.IsIgnored(commentText).Should().BeFalse();
    }

    [Fact]
    public void IsPrecedingTriviaIgnored_WhenPrecedingCommentHasIgnoreMarker_ReturnsTrue()
    {
        var code = """
            // codecondenser:ignore
            // This is the actual comment
            var x = 1;
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();
        var token = root.FindToken(code.IndexOf("var", StringComparison.Ordinal));
        var leadingTrivia = token.LeadingTrivia;

        // The second comment is index 2 (index 0 is first comment, index 1 is newline)
        var actualCommentIndex = -1;
        for (int i = 0; i < leadingTrivia.Count; i++)
        {
            if (leadingTrivia[i].ToString().Contains("actual comment"))
            {
                actualCommentIndex = i;
                break;
            }
        }

        CommentIgnoreMarker.IsPrecedingTriviaIgnored(leadingTrivia, actualCommentIndex).Should().BeTrue();
    }

    [Fact]
    public void IsPrecedingTriviaIgnored_WhenPrecedingCommentHasNoIgnoreMarker_ReturnsFalse()
    {
        var code = """
            // Unrelated comment
            // This is the actual comment
            var x = 1;
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();
        var token = root.FindToken(code.IndexOf("var", StringComparison.Ordinal));
        var leadingTrivia = token.LeadingTrivia;

        var actualCommentIndex = -1;
        for (int i = 0; i < leadingTrivia.Count; i++)
        {
            if (leadingTrivia[i].ToString().Contains("actual comment"))
            {
                actualCommentIndex = i;
                break;
            }
        }

        CommentIgnoreMarker.IsPrecedingTriviaIgnored(leadingTrivia, actualCommentIndex).Should().BeFalse();
    }
}
