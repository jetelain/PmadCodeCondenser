using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Patterns;
using Pmad.CodeCondenser.Core.Patterns.Rules;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Patterns;

public class LinqWhereSimplificationRuleTests
{
    private readonly LinqWhereSimplificationRule _rule = new();
    private readonly CodeCondenserSettings _settings = new();

    [Fact]
    public void TryMatch_WhereAny_MatchesAndRewritesToAny()
    {
        var code = """
            var hasActive = users.Where(u => u.IsActive).Any();
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings
        };

        var outerInvocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var matched = _rule.TryMatch(outerInvocation, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("users.Any(u => u.IsActive)", match.SuggestedCode);

        var rewritten = _rule.Rewrite(outerInvocation, match);
        Assert.Equal("users.Any(u => u.IsActive)", rewritten.ToString());
    }

    [Fact]
    public void TryMatch_WhereCount_MatchesAndRewritesToCount()
    {
        var code = """
            var count = items.Where(x => x.Valid).Count();
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings
        };

        var outerInvocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var matched = _rule.TryMatch(outerInvocation, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("items.Count(x => x.Valid)", match.SuggestedCode);
    }

    [Fact]
    public void TryMatch_WhereFirstOrDefault_Matches()
    {
        var code = """
            var item = items.Where(x => x.Id == 1).FirstOrDefault();
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings
        };

        var outerInvocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var matched = _rule.TryMatch(outerInvocation, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("items.FirstOrDefault(x => x.Id == 1)", match.SuggestedCode);
    }
}
