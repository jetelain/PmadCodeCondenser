using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Comments;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Tests.Comments;

public class VerboseCommentExtractorTests
{
    private readonly VerboseCommentExtractor _extractor = new();

    [Fact]
    public void Extract_SingleLineComment_ExceedingThreshold_IsExtracted()
    {
        var longComment = "// " + new string('a', 120);
        var code = $$"""
            public class TestClass
            {
                {{longComment}}
                public void DoSomething() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };

        var comments = _extractor.Extract(tree, "TestClass.cs", settings);

        comments.Should().HaveCount(1);
        var comment = comments[0];
        comment.FilePath.Should().Be("TestClass.cs");
        comment.Kind.Should().Be(CommentKind.SingleLine);
        comment.OriginalText.Should().Be(longComment);
        comment.LeadingWhitespace.Should().Be("    ");
        comment.StartLine.Should().Be(3);
        comment.EndLine.Should().Be(3);
    }

    [Fact]
    public void Extract_SingleLineComment_BelowThreshold_IsIgnored()
    {
        var code = """
            public class TestClass
            {
                // Short comment
                public void DoSomething() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };

        var comments = _extractor.Extract(tree, "TestClass.cs", settings);

        comments.Should().BeEmpty();
    }

    [Fact]
    public void Extract_MultiLineComment_ExceedingThreshold_IsExtracted()
    {
        var code = """
            public class TestClass
            {
                /*
                 * This is a multi-line comment that has sufficient length to surpass the default comment length threshold of 100 characters.
                 * It provides in-depth technical explanation of the subsequent method.
                 */
                public void Calculate() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };

        var comments = _extractor.Extract(tree, "TestClass.cs", settings);

        comments.Should().HaveCount(1);
        var comment = comments[0];
        comment.Kind.Should().Be(CommentKind.MultiLine);
        comment.LeadingWhitespace.Should().Be("    ");
        comment.StartLine.Should().Be(3);
        comment.EndLine.Should().Be(6);
    }

    [Fact]
    public void Extract_MultiLineComment_BelowThreshold_IsIgnored()
    {
        var code = """
            public class TestClass
            {
                /* Short multi-line */
                public void Calculate() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };

        var comments = _extractor.Extract(tree, "TestClass.cs", settings);

        comments.Should().BeEmpty();
    }

    [Fact]
    public void Extract_XmlDocumentationComment_WhenIncludeXmlDocTrue_IsExtracted()
    {
        var code = """
            public class TestClass
            {
                /// <summary>
                /// This is an extensive XML documentation comment providing complete documentation about this method.
                /// It describes parameter requirements, return values, and implementation constraints in depth.
                /// </summary>
                public void Execute() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100, IncludeXmlDocumentation = true };

        var comments = _extractor.Extract(tree, "TestClass.cs", settings);

        comments.Should().HaveCount(1);
        var comment = comments[0];
        comment.Kind.Should().Be(CommentKind.XmlDocumentation);
        comment.OriginalText.Should().Contain("<summary>");
        comment.LeadingWhitespace.Should().Be("    ");
        comment.StartLine.Should().Be(3);
        comment.EndLine.Should().Be(7);
    }

    [Fact]
    public void Extract_XmlDocumentationComment_WhenIncludeXmlDocFalse_IsIgnored()
    {
        var code = """
            public class TestClass
            {
                /// <summary>
                /// This is an extensive XML documentation comment providing complete documentation about this method.
                /// It describes parameter requirements, return values, and implementation constraints in depth.
                /// </summary>
                public void Execute() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100, IncludeXmlDocumentation = false };

        var comments = _extractor.Extract(tree, "TestClass.cs", settings);

        comments.Should().BeEmpty();
    }

    [Fact]
    public void Extract_CommentAtEndOfFile_IsExtracted()
    {
        var longComment = "// " + new string('z', 150);
        var code = $"public class TestClass {{ }}\r\n{longComment}";

        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };

        var comments = _extractor.Extract(tree, "TestClass.cs", settings);

        comments.Should().HaveCount(1);
        comments[0].OriginalText.Should().Be(longComment);
    }

    [Fact]
    public void Extract_MultipleComments_AreExtractedInOrder()
    {
        var longComment1 = "// " + new string('1', 110);
        var longComment2 = "/* " + new string('2', 110) + " */";
        var code = $$"""
            {{longComment1}}
            public class TestClass
            {
                {{longComment2}}
                public void Run() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };

        var comments = _extractor.Extract(tree, "TestClass.cs", settings);

        comments.Should().HaveCount(2);
        comments[0].Kind.Should().Be(CommentKind.SingleLine);
        comments[1].Kind.Should().Be(CommentKind.MultiLine);
    }

    [Fact]
    public void Extract_NullArguments_ThrowsArgumentNullException()
    {
        var settings = new CodeCondenserSettings();
        var tree = CSharpSyntaxTree.ParseText("class C {}");

        var act1 = () => _extractor.Extract(null!, "file.cs", settings);
        var act2 = () => _extractor.Extract(tree, "file.cs", null!);

        act1.Should().Throw<ArgumentNullException>();
        act2.Should().Throw<ArgumentNullException>();
    }
}
