using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Patterns;
using Pmad.CodeCondenser.Core.Patterns.Rules;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Patterns;

public class ThrowIfNullRuleTests
{
    private readonly ThrowIfNullRule _rule = new();
    private readonly CodeCondenserSettings _settings = new();

    [Fact]
    public void TryMatch_DirectThrowWithSameNameof_Matches()
    {
        var code = """
            if (argument == null)
                throw new ArgumentNullException(nameof(argument));
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

        var ifStmt = root.DescendantNodes().OfType<IfStatementSyntax>().First();
        var matched = _rule.TryMatch(ifStmt, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("ArgumentNullException.ThrowIfNull(argument);", match.SuggestedCode);

        var rewritten = _rule.Rewrite(ifStmt, match);
        Assert.Equal("ArgumentNullException.ThrowIfNull(argument);", rewritten.ToString());
    }

    [Fact]
    public void TryMatch_BlockThrowWithSameStringLiteral_Matches()
    {
        var code = """
            if (argument == null)
            {
                throw new ArgumentNullException("argument");
            }
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

        var ifStmt = root.DescendantNodes().OfType<IfStatementSyntax>().First();
        var matched = _rule.TryMatch(ifStmt, context, out var match);

        Assert.True(matched);
        Assert.NotNull(match);
        Assert.Equal("ArgumentNullException.ThrowIfNull(argument);", match.SuggestedCode);
    }

    [Fact]
    public void TryMatch_DifferentParamName_DoesNotMatch()
    {
        var code = """
            if (argument == null)
                throw new ArgumentNullException(nameof(other));
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

        var ifStmt = root.DescendantNodes().OfType<IfStatementSyntax>().First();
        var matched = _rule.TryMatch(ifStmt, context, out var match);

        Assert.False(matched);
        Assert.Null(match);
    }

    [Fact]
    public void TryMatch_CustomErrorMessage_DoesNotMatch()
    {
        var code = """
            if (argument == null)
                throw new ArgumentNullException(nameof(argument), "Must not be null");
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

        var ifStmt = root.DescendantNodes().OfType<IfStatementSyntax>().First();
        var matched = _rule.TryMatch(ifStmt, context, out var match);

        Assert.False(matched);
        Assert.Null(match);
    }
}
