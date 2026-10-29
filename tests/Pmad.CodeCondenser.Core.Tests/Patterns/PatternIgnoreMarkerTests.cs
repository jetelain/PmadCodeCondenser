using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Patterns;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Patterns;

public class PatternIgnoreMarkerTests
{
    [Fact]
    public void IsIgnored_RuleSpecificIgnore_OnlySuppressesTargetRule()
    {
        var code = """
            public class Test
            {
                public void Run()
                {
                    // codecondenser:ignore-pattern:PCC401
                    var x = text.Replace("\r\n", "\n").Replace("\r", "\n");
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var node = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();

        Assert.True(PatternIgnoreMarker.IsIgnored(node, "PCC401"));
        Assert.False(PatternIgnoreMarker.IsIgnored(node, "PCC402"));
        Assert.False(PatternIgnoreMarker.IsIgnored(node, "PCC403"));
    }

    [Fact]
    public void IsIgnored_GeneralIgnore_SuppressesAllRules()
    {
        var code = """
            public class Test
            {
                public void Run()
                {
                    // codecondenser:ignore
                    var x = text.Replace("\r\n", "\n").Replace("\r", "\n");
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var node = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();

        Assert.True(PatternIgnoreMarker.IsIgnored(node, "PCC401"));
        Assert.True(PatternIgnoreMarker.IsIgnored(node, "PCC402"));
        Assert.True(PatternIgnoreMarker.IsIgnored(node, null));
    }

    [Fact]
    public void IsIgnored_UnrelatedComment_DoesNotSuppress()
    {
        var code = """
            public class Test
            {
                public void Run()
                {
                    // codecondenser:ignore-something-else
                    var x = text.Replace("\r\n", "\n").Replace("\r", "\n");
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var node = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();

        Assert.False(PatternIgnoreMarker.IsIgnored(node, "PCC401"));
    }

    [Fact]
    public void IsIgnored_MultipleRulesSpecified_SuppressesListedRulesOnly()
    {
        var code = """
            public class Test
            {
                public void Run()
                {
                    // codecondenser:ignore-pattern:PCC401, PCC402
                    var x = text.Replace("\r\n", "\n").Replace("\r", "\n");
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var node = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();

        Assert.True(PatternIgnoreMarker.IsIgnored(node, "PCC401"));
        Assert.True(PatternIgnoreMarker.IsIgnored(node, "PCC402"));
        Assert.False(PatternIgnoreMarker.IsIgnored(node, "PCC403"));
    }

    [Fact]
    public void IsIgnored_SuppressMessageAttribute_SuppressesTargetRule()
    {
        var code = """
            public class Test
            {
                [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeCondenser", "PCC401")]
                public void Run()
                {
                    var x = text.Replace("\r\n", "\n").Replace("\r", "\n");
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var node = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();

        Assert.True(PatternIgnoreMarker.IsIgnored(node, "PCC401"));
        Assert.False(PatternIgnoreMarker.IsIgnored(node, "PCC402"));
    }
}
