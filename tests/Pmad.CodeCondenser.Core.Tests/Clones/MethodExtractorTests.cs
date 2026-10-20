using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Clones;

namespace Pmad.CodeCondenser.Core.Tests.Clones;

public class MethodExtractorTests
{
    private static MethodDeclarationSyntax ParseMethod(string code)
    {
        var source = code.Contains("class") ? code : $"class Wrapper {{ {code} }}";
        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
    }

    private static SyntaxTree ParseTree(string code) => CSharpSyntaxTree.ParseText(code);

    [Fact]
    public void GetStatementCount_EmptyBody_ReturnsZero()
    {
        var method = ParseMethod("void Empty() {}");
        MethodExtractor.GetStatementCount(method).Should().Be(0);
    }

    [Fact]
    public void GetStatementCount_NoBody_ReturnsZero()
    {
        var method = ParseMethod("abstract void AbstractMethod();");
        MethodExtractor.GetStatementCount(method).Should().Be(0);
    }

    [Fact]
    public void GetStatementCount_ExpressionBody_ReturnsOne()
    {
        var method = ParseMethod("int Add(int a, int b) => a + b;");
        MethodExtractor.GetStatementCount(method).Should().Be(1);
    }

    [Fact]
    public void GetStatementCount_SequentialStatements_ReturnsCorrectCount()
    {
        var method = ParseMethod(@"
void Execute()
{
    int a = 1;
    int b = 2;
    int c = 3;
    int d = 4;
    int e = 5;
    int f = 6;
    int g = 7;
    int h = 8;
}");
        MethodExtractor.GetStatementCount(method).Should().Be(8);
    }

    [Fact]
    public void GetStatementCount_NestedBlocks_DoesNotCountBlockSyntaxItself()
    {
        var method = ParseMethod(@"
void ControlFlow()
{
    if (true)
    {
        int x = 1;
    }
    else
    {
        int y = 2;
    }
}");
        // if-statement + int x = 1 + int y = 2 = 3 statements (blocks are excluded)
        MethodExtractor.GetStatementCount(method).Should().Be(3);
    }

    [Fact]
    public void GetStatementCount_EmptyStatements_AreExcluded()
    {
        var method = ParseMethod(@"
void StraySemicolons()
{
    int a = 1;
    ;
    ;
    int b = 2;
}");
        MethodExtractor.GetStatementCount(method).Should().Be(2);
    }

    [Fact]
    public void GetStatementCount_LoopsAndTryCatch_CountsInnerStatements()
    {
        var method = ParseMethod(@"
void LoopAndException()
{
    try
    {
        for (int i = 0; i < 10; i++)
        {
            Console.WriteLine(i);
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex);
    }
}");
        // try, for, Console.WriteLine, Console.Error.WriteLine = 4 statements
        MethodExtractor.GetStatementCount(method).Should().Be(4);
    }

    [Fact]
    public void Extract_DefaultThreshold_FiltersOutMethodsBelowThreshold()
    {
        var tree = ParseTree(@"
class Sample
{
    void ShortMethod()
    {
        int a = 1;
        int b = 2;
        int c = 3;
    }

    void LongMethod()
    {
        int s1 = 1;
        int s2 = 2;
        int s3 = 3;
        int s4 = 4;
        int s5 = 5;
        int s6 = 6;
        int s7 = 7;
        int s8 = 8;
    }
}");
        var extractor = new MethodExtractor();
        var extracted = extractor.Extract(tree);

        extracted.Should().HaveCount(1);
        extracted[0].Identifier.ValueText.Should().Be("LongMethod");
    }

    [Fact]
    public void Extract_CustomThreshold_RespectsParameter()
    {
        var tree = ParseTree(@"
class Sample
{
    void OneStatement() => Console.WriteLine();

    void TwoStatements()
    {
        int a = 1;
        int b = 2;
    }

    void FourStatements()
    {
        int a = 1;
        int b = 2;
        int c = 3;
        int d = 4;
    }
}");
        var extractor = new MethodExtractor(minStatements: 3);
        var extracted = extractor.Extract(tree, minStatements: 3);

        extracted.Should().HaveCount(1);
        extracted[0].Identifier.ValueText.Should().Be("FourStatements");
    }

    [Fact]
    public void Extract_ExpressionBodyWithThresholdOne_IsExtracted()
    {
        var tree = ParseTree(@"
class Sample
{
    int Double(int x) => x * 2;
}");
        var extractor = new MethodExtractor(minStatements: 1);
        var extracted = extractor.Extract(tree, minStatements: 1);

        extracted.Should().HaveCount(1);
        extracted[0].Identifier.ValueText.Should().Be("Double");
    }

    [Fact]
    public void Extract_NestedClasses_ExtractsMethodsFromAllLevels()
    {
        var tree = ParseTree(@"
class Outer
{
    void OuterMethod()
    {
        int a = 1; int b = 2; int c = 3; int d = 4;
        int e = 5; int f = 6; int g = 7; int h = 8;
    }

    class Inner
    {
        void InnerMethod()
        {
            int a = 1; int b = 2; int c = 3; int d = 4;
            int e = 5; int f = 6; int g = 7; int h = 8;
        }
    }
}");
        var extractor = new MethodExtractor(minStatements: 8);
        var extracted = extractor.Extract(tree);

        extracted.Should().HaveCount(2);
        extracted.Select(m => m.Identifier.ValueText).Should().Contain(["OuterMethod", "InnerMethod"]);
    }

    [Fact]
    public void NullArguments_ThrowArgumentNullException()
    {
        Action act1 = () => MethodExtractor.GetStatementCount(null!);
        act1.Should().Throw<ArgumentNullException>();

        var extractor = new MethodExtractor();
        Action act2 = () => extractor.Extract((SyntaxTree)null!);
        act2.Should().Throw<ArgumentNullException>();

        Action act3 = () => extractor.Extract((SyntaxNode)null!);
        act3.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Extract_MethodWithIgnoreDuplicateComment_IsSkipped()
    {
        var tree = ParseTree(@"
class C
{
    // codecondenser:ignore-duplicate
    void Execute()
    {
        int a = 1; int b = 2; int c = 3; int d = 4;
        int e = 5; int f = 6; int g = 7; int h = 8;
    }
}");
        var extractor = new MethodExtractor(minStatements: 8);
        var extracted = extractor.Extract(tree);

        extracted.Should().BeEmpty();
    }

    [Fact]
    public void Extract_MethodWithSuppressMessageAttribute_IsSkipped()
    {
        var tree = ParseTree(@"
class C
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(""CodeCondenser"", ""Duplication"")]
    void Execute()
    {
        int a = 1; int b = 2; int c = 3; int d = 4;
        int e = 5; int f = 6; int g = 7; int h = 8;
    }
}");
        var extractor = new MethodExtractor(minStatements: 8);
        var extracted = extractor.Extract(tree);

        extracted.Should().BeEmpty();
    }

    [Fact]
    public void Extract_MethodWithCodeCondenserIgnoreAttribute_IsSkipped()
    {
        var tree = ParseTree(@"
class C
{
    [CodeCondenserIgnore]
    void Execute()
    {
        int a = 1; int b = 2; int c = 3; int d = 4;
        int e = 5; int f = 6; int g = 7; int h = 8;
    }
}");
        var extractor = new MethodExtractor(minStatements: 8);
        var extracted = extractor.Extract(tree);

        extracted.Should().BeEmpty();
    }

    [Fact]
    public void Extract_MethodWithBodyIgnoreComment_IsSkipped()
    {
        var tree = ParseTree(@"
class C
{
    void Execute()
    {
        // codecondenser:ignore-duplicate
        int a = 1; int b = 2; int c = 3; int d = 4;
        int e = 5; int f = 6; int g = 7; int h = 8;
    }
}");
        var extractor = new MethodExtractor(minStatements: 8);
        var extracted = extractor.Extract(tree);

        extracted.Should().BeEmpty();
    }
}
