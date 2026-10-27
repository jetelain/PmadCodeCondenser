using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Types;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Types;

public class TypeLocCounterTests
{
    [Fact]
    public void CountLinesOfCode_SingleLineDelegate_ReturnsOne()
    {
        var code = "public delegate void MyHandler(int x, string y);";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();
        var node = root.Members.OfType<DelegateDeclarationSyntax>().First();

        var loc = TypeLocCounter.CountLinesOfCode(node);

        Assert.Equal(1, loc);
    }

    [Fact]
    public void CountLinesOfCode_DelegateWithXmlDocComment_ExcludesComments()
    {
        var code = """
            /// <summary>
            /// This is a summary.
            /// </summary>
            /// <remarks>Remarks here.</remarks>
            public delegate void MyHandler(int x);
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();
        var node = root.Members.OfType<DelegateDeclarationSyntax>().First();

        var loc = TypeLocCounter.CountLinesOfCode(node);

        Assert.Equal(1, loc);
    }

    [Fact]
    public void CountLinesOfCode_MultiLineDelegate_ReturnsActualCodeLines()
    {
        var code = """
            public delegate void MyHandler(
                int x,
                string y);
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();
        var node = root.Members.OfType<DelegateDeclarationSyntax>().First();

        var loc = TypeLocCounter.CountLinesOfCode(node);

        Assert.Equal(3, loc);
    }

    [Fact]
    public void CountLinesOfCode_EnumWithMembersAndComments_CountsOnlyCode()
    {
        var code = """
            public enum Status
            {
                // Active status
                Active,
                /* Inactive status */
                Inactive
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();
        var node = root.Members.OfType<EnumDeclarationSyntax>().First();

        var loc = TypeLocCounter.CountLinesOfCode(node);

        // Lines: "public enum Status", "{", "Active,", "Inactive", "}" = 5
        Assert.Equal(5, loc);
    }
}
