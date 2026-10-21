using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Comments;

namespace Pmad.CodeCondenser.Core.Tests.Comments;

public class VerboseCommentRewriterTests
{
    private readonly VerboseCommentRewriter _rewriter = new();

    private static string Hash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text.Trim()));
        return Convert.ToHexStringLower(bytes);
    }

    [Fact]
    public void Rewrite_SingleLineComment_WithHashLookup_IsReplaced()
    {
        var originalComment = "// " + new string('a', 120);
        var code = $$"""
            public class TestClass
            {
                {{originalComment}}
                public void DoSomething() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var replacements = new Dictionary<string, string>
        {
            [Hash(originalComment)] = "Short explanation."
        };

        var rewritten = _rewriter.Rewrite(tree, replacements);
        var resultText = rewritten.ToString();

        resultText.Should().Contain("// Short explanation.");
        resultText.Should().NotContain(originalComment);
    }

    [Fact]
    public void Rewrite_SingleLineComment_WithTextLookup_IsReplaced()
    {
        var originalComment = "// " + new string('b', 120);
        var code = $$"""
            public class TestClass
            {
                {{originalComment}}
                public void DoSomething() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var replacements = new Dictionary<string, string>
        {
            [originalComment] = "// Short explanation with prefix."
        };

        var rewritten = _rewriter.Rewrite(tree, replacements);
        var resultText = rewritten.ToString();

        resultText.Should().Contain("// Short explanation with prefix.");
        resultText.Should().NotContain(originalComment);
    }

    [Fact]
    public void Rewrite_SingleLineComment_WithMultilineReplacementStartingWithSlashes_WrapsSafelyInBlockComment()
    {
        var originalComment = "// " + new string('c', 120);
        var code = $$"""
            public class TestClass
            {
                {{originalComment}}
                public void DoSomething() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        // Multiline replacement starting with //
        var replacements = new Dictionary<string, string>
        {
            [originalComment] = "// First summary line.\r\nSecond summary line."
        };

        var rewritten = _rewriter.Rewrite(tree, replacements);
        var resultText = rewritten.ToString();

        resultText.Should().Contain("/* // First summary line.\r\nSecond summary line. */");
        resultText.Should().NotContain(originalComment);
        // Code should remain valid C# that parses with 0 errors
        var reParsed = CSharpSyntaxTree.ParseText(resultText);
        reParsed.GetDiagnostics().Should().BeEmpty();
    }

    [Fact]
    public void Rewrite_MultiLineComment_IsReplaced()
    {
        var originalComment = "/*\r\n     * Very long multi-line comment describing implementation details.\r\n     */";
        var code = $$"""
            public class TestClass
            {
                {{originalComment}}
                public void Calculate() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var replacements = new Dictionary<string, string>
        {
            [Hash(originalComment)] = "Brief logic overview."
        };

        var rewritten = _rewriter.Rewrite(tree, replacements);
        var resultText = rewritten.ToString();

        resultText.Should().Contain("/* Brief logic overview. */");
        resultText.Should().NotContain("Very long multi-line comment");
    }

    [Fact]
    public void Rewrite_XmlDocumentationComment_PreservesIndentation()
    {
        var originalDoc = """
                /// <summary>
                /// This is an extensive XML documentation comment providing complete documentation about this method.
                /// It describes parameter requirements, return values, and implementation constraints in depth.
                /// </summary>
            """;

        var code = $$"""
            public class TestClass
            {
            {{originalDoc}}
                public void Execute() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);

        // Find the extracted doc comment text to hash it exactly
        var extractor = new VerboseCommentExtractor();
        var comments = extractor.Extract(tree, "test.cs", new() { CommentLengthThreshold = 50 });
        comments.Should().HaveCount(1);
        var docComment = comments[0];

        var summaryReplacement = "/// <summary>\r\n/// Compact summary.\r\n/// </summary>";
        var replacements = new Dictionary<string, string>
        {
            [docComment.ContentHash] = summaryReplacement
        };

        var rewritten = _rewriter.Rewrite(tree, replacements);
        var resultText = rewritten.ToString();

        resultText.Should().Contain("    /// <summary>");
        resultText.Should().Contain("    /// Compact summary.");
        resultText.Should().Contain("    /// </summary>");
        resultText.Should().Contain("    public void Execute()");
    }

    [Fact]
    public void Rewrite_NoMatchingReplacements_LeavesCodeUnchanged()
    {
        var code = """
            public class TestClass
            {
                // Unchanged comment
                public void Test() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var replacements = new Dictionary<string, string>
        {
            ["unrelated_hash"] = "Nothing to see here"
        };

        var rewritten = _rewriter.Rewrite(tree, replacements);
        rewritten.ToString().Should().Be(code);
    }

    [Fact]
    public void Rewrite_EmptyReplacements_ReturnsOriginalTree()
    {
        var code = "public class C {}";
        var tree = CSharpSyntaxTree.ParseText(code);

        var rewritten = _rewriter.Rewrite(tree, new Dictionary<string, string>());
        rewritten.Should().BeSameAs(tree);
    }

    [Fact]
    public void Rewrite_NullArguments_ThrowsArgumentNullException()
    {
        var tree = CSharpSyntaxTree.ParseText("class C {}");
        var replacements = new Dictionary<string, string>();

        var act1 = () => _rewriter.Rewrite(null!, replacements);
        var act2 = () => _rewriter.Rewrite(tree, null!);

        act1.Should().Throw<ArgumentNullException>();
        act2.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Rewrite_WithAppliedTriviaTexts_CapturesRewrittenTrivia()
    {
        var originalComment = "// " + new string('x', 120);
        var code = $$"""
            public class TestClass
            {
                {{originalComment}}
                public void DoWork() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var replacements = new Dictionary<string, string>
        {
            [Hash(originalComment)] = "Short explanation."
        };

        var appliedTrivia = new List<string>();
        var rewritten = _rewriter.Rewrite(tree, replacements, appliedTrivia);

        appliedTrivia.Should().NotBeEmpty();
        appliedTrivia.Should().Contain("// Short explanation.");
    }

    [Fact]
    public void Rewrite_WithNullOrWhitespaceReplacement_DoesNotThrowNreAndLeavesTriviaUnchanged()
    {
        var comment = "// Long comment text";
        var code = $"public class C {{ {comment} public void M() {{}} }}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var replacements = new Dictionary<string, string?>
        {
            [Hash(comment)] = null,
            [comment] = "   "
        }!;

        var act = () => _rewriter.Rewrite(tree, replacements!);
        act.Should().NotThrow();
        var result = _rewriter.Rewrite(tree, replacements!);
        result.ToString().Should().Be(code);
    }
}

