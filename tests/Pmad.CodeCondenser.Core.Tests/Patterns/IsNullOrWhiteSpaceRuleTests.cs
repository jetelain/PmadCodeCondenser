using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Patterns;
using Pmad.CodeCondenser.Core.Patterns.Rules;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Patterns;

public class IsNullOrWhiteSpaceRuleTests
{
    private readonly IsNullOrWhiteSpaceRule _rule = new();
    private readonly CodeCondenserSettings _settings = new();

    [Fact]
    public void TryMatch_StringIsNullOrEmptyWithTrim_MatchesAndSuggestsIsNullOrWhiteSpace()
    {
        var code = """
            if (string.IsNullOrEmpty(str.Trim()))
            {
                return;
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings
        };

        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var matched = _rule.TryMatch(invocation, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("string.IsNullOrWhiteSpace(str)", match.SuggestedCode);

        var rewritten = _rule.Rewrite(invocation, match);
        Assert.Equal("string.IsNullOrWhiteSpace(str)", rewritten.ToString());
    }

    [Fact]
    public void TryMatch_StringIsNullOrEmptyWithConditionalTrim_Matches()
    {
        var code = """
            if (string.IsNullOrEmpty(str?.Trim()))
            {
                return;
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings
        };

        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var matched = _rule.TryMatch(invocation, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("string.IsNullOrWhiteSpace(str)", match.SuggestedCode);
    }

    [Fact]
    public void TryMatch_StringIsNullOrEmptyWithoutTrim_DoesNotMatch()
    {
        var code = """
            if (string.IsNullOrEmpty(str))
            {
                return;
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings
        };

        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var matched = _rule.TryMatch(invocation, context, out var match);

        Assert.False(matched);
        Assert.Null(match);
    }
}
