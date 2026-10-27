using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
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

    [Fact]
    public void Rewrite_WithUselessCommentReplacement_RemovesComment()
    {
        var originalComment = "// Trivial comment that adds no value whatsoever";
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
            [Hash(originalComment)] = "Comment is useless"
        };

        var appliedTrivia = new List<string>();
        var rewritten = _rewriter.Rewrite(tree, replacements, appliedTrivia);
        var resultText = rewritten.ToString();

        resultText.Should().NotContain(originalComment);
        resultText.Should().NotContain("Comment is useless");
        appliedTrivia.Should().Contain(originalComment);
    }

    [Fact]
    public void Rewrite_ConsecutiveSingleLineComments_WithSingleLineSummary_ReplacesGroupWithSingleComment()
    {
        var code = """
            public class TestClass
            {
                    // 4 — Aesthetic → anchors/style.md (write-if-absent; the body is the
                    //      only content — no front-matter keys are invented; a re-run over an
                    //      identical aesthetic keeps the file byte-identical).
                    public void Generate() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var extractor = new VerboseCommentExtractor();
        var comments = extractor.Extract(tree, "test.cs", new() { CommentLengthThreshold = 50 });
        comments.Should().HaveCount(1);
        var originalComment = comments[0];

        var replacements = new Dictionary<string, string>
        {
            [originalComment.ContentHash] = "4 — Aesthetic style anchor."
        };

        var appliedTrivia = new List<string>();
        var rewritten = _rewriter.Rewrite(tree, replacements, appliedTrivia);
        var resultText = rewritten.ToString();

        resultText.Should().Contain("// 4 — Aesthetic style anchor.");
        resultText.Should().NotContain("write-if-absent");
        resultText.Should().NotContain("only content");
        resultText.Should().NotContain("identical aesthetic");

        var lines = resultText.Replace("\r\n", "\n").Split('\n');
        lines.Should().Contain("        // 4 — Aesthetic style anchor.");
        lines.Should().Contain("        public void Generate() {}");

        var reParsed = CSharpSyntaxTree.ParseText(resultText);
        reParsed.GetDiagnostics().Should().BeEmpty();
    }

    [Fact]
    public void Rewrite_ConsecutiveSingleLineComments_WithMultiLineSummary_WrapsInBlockComment()
    {
        var code = """
            public class TestClass
            {
                    // Line 1 of long comment explaining complex logic and requirements.
                    // Line 2 of long comment continuing the elaborate technical reasoning.
                    public void Run() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var extractor = new VerboseCommentExtractor();
        var comments = extractor.Extract(tree, "test.cs", new() { CommentLengthThreshold = 50 });
        comments.Should().HaveCount(1);
        var originalComment = comments[0];

        var replacements = new Dictionary<string, string>
        {
            [originalComment.ContentHash] = "Summary line 1.\r\nSummary line 2."
        };

        var rewritten = _rewriter.Rewrite(tree, replacements);
        var resultText = rewritten.ToString();

        resultText.Should().Contain("/* Summary line 1.\r\nSummary line 2. */");
        resultText.Should().NotContain("Line 1 of long comment");
        resultText.Should().NotContain("Line 2 of long comment");

        var reParsed = CSharpSyntaxTree.ParseText(resultText);
        reParsed.GetDiagnostics().Should().BeEmpty();
    }

    [Fact]
    public void Rewrite_ConsecutiveSingleLineComments_WithUselessComment_RemovesEntireCommentGroup()
    {
        var code = """
            public class TestClass
            {
                    // Line 1 of obvious comment restating method name.
                    // Line 2 of obvious comment explaining nothing useful.
                    public void Run() {}
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var extractor = new VerboseCommentExtractor();
        var comments = extractor.Extract(tree, "test.cs", new() { CommentLengthThreshold = 50 });
        comments.Should().HaveCount(1);
        var originalComment = comments[0];

        var replacements = new Dictionary<string, string>
        {
            [originalComment.ContentHash] = "Comment is useless"
        };

        var appliedTrivia = new List<string>();
        var rewritten = _rewriter.Rewrite(tree, replacements, appliedTrivia);
        var resultText = rewritten.ToString();

        resultText.Should().NotContain("Line 1 of obvious");
        resultText.Should().NotContain("Line 2 of obvious");
        resultText.Should().NotContain("Comment is useless");
        appliedTrivia.Should().Contain(originalComment.OriginalText);

        var reParsed = CSharpSyntaxTree.ParseText(resultText);
        reParsed.GetDiagnostics().Should().BeEmpty();
    }

    [Fact]
    public void Rewrite_ConsecutiveSingleLineComments_WithLfReplacementHashOnCrlfSource_Succeeds()
    {
        // Source text explicitly formatted with CRLF
        var code = "public class TestClass\r\n{\r\n    // Line 1 of comment to be summarized\r\n    // Line 2 of comment to be summarized\r\n    public void Run() {}\r\n}";

        var tree = CSharpSyntaxTree.ParseText(code);
        // Compute hash using LF normalized text as might happen across OS / git
        var lfText = "// Line 1 of comment to be summarized\n    // Line 2 of comment to be summarized";
        var lfHash = Hash(lfText);

        var replacements = new Dictionary<string, string>
        {
            [lfHash] = "Summarized comment."
        };

        var rewritten = _rewriter.Rewrite(tree, replacements);
        var resultText = rewritten.ToString();

        resultText.Should().Contain("// Summarized comment.");
        resultText.Should().NotContain("Line 1 of comment to be summarized");

        var reParsed = CSharpSyntaxTree.ParseText(resultText);
        reParsed.GetDiagnostics().Should().BeEmpty();
    }
}


