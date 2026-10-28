using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Patterns;
using Pmad.CodeCondenser.Core.Patterns.Rules;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Patterns;

public class TernaryNullCoalescingRuleTests
{
    private readonly TernaryNullCoalescingRule _rule = new();
    private readonly CodeCondenserSettings _settings = new();

    [Fact]
    public void TryMatch_NotNullTernary_SuggestsNullCoalesce()
    {
        var code = "var name = user != null ? user : \"default\";";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var conditional = root.DescendantNodes().OfType<ConditionalExpressionSyntax>().First();
        var matched = _rule.TryMatch(conditional, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("user ?? \"default\"", match.SuggestedCode);

        var rewritten = _rule.Rewrite(conditional, match);
        Assert.Equal("user ?? \"default\"", rewritten.ToString());
    }

    [Fact]
    public void TryMatch_NullTernary_SuggestsNullCoalesce()
    {
        var code = "var name = user == null ? \"default\" : user;";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var conditional = root.DescendantNodes().OfType<ConditionalExpressionSyntax>().First();
        var matched = _rule.TryMatch(conditional, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("user ?? \"default\"", match.SuggestedCode);

        var rewritten = _rule.Rewrite(conditional, match);
        Assert.Equal("user ?? \"default\"", rewritten.ToString());
    }

    [Fact]
    public void TryMatch_ParenthesizedCondition_SuggestsNullCoalesce()
    {
        var code = "var name = (user != null) ? user : \"default\";";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var conditional = root.DescendantNodes().OfType<ConditionalExpressionSyntax>().First();
        var matched = _rule.TryMatch(conditional, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("user ?? \"default\"", match.SuggestedCode);
    }

    [Fact]
    public void TryMatch_DifferentOperands_DoesNotMatch()
    {
        var code = "var name = user != null ? other : \"default\";";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var conditional = root.DescendantNodes().OfType<ConditionalExpressionSyntax>().First();
        var matched = _rule.TryMatch(conditional, context, out var match);

        Assert.False(matched);
        Assert.Null(match);
    }

    [Fact]
    public void TryMatch_NestedTernaryFallback_PreservesParentheses()
    {
        var code = "var name = user != null ? user : (other != null ? other : \"default\");";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var conditional = root.DescendantNodes().OfType<ConditionalExpressionSyntax>().First();
        var matched = _rule.TryMatch(conditional, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("user ?? (other != null ? other : \"default\")", match.SuggestedCode);

        var rewritten = _rule.Rewrite(conditional, match);
        Assert.Equal("user ?? (other != null ? other : \"default\")", rewritten.ToString());
    }

    [Fact]
    public void TryMatch_BinaryFallback_PreservesParentheses()
    {
        var code = "var name = flag != null ? flag : (a == b);";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var conditional = root.DescendantNodes().OfType<ConditionalExpressionSyntax>().First();
        var matched = _rule.TryMatch(conditional, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("flag ?? (a == b)", match.SuggestedCode);

        var rewritten = _rule.Rewrite(conditional, match);
        Assert.Equal("flag ?? (a == b)", rewritten.ToString());
    }
}
