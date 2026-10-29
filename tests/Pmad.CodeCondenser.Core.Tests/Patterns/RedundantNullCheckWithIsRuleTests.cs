using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Patterns;
using Pmad.CodeCondenser.Core.Patterns.Rules;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Patterns;

public class RedundantNullCheckWithIsRuleTests
{
    private readonly RedundantNullCheckWithIsRule _rule = new();
    private readonly CodeCondenserSettings _settings = new();

    [Fact]
    public void TryMatch_NotNullAndIsType_SuggestsIsType()
    {
        var code = "var check = item != null && item is Widget;";
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
        Assert.Equal("item is Widget", match.SuggestedCode);

        var rewritten = _rule.Rewrite(binary, match);
        Assert.Equal("item is Widget", rewritten.ToString());
    }

    [Fact]
    public void TryMatch_NullNotEqualAndDeclarationPattern_SuggestsDeclarationPattern()
    {
        var code = "var check = null != item && item is Widget w;";
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
        Assert.Equal("item is Widget w", match.SuggestedCode);

        var rewritten = _rule.Rewrite(binary, match);
        Assert.Equal("item is Widget w", rewritten.ToString());
    }

    [Fact]
    public void TryMatch_IsPatternThenNotNull_SuggestsIsPattern()
    {
        var code = "var check = item is Widget && item != null;";
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
        Assert.Equal("item is Widget", match.SuggestedCode);
    }

    [Fact]
    public void TryMatch_DifferentOperands_DoesNotMatch()
    {
        var code = "var check = item != null && other is Widget;";
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

        Assert.False(matched);
        Assert.Null(match);
    }

    [Fact]
    public void TryMatch_ParenthesizedOperand_Matches()
    {
        var code = "var check = (item) != null && item is Widget;";
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
        Assert.Equal("item is Widget", match.SuggestedCode);
    }
}
