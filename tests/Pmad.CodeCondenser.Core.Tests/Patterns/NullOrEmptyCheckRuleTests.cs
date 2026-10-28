using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Patterns;
using Pmad.CodeCondenser.Core.Patterns.Rules;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Patterns;

public class NullOrEmptyCheckRuleTests
{
    private readonly NullOrEmptyCheckRule _rule = new();
    private readonly CodeCondenserSettings _settings = new();

    [Fact]
    public void TryMatch_NullOrLengthZero_SuggestsIsNullOrEmpty()
    {
        var code = "var empty = str == null || str.Length == 0;";
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
        Assert.Equal("string.IsNullOrEmpty(str)", match.SuggestedCode);

        var rewritten = _rule.Rewrite(binary, match);
        Assert.Equal("string.IsNullOrEmpty(str)", rewritten.ToString());
    }

    [Fact]
    public void TryMatch_NullOrEmptyLiteral_SuggestsIsNullOrEmpty()
    {
        var code = "var empty = str == null || str == \"\";";
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
        Assert.Equal("string.IsNullOrEmpty(str)", match.SuggestedCode);
    }

    [Fact]
    public void TryMatch_NullOrStringEmpty_SuggestsIsNullOrEmpty()
    {
        var code = "var empty = str == null || str == string.Empty;";
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
        Assert.Equal("string.IsNullOrEmpty(str)", match.SuggestedCode);
    }

    [Fact]
    public void TryMatch_NotNullAndLengthGreaterThanZero_SuggestsNegatedIsNullOrEmpty()
    {
        var code = "var hasContent = str != null && str.Length > 0;";
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
        Assert.Equal("!string.IsNullOrEmpty(str)", match.SuggestedCode);

        var rewritten = _rule.Rewrite(binary, match);
        Assert.Equal("!string.IsNullOrEmpty(str)", rewritten.ToString());
    }

    [Fact]
    public void TryMatch_NotNullAndNotEmptyLiteral_SuggestsNegatedIsNullOrEmpty()
    {
        var code = "var hasContent = str != null && str != \"\";";
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
        Assert.Equal("!string.IsNullOrEmpty(str)", match.SuggestedCode);
    }

    [Fact]
    public void TryMatch_DifferentOperands_DoesNotMatch()
    {
        var code = "var empty = str == null || other.Length == 0;";
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
}
