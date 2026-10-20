using FluentAssertions;
using Microsoft.CodeAnalysis;
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

    [Fact]
    public void Extract_XmlComment_WithMultipleShortTags_TotalLengthOver100_IsNotExtracted()
    {
        var code = """
            public class TestClass
            {
                /// <summary>
                /// Gets or sets user identifier.
                /// </summary>
                /// <param name="id">The user ID.</param>
                /// <param name="name">The name.</param>
                /// <returns>True if created.</returns>
                public void Execute(int id, string name) {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };

        var comments = _extractor.Extract(tree, "TestClass.cs", settings);

        // Even though total raw text of comment is well over 100 characters,
        // none of the root tags exceed their threshold (summary < 100, param < 50, returns < 100).
        comments.Should().BeEmpty();
    }

    [Fact]
    public void Extract_XmlComment_WithVerboseParam_IsExtracted()
    {
        var longParamText = "This parameter represents the user ID that was generated during initial registration and must be positive.";
        var code = $$"""
            public class TestClass
            {
                /// <summary>
                /// Short summary.
                /// </summary>
                /// <param name="id">{{longParamText}}</param>
                public void Execute(int id) {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };

        var comments = _extractor.Extract(tree, "TestClass.cs", settings);

        // Summary is short (< 100), but param is 107 characters (>= default 50 threshold for param)
        comments.Should().HaveCount(1);
        comments[0].Kind.Should().Be(CommentKind.XmlDocumentation);
        comments[0].OriginalText.Should().Contain(longParamText);
    }

    [Fact]
    public void Extract_XmlComment_WithVerboseRemarks_IsExtracted()
    {
        var longRemarksText = new string('m', 120);
        var code = $$"""
            public class TestClass
            {
                /// <summary>
                /// Short summary.
                /// </summary>
                /// <remarks>
                /// {{longRemarksText}}
                /// </remarks>
                public void Execute() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };

        var comments = _extractor.Extract(tree, "TestClass.cs", settings);

        comments.Should().HaveCount(1);
        comments[0].Kind.Should().Be(CommentKind.XmlDocumentation);
    }

    [Fact]
    public void Extract_XmlComment_WithCustomThresholds_RespectsXmlSettings()
    {
        var code = """
            public class TestClass
            {
                /// <summary>
                /// A medium length summary that has around sixty characters here.
                /// </summary>
                /// <param name="x">Param with thirty characters.</param>
                public void Execute(int x) {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);

        // Under default settings: summary (~65 chars < 100) and param (~30 chars < 50) are not verbose
        var defaultSettings = new CodeCondenserSettings { CommentLengthThreshold = 100 };
        _extractor.Extract(tree, "TestClass.cs", defaultSettings).Should().BeEmpty();

        // Under custom settings with XmlSummaryLengthThreshold = 50: summary is verbose (65 >= 50)
        var summarySettings = new CodeCondenserSettings
        {
            CommentLengthThreshold = 100,
            XmlSummaryLengthThreshold = 50
        };
        _extractor.Extract(tree, "TestClass.cs", summarySettings).Should().HaveCount(1);

        // Under custom settings with XmlParamLengthThreshold = 25: param is verbose (30 >= 25)
        var paramSettings = new CodeCondenserSettings
        {
            CommentLengthThreshold = 100,
            XmlParamLengthThreshold = 25
        };
        _extractor.Extract(tree, "TestClass.cs", paramSettings).Should().HaveCount(1);
    }
}
