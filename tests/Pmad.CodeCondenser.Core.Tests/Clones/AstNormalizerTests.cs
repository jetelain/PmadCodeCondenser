using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Clones;

namespace Pmad.CodeCondenser.Core.Tests.Clones;

public class AstNormalizerTests
{
    private readonly AstNormalizer _normalizer = new();

    private static MethodDeclarationSyntax ParseMethod(string code)
    {
        var source = code.Contains("class") ? code : $"class Wrapper {{ {code} }}";
        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
    }

    [Fact]
    public void Normalize_LocalVariablesAndParameters_AreReplacedWithVar()
    {
        var method = ParseMethod(@"
public int Calculate(int x, int y)
{
    int result = x + y;
    return result;
}");

        var normalized = _normalizer.Normalize(method);
        var normalizedCode = normalized.NormalizeWhitespace().ToFullString();

        normalizedCode.Should().Contain("METHOD(int VAR, int VAR)");
        normalizedCode.Should().Contain("int VAR = VAR + VAR;");
        normalizedCode.Should().Contain("return VAR;");
    }

    [Fact]
    public void Normalize_AllIdentifiers_AreNormalizedToVar()
    {
        var method = ParseMethod(@"
public void Process(Order order)
{
    order.Validate();
    Logger.Info(order);
    Console.WriteLine(order.Id);
}");

        var normalized = _normalizer.Normalize(method);
        var normalizedCode = normalized.NormalizeWhitespace().ToFullString();

        normalizedCode.Should().Contain("METHOD(VAR VAR)");
        normalizedCode.Should().Contain("VAR.VAR();");
        normalizedCode.Should().Contain("VAR.VAR(VAR);");
        normalizedCode.Should().Contain("VAR.VAR(VAR.VAR);");
    }

    [Fact]
    public void Normalize_Literals_AreReplacedWithConst()
    {
        var method = ParseMethod(@"
public void Constants()
{
    int num = 42;
    double dbl = 3.14;
    string str = ""hello world"";
    char ch = 'c';
    bool flag = true;
    bool flag2 = false;
    object obj = null;
}");

        var normalized = _normalizer.Normalize(method);
        var normalizedCode = normalized.NormalizeWhitespace().ToFullString();

        normalizedCode.Should().Contain("int VAR = CONST;");
        normalizedCode.Should().Contain("double VAR = CONST;");
        normalizedCode.Should().Contain("string VAR = CONST;");
        normalizedCode.Should().Contain("char VAR = CONST;");
        normalizedCode.Should().Contain("bool VAR = CONST;");
        // null is preserved
        normalizedCode.Should().Contain("object VAR = null;");
    }

    [Fact]
    public void Normalize_RawStringLiterals_AreReplacedWithConst()
    {
        var method = ParseMethod("public void M() { string raw = @\"multi\r\nline\r\nraw\"; }");

        var normalized = _normalizer.Normalize(method);
        var normalizedCode = normalized.NormalizeWhitespace().ToFullString();

        normalizedCode.Should().Contain("string VAR = CONST;");
    }

    [Fact]
    public void Normalize_Comments_AreStripped()
    {
        var method = ParseMethod(@"
/// <summary>
/// Documentation comment.
/// </summary>
public void CommentedMethod()
{
    // Single line comment
    int a = 1;
    /* Multi line
       comment */
    int b = 2;
}");

        var normalized = _normalizer.Normalize(method);
        var normalizedCode = normalized.NormalizeWhitespace().ToFullString();

        normalizedCode.Should().NotContain("Documentation comment");
        normalizedCode.Should().NotContain("Single line comment");
        normalizedCode.Should().NotContain("Multi line");
    }

    [Fact]
    public void Normalize_AccessibilityModifiers_AreStripped()
    {
        var publicMethod = ParseMethod("public void Run() { int a = 1; }");
        var privateMethod = ParseMethod("private void Execute() { int b = 2; }");

        var normPublic = _normalizer.Normalize(publicMethod).NormalizeWhitespace().ToFullString();
        var normPrivate = _normalizer.Normalize(privateMethod).NormalizeWhitespace().ToFullString();

        normPublic.Should().Be(normPrivate);
    }

    [Fact]
    public void Normalize_ForEachAndCatchVariables_AreNormalizedToVar()
    {
        var method = ParseMethod(@"
public void LoopAndCatch(List<int> items)
{
    foreach (var element in items)
    {
        try
        {
            Console.WriteLine(element);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine(ex);
        }
    }
}");

        var normalized = _normalizer.Normalize(method);
        var normalizedCode = normalized.NormalizeWhitespace().ToFullString();

        normalizedCode.Should().Contain("foreach (VAR VAR in VAR)");
        normalizedCode.Should().Contain("catch (VAR VAR)");
        normalizedCode.Should().Contain("VAR.VAR(VAR);");
    }

    [Fact]
    public void Normalize_PatternMatchingAndOutVariables_AreNormalizedToVar()
    {
        var method = ParseMethod(@"
public void Patterns(object obj, string text)
{
    if (int.TryParse(text, out int parsed))
    {
        Console.WriteLine(parsed);
    }

    if (obj is string str)
    {
        Console.WriteLine(str);
    }
}");

        var normalized = _normalizer.Normalize(method);
        var normalizedCode = normalized.NormalizeWhitespace().ToFullString();

        normalizedCode.Should().Contain("int.VAR(VAR, out int VAR)");
        normalizedCode.Should().Contain("VAR is string VAR");
    }

    [Fact]
    public void Normalize_TwoCloneMethods_ProduceIdenticalNormalizedAst()
    {
        var method1 = ParseMethod(@"
public int ProcessOrder(int orderId, int factor)
{
    int initialScore = 100;
    int calculated = (orderId * factor) + initialScore;
    Console.WriteLine(calculated);
    return calculated;
}");

        var method2 = ParseMethod(@"
private int HandleInvoice(int invoiceNumber, int multiplier)
{
    int baseValue = 500;
    int total = (invoiceNumber * multiplier) + baseValue;
    Console.WriteLine(total);
    return total;
}");

        var norm1 = _normalizer.Normalize(method1).NormalizeWhitespace("", "").ToFullString();
        var norm2 = _normalizer.Normalize(method2).NormalizeWhitespace("", "").ToFullString();

        norm1.Should().Be(norm2);
    }

    [Fact]
    public void Normalize_TwoDifferentMethods_ProduceDifferentNormalizedAst()
    {
        var method1 = ParseMethod(@"
public int Add(int a, int b)
{
    return a + b;
}");

        var method2 = ParseMethod(@"
public int Multiply(int a, int b)
{
    return a * b;
}");

        var norm1 = _normalizer.Normalize(method1).NormalizeWhitespace("", "").ToFullString();
        var norm2 = _normalizer.Normalize(method2).NormalizeWhitespace("", "").ToFullString();

        norm1.Should().NotBe(norm2);
    }

    [Fact]
    public void Normalize_NullArgument_ThrowsArgumentNullException()
    {
        Action act = () => _normalizer.Normalize(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
