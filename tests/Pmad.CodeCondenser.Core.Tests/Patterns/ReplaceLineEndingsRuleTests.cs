using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Patterns;
using Pmad.CodeCondenser.Core.Patterns.Rules;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Patterns;

public class ReplaceLineEndingsRuleTests
{
    private readonly ReplaceLineEndingsRule _rule = new();
    private readonly CodeCondenserSettings _settings = new();

    [Fact]
    public void TryMatch_ChainedReplaceWindowsAndMac_MatchesAndSuggestsReplaceLineEndings()
    {
        var code = """
            var result = text.Replace("\r\n", "\n").Replace("\r", "\n");
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var invocation = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>().First();
        var matched = _rule.TryMatch(invocation, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("text.ReplaceLineEndings(\"\\n\")", match.SuggestedCode);
    }

    [Fact]
    public void TryMatch_EnvironmentNewLine_SuggestsParameterlessReplaceLineEndings()
    {
        var code = """
            var result = str.Replace("\r\n", Environment.NewLine).Replace("\r", Environment.NewLine);
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var invocation = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>().First();
        var matched = _rule.TryMatch(invocation, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("str.ReplaceLineEndings()", match.SuggestedCode);
    }

    [Fact]
    public void TryMatch_TargetVersionBelowNet6_DoesNotMatch()
    {
        var code = """
            var result = text.Replace("\r\n", "\n").Replace("\r", "\n");
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(5, 0)
        };

        var invocation = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>().First();
        var matched = _rule.TryMatch(invocation, context, out var match);

        Assert.False(matched);
        Assert.Null(match);
    }

    [Fact]
    public void Rewrite_ProducesCleanReplaceLineEndingsCall()
    {
        var code = """
            var result = text.Replace("\r\n", "\n").Replace("\r", "\n");
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var invocation = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>().First();
        _rule.TryMatch(invocation, context, out var match);
        var rewritten = _rule.Rewrite(invocation, match!);

        Assert.Equal("text.ReplaceLineEndings(\"\\n\")", rewritten.ToString());
    }

    [Fact]
    public void TryMatch_NonNewlineReplacement_DoesNotMatch()
    {
        var code = """
            var result = text.Replace("\r\n", "").Replace("\r", "");
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var invocation = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>().First();
        var matched = _rule.TryMatch(invocation, context, out var match);

        Assert.False(matched);
        Assert.Null(match);
    }

    [Fact]
    public void TryMatch_WindowsCrlfReplacement_Matches()
    {
        var code = """
            var result = text.Replace("\r\n", "\r\n").Replace("\r", "\r\n");
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var invocation = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>().First();
        var matched = _rule.TryMatch(invocation, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("text.ReplaceLineEndings(\"\\r\\n\")", match.SuggestedCode);
    }
}
