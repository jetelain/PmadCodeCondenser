using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Patterns;
using Pmad.CodeCondenser.Core.Patterns.Rules;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Patterns;

public class ThrowIfNullOrWhiteSpaceRuleTests
{
    private readonly ThrowIfNullOrWhiteSpaceRule _rule = new();
    private readonly CodeCondenserSettings _settings = new();

    [Fact]
    public void TryMatch_IsNullOrWhiteSpace_SuggestsThrowIfNullOrWhiteSpace()
    {
        var code = """
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty", nameof(name));
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
        Assert.Equal("ArgumentException.ThrowIfNullOrWhiteSpace(name);", match.SuggestedCode);

        var rewritten = _rule.Rewrite(ifStmt, match);
        Assert.Equal("ArgumentException.ThrowIfNullOrWhiteSpace(name);", rewritten.ToString());
    }

    [Fact]
    public void TryMatch_IsNullOrEmpty_SuggestsThrowIfNullOrEmpty()
    {
        var code = """
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path required", nameof(path));
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
        Assert.Equal("ArgumentException.ThrowIfNullOrEmpty(path);", match.SuggestedCode);
    }

    [Fact]
    public void TryMatch_Net7_DoesNotMatchDueToTfm()
    {
        var code = """
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty", nameof(name));
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var context = new CondensationContext
        {
            SyntaxTree = tree,
            FilePath = "Test.cs",
            Settings = _settings,
            TargetDotNetVersion = new Version(7, 0)
        };

        var ifStmt = root.DescendantNodes().OfType<IfStatementSyntax>().First();
        var matched = _rule.TryMatch(ifStmt, context, out var match);

        Assert.False(matched);
        Assert.Null(match);
    }

    [Fact]
    public void TryMatch_DifferentParamName_DoesNotMatch()
    {
        var code = """
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty", nameof(other));
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
    public void TryMatch_InnerExceptionPresent_DoesNotMatch()
    {
        var code = """
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty", nameof(name), innerEx);
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
    public void TryMatch_SingleArgumentNonParamMessage_DoesNotMatch()
    {
        var code = """
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Arbitrary domain validation failed");
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
    public void TryMatch_ElseIfBranch_DoesNotMatch()
    {
        var code = """
            if (other)
            {
                DoSomething();
            }
            else if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name required", nameof(name));
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

        var elseIfStmt = root.DescendantNodes().OfType<IfStatementSyntax>().ElementAt(1);
        var matched = _rule.TryMatch(elseIfStmt, context, out var match);

        Assert.False(matched);
        Assert.Null(match);
    }
}
