using FluentAssertions;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Tests.Models;

public class VerboseCommentTests
{
    [Fact]
    public void ContentHash_ShouldBeStableAndIgnoreSurroundingWhitespace()
    {
        var comment1 = new VerboseComment(
            "Test.cs",
            1,
            2,
            0,
            50,
            CommentKind.SingleLine,
            "// This is a long verbose comment describing some logic in detail.",
            "    ");

        var comment2 = new VerboseComment(
            "AnotherFile.cs",
            10,
            11,
            100,
            52,
            CommentKind.SingleLine,
            "  // This is a long verbose comment describing some logic in detail.  ",
            "\t");

        comment1.ContentHash.Should().NotBeNullOrWhiteSpace();
        comment1.ContentHash.Should().Be(comment2.ContentHash);
    }
}
