using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Patterns;
using Pmad.CodeCondenser.Core.Patterns.Rules;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Patterns;

public class MinByMaxByRuleTests
{
    private readonly MinByMaxByRule _rule = new();
    private readonly CodeCondenserSettings _settings = new();

    [Fact]
    public void TryMatch_OrderByFirst_SuggestsMinBy()
    {
        var code = "var cheapest = products.OrderBy(p => p.Price).First();";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var matched = _rule.TryMatch(invocation, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("products.MinBy(p => p.Price)", match.SuggestedCode);

        var rewritten = _rule.Rewrite(invocation, match);
        Assert.Equal("products.MinBy(p => p.Price)", rewritten.ToString());
    }

    [Fact]
    public void TryMatch_OrderByFirstOrDefault_SuggestsMinBy()
    {
        var code = "var cheapest = products.OrderBy(p => p.Price).FirstOrDefault();";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var matched = _rule.TryMatch(invocation, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("products.MinBy(p => p.Price)", match.SuggestedCode);
    }

    [Fact]
    public void TryMatch_OrderByDescendingFirst_SuggestsMaxBy()
    {
        var code = "var highest = students.OrderByDescending(s => s.Score).First();";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var matched = _rule.TryMatch(invocation, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("students.MaxBy(s => s.Score)", match.SuggestedCode);

        var rewritten = _rule.Rewrite(invocation, match);
        Assert.Equal("students.MaxBy(s => s.Score)", rewritten.ToString());
    }

    [Fact]
    public void TryMatch_OrderByLast_DoesNotMatch()
    {
        var code = "var max = items.OrderBy(x => x.Val).Last();";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(8, 0)
        };

        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var matched = _rule.TryMatch(invocation, context, out var match);

        Assert.False(matched);
        Assert.Null(match);
    }

    [Fact]
    public void TryMatch_Net5_DoesNotMatchDueToTfm()
    {
        var code = "var cheapest = products.OrderBy(p => p.Price).First();";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(5, 0)
        };

        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var matched = _rule.TryMatch(invocation, context, out var match);

        Assert.False(matched);
        Assert.Null(match);
    }
}
