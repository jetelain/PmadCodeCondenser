using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Patterns;
using Pmad.CodeCondenser.Core.Patterns.Rules;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Patterns;

public class StringContainsComparisonRuleTests
{
    private readonly StringContainsComparisonRule _rule = new();
    private readonly CodeCondenserSettings _settings = new();

    [Fact]
    public void TryMatch_IndexOfGreaterThanOrEqualToZero_SuggestsContains()
    {
        var code = """var found = text.IndexOf("hello") >= 0;""";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var binary = root.DescendantNodes().OfType<BinaryExpressionSyntax>().First();
        var matched = _rule.TryMatch(binary, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("text.Contains(\"hello\")", match.SuggestedCode);

        var rewritten = _rule.Rewrite(binary, match);
        Assert.Equal("text.Contains(\"hello\")", rewritten.ToString());
    }

    [Fact]
    public void TryMatch_IndexOfWithComparison_SuggestsContainsWithComparison()
    {
        var code = """var found = text.IndexOf("hello", StringComparison.OrdinalIgnoreCase) != -1;""";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var binary = root.DescendantNodes().OfType<BinaryExpressionSyntax>().First();
        var matched = _rule.TryMatch(binary, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("text.Contains(\"hello\", StringComparison.OrdinalIgnoreCase)", match.SuggestedCode);
    }

    [Fact]
    public void TryMatch_IndexOfEqualsMinusOne_SuggestsNegatedContains()
    {
        var code = """var missing = text.IndexOf("hello") == -1;""";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var binary = root.DescendantNodes().OfType<BinaryExpressionSyntax>().First();
        var matched = _rule.TryMatch(binary, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("!text.Contains(\"hello\")", match.SuggestedCode);

        var rewritten = _rule.Rewrite(binary, match);
        Assert.Equal("!text.Contains(\"hello\")", rewritten.ToString());
    }

    [Fact]
    public void TryMatch_IndexOfLessThanZero_SuggestsNegatedContains()
    {
        var code = """var missing = text.IndexOf("hello") < 0;""";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var binary = root.DescendantNodes().OfType<BinaryExpressionSyntax>().First();
        var matched = _rule.TryMatch(binary, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("!text.Contains(\"hello\")", match.SuggestedCode);
    }

    [Fact]
    public void TryMatch_FlippedComparison_Matches()
    {
        var code = """var found = 0 <= text.IndexOf("hello");""";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var binary = root.DescendantNodes().OfType<BinaryExpressionSyntax>().First();
        var matched = _rule.TryMatch(binary, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("text.Contains(\"hello\")", match.SuggestedCode);
    }

    [Fact]
    public void TryMatch_Net48Target_WithComparison_DoesNotMatch()
    {
        var code = """var found = text.IndexOf("hello", StringComparison.OrdinalIgnoreCase) >= 0;""";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(4, 8)
        };

        var binary = root.DescendantNodes().OfType<BinaryExpressionSyntax>().First();
        var matched = _rule.TryMatch(binary, context, out var match);

        Assert.False(matched);
        Assert.Null(match);
    }

    [Fact]
    public void TryMatch_CharLiteral_Net8_Matches()
    {
        var code = """var found = text.IndexOf('a') >= 0;""";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var binary = root.DescendantNodes().OfType<BinaryExpressionSyntax>().First();
        var matched = _rule.TryMatch(binary, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("text.Contains('a')", match.SuggestedCode);
    }

    [Fact]
    public void TryMatch_CharLiteral_Net48_DoesNotMatch()
    {
        var code = """var found = text.IndexOf('a') >= 0;""";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(4, 8)
        };

        var binary = root.DescendantNodes().OfType<BinaryExpressionSyntax>().First();
        var matched = _rule.TryMatch(binary, context, out var match);

        Assert.False(matched);
        Assert.Null(match);
    }
}
