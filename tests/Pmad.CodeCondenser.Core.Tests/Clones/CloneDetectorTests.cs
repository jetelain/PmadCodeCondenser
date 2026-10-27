using AwesomeAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Clones;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Tests.Clones;

public class CloneDetectorTests
{
    private readonly CloneDetector _detector = new();

    [Fact]
    public void ExtractMethods_CalculatesAccurateCoordinatesAndMetadata()
    {
        var code = @"
class TestService
{
    public int Process(int a, int b)
    {
        int x = a + 1;
        int y = b + 2;
        int z = x + y;
        int w = z * 2;
        int v = w - 1;
        int u = v + 3;
        int t = u * 4;
        return t;
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { DuplicationStatementThreshold = 8 };

        var methods = _detector.ExtractMethods(tree, "src/TestService.cs", settings);

        methods.Should().HaveCount(1);
        var info = methods[0];
        info.FilePath.Should().Be("src/TestService.cs");
        info.MethodName.Should().Be("Process");
        info.StartLine.Should().Be(4);
        info.EndLine.Should().Be(14);
        info.StatementCount.Should().Be(8);
        info.Signature.Should().Be("public int Process(int a, int b)");
        info.NormalizedHash.Should().HaveLength(64);
    }

    [Fact]
    public void ExtractMethods_BelowThreshold_AreFilteredOut()
    {
        var code = @"
class ShortClass
{
    public void Quick()
    {
        int a = 1;
        int b = 2;
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { DuplicationStatementThreshold = 8 };

        var methods = _detector.ExtractMethods(tree, "src/ShortClass.cs", settings);

        methods.Should().BeEmpty();
    }

    [Fact]
    public void GroupClones_UniqueMethods_ReturnsEmptyList()
    {
        var methods = new List<MethodCloneInfo>
        {
            new("file1.cs", "M1", 1, 10, 8, "hash_one", "void M1()"),
            new("file2.cs", "M2", 1, 10, 8, "hash_two", "void M2()"),
        };

        var groups = _detector.GroupClones(methods);

        groups.Should().BeEmpty();
    }

    [Fact]
    public void GroupClones_DuplicateHashes_AreGroupedTogether()
    {
        var methods = new List<MethodCloneInfo>
        {
            new("file1.cs", "CalculateA", 1, 10, 8, "hash_shared", "void CalculateA()"),
            new("file2.cs", "CalculateB", 1, 10, 8, "hash_shared", "void CalculateB()"),
            new("file3.cs", "Unique", 1, 10, 8, "hash_unique", "void Unique()"),
        };

        var groups = _detector.GroupClones(methods);

        groups.Should().HaveCount(1);
        groups[0].NormalizedHash.Should().Be("hash_shared");
        groups[0].Methods.Should().HaveCount(2);
        groups[0].Methods.Select(m => m.MethodName).Should().Contain(["CalculateA", "CalculateB"]);
    }

    [Fact]
    public void GroupClones_MultipleGroups_AreOrderedByCountDescending()
    {
        var methods = new List<MethodCloneInfo>
        {
            new("f1.cs", "A1", 1, 10, 8, "hash_two_clones", "void A1()"),
            new("f2.cs", "A2", 1, 10, 8, "hash_two_clones", "void A2()"),
            new("f3.cs", "B1", 1, 10, 8, "hash_three_clones", "void B1()"),
            new("f4.cs", "B2", 1, 10, 8, "hash_three_clones", "void B2()"),
            new("f5.cs", "B3", 1, 10, 8, "hash_three_clones", "void B3()"),
        };

        var groups = _detector.GroupClones(methods);

        groups.Should().HaveCount(2);
        groups[0].NormalizedHash.Should().Be("hash_three_clones");
        groups[0].Methods.Should().HaveCount(3);
        groups[1].NormalizedHash.Should().Be("hash_two_clones");
        groups[1].Methods.Should().HaveCount(2);
    }

    [Fact]
    public void DetectClones_CrossFileDuplication_SuccessfullyDetectsClones()
    {
        var file1Content = @"
class OrderService
{
    public decimal ProcessOrder(decimal amount, decimal taxRate)
    {
        decimal basePrice = amount * 100;
        decimal subTotal = basePrice - 50;
        decimal tax = subTotal * taxRate;
        decimal shipping = 15;
        decimal grandTotal = subTotal + tax + shipping;
        Console.WriteLine(grandTotal);
        AuditLog(grandTotal);
        return grandTotal;
    }

    private void AuditLog(decimal val) {}
}";

        var file2Content = @"
class InvoiceHandler
{
    public decimal HandleInvoice(decimal price, decimal rate)
    {
        decimal startingCost = price * 200;
        decimal intermediate = startingCost - 100;
        decimal vat = intermediate * rate;
        decimal delivery = 25;
        decimal invoiceTotal = intermediate + vat + delivery;
        Console.WriteLine(invoiceTotal);
        AuditLog(invoiceTotal);
        return invoiceTotal;
    }

    private void AuditLog(decimal val) {}
}";

        var file3Content = @"
class UnrelatedService
{
    public void CompletelyDifferent()
    {
        for (int i = 0; i < 10; i++)
        {
            int a = i * 2;
            int b = a + 3;
            int c = b * 4;
            int d = c + 5;
            int e = d * 6;
            int f = e + 7;
            Console.WriteLine(f);
        }
    }
}";

        var tree1 = CSharpSyntaxTree.ParseText(file1Content);
        var tree2 = CSharpSyntaxTree.ParseText(file2Content);
        var tree3 = CSharpSyntaxTree.ParseText(file3Content);

        var settings = new CodeCondenserSettings
        {
            DuplicationStatementThreshold = 8
        };

        var syntaxTrees = new List<(Microsoft.CodeAnalysis.SyntaxTree Tree, string FilePath)>
        {
            (tree1, "Services/OrderService.cs"),
            (tree2, "Handlers/InvoiceHandler.cs"),
            (tree3, "Services/UnrelatedService.cs")
        };

        var cloneGroups = _detector.DetectClones(syntaxTrees, settings);

        cloneGroups.Should().HaveCount(1);
        var group = cloneGroups[0];
        group.Methods.Should().HaveCount(2);
        group.Methods.Select(m => m.MethodName).Should().Contain(["ProcessOrder", "HandleInvoice"]);
        group.Methods.Select(m => m.FilePath).Should().Contain(["Services/OrderService.cs", "Handlers/InvoiceHandler.cs"]);
    }

    [Fact]
    public void NullArguments_ThrowArgumentNullException()
    {
        var settings = new CodeCondenserSettings();
        var tree = CSharpSyntaxTree.ParseText("class C {}");

        Action act1 = () => _detector.ExtractMethods(null!, "file.cs", settings);
        act1.Should().Throw<ArgumentNullException>();

        Action act2 = () => _detector.ExtractMethods(tree, "file.cs", null!);
        act2.Should().Throw<ArgumentNullException>();

        Action act3 = () => _detector.GroupClones(null!);
        act3.Should().Throw<ArgumentNullException>();

        Action act4 = () => _detector.DetectClones(null!, settings);
        act4.Should().Throw<ArgumentNullException>();
    }
}
