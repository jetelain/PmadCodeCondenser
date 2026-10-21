using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Clones;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Clones;

public class SubMethodCloneDetectorTests
{
    private readonly SubMethodCloneDetector _detector = new();
    private readonly CodeCondenserSettings _settings = new()
    {
        EnableSubMethodCloneDetection = true,
        SubMethodStatementThreshold = 10
    };

    [Fact]
    public void DetectClones_WhenTwoMethodsShareTenStatements_DetectsSubMethodClone()
    {
        var code1 = @"
namespace App;
public class ServiceA
{
    public void ProcessA(int id)
    {
        int a = 1;
        string b = ""str"";
        bool c = true;
        double d = 3.14;
        char e = 'x';
        long f = 100L;
        short g = 2;
        byte h = 3;
        float i = 4.5f;
        decimal j = 5.5m;
        return;
    }
}";

        var code2 = @"
namespace App;
public class ServiceB
{
    public void ProcessB(int id)
    {
        throw new System.InvalidOperationException();
        int a = 10;
        string b = ""other"";
        bool c = false;
        double d = 2.71;
        char e = 'y';
        long f = 200L;
        short g = 4;
        byte h = 6;
        float i = 9.9f;
        decimal j = 7.7m;
    }
}";

        var tree1 = CSharpSyntaxTree.ParseText(code1, path: "ServiceA.cs");
        var tree2 = CSharpSyntaxTree.ParseText(code2, path: "ServiceB.cs");

        var blocks1 = _detector.ExtractBlocks(tree1, "ServiceA.cs", _settings);
        var blocks2 = _detector.ExtractBlocks(tree2, "ServiceB.cs", _settings);

        var groups = _detector.DetectClones(blocks1.Concat(blocks2), threshold: 10);

        groups.Should().HaveCount(1);
        var group = groups[0];
        group.StatementCount.Should().Be(10);
        group.Occurrences.Should().HaveCount(2);
        group.ImpactScore.Should().Be(10); // 10 * (2 - 1)
        group.Category.Should().Be(CloneCategory.Production);
        group.Occurrences.Select(o => o.MethodName).Should().Contain(["ProcessA", "ProcessB"]);
    }

    [Fact]
    public void DetectClones_GreedyContiguousExpansion_ExpandsBeyondWindowSize()
    {
        // 14 matching statements: window is 10, should expand to 14
        var code1 = @"
namespace App;
public class ServiceA
{
    public void ProcessA()
    {
        int a1 = 1;
        string a2 = ""2"";
        bool a3 = true;
        double a4 = 4.0;
        char a5 = '5';
        long a6 = 6L;
        short a7 = 7;
        byte a8 = 8;
        float a9 = 9f;
        decimal a10 = 10m;
        uint a11 = 11u;
        ulong a12 = 12ul;
        sbyte a13 = 13;
        ushort a14 = 14;
        return;
    }
}";

        var code2 = @"
namespace App;
public class ServiceB
{
    public void ProcessB()
    {
        throw new System.InvalidOperationException();
        int b1 = 10;
        string b2 = ""20"";
        bool b3 = false;
        double b4 = 40.0;
        char b5 = 'z';
        long b6 = 60L;
        short b7 = 70;
        byte b8 = 80;
        float b9 = 90f;
        decimal b10 = 100m;
        uint b11 = 110u;
        ulong b12 = 120ul;
        sbyte b13 = 127;
        ushort b14 = 140;
    }
}";

        var tree1 = CSharpSyntaxTree.ParseText(code1, path: "ServiceA.cs");
        var tree2 = CSharpSyntaxTree.ParseText(code2, path: "ServiceB.cs");

        var blocks = _detector.ExtractBlocks(tree1, "ServiceA.cs", _settings)
            .Concat(_detector.ExtractBlocks(tree2, "ServiceB.cs", _settings));

        var groups = _detector.DetectClones(blocks, threshold: 10);

        groups.Should().HaveCount(1);
        groups[0].StatementCount.Should().Be(14);
        groups[0].ImpactScore.Should().Be(14); // 14 * (2 - 1) = 14
    }

    [Fact]
    public void ExtractBlocks_FiltersEmptyStatementsAndUninitializedVariables()
    {
        var code = @"
namespace App;
public class ServiceA
{
    public void ProcessA()
    {
        ; // empty statement - filtered
        int uninit1; // uninitialized - filtered
        int uninit2, uninit3; // uninitialized - filtered
        int init1 = 1;
        int init2 = 2;
        int init3 = 3;
        int init4 = 4;
        int init5 = 5;
        int init6 = 6;
        int init7 = 7;
        int init8 = 8;
        int init9 = 9;
        int init10 = 10;
    }
}";

        var tree = CSharpSyntaxTree.ParseText(code, path: "ServiceA.cs");
        var blocks = _detector.ExtractBlocks(tree, "ServiceA.cs", _settings);

        blocks.Should().HaveCount(1);
        blocks[0].StatementCount.Should().Be(10);
    }

    [Fact]
    public void DetectClones_IntraMethodSelfMatch_IsSuppressed()
    {
        // Repeating the same 10-statement sequence twice within the same method body
        var code = @"
namespace App;
public class ServiceA
{
    public void ProcessLoop()
    {
        int a1 = 1; int a2 = 2; int a3 = 3; int a4 = 4; int a5 = 5;
        int a6 = 6; int a7 = 7; int a8 = 8; int a9 = 9; int a10 = 10;

        int b1 = 1; int b2 = 2; int b3 = 3; int b4 = 4; int b5 = 5;
        int b6 = 6; int b7 = 7; int b8 = 8; int b9 = 9; int b10 = 10;
    }
}";

        var tree = CSharpSyntaxTree.ParseText(code, path: "ServiceA.cs");
        var blocks = _detector.ExtractBlocks(tree, "ServiceA.cs", _settings);

        var groups = _detector.DetectClones(blocks, threshold: 10);

        // Self-matches within the same method must be suppressed
        groups.Should().BeEmpty();
    }

    [Fact]
    public void DetectClones_DistinctMethodsInSameFile_IsReported()
    {
        var code = @"
namespace App;
public class ServiceA
{
    public void Method1()
    {
        int a1 = 1;
        string a2 = ""2"";
        bool a3 = true;
        double a4 = 4.0;
        char a5 = '5';
        long a6 = 6L;
        short a7 = 7;
        byte a8 = 8;
        float a9 = 9f;
        decimal a10 = 10m;
        return;
    }

    public void Method2()
    {
        throw new System.InvalidOperationException();
        int b1 = 10;
        string b2 = ""20"";
        bool b3 = false;
        double b4 = 40.0;
        char b5 = 'z';
        long b6 = 60L;
        short b7 = 70;
        byte b8 = 80;
        float b9 = 90f;
        decimal b10 = 100m;
    }
}";

        var tree = CSharpSyntaxTree.ParseText(code, path: "ServiceA.cs");
        var blocks = _detector.ExtractBlocks(tree, "ServiceA.cs", _settings);

        var groups = _detector.DetectClones(blocks, threshold: 10);

        groups.Should().HaveCount(1);
        groups[0].StatementCount.Should().Be(10);
        groups[0].Occurrences.Select(o => o.MethodName).Should().Contain(["Method1", "Method2"]);
    }

    [Fact]
    public void DetectClones_WholeMethodSubsumption_SuppressesRedundantSubMethodMatches()
    {
        var code1 = @"
namespace App;
public class ServiceA
{
    public void MethodA()
    {
        int a1 = 1; int a2 = 2; int a3 = 3; int a4 = 4; int a5 = 5;
        int a6 = 6; int a7 = 7; int a8 = 8; int a9 = 9; int a10 = 10;
    }
}";

        var code2 = @"
namespace App;
public class ServiceB
{
    public void MethodB()
    {
        int b1 = 1; int b2 = 2; int b3 = 3; int b4 = 4; int b5 = 5;
        int b6 = 6; int b7 = 7; int b8 = 8; int b9 = 9; int b10 = 10;
    }
}";

        var tree1 = CSharpSyntaxTree.ParseText(code1, path: "ServiceA.cs");
        var tree2 = CSharpSyntaxTree.ParseText(code2, path: "ServiceB.cs");

        var wholeMethodClone = new CodeCloneGroup(
            NormalizedHash: "fake_whole_method_hash",
            Methods:
            [
                new MethodCloneInfo("ServiceA.cs", "MethodA", 5, 10, 10, "fake_whole_method_hash", "void MethodA()"),
                new MethodCloneInfo("ServiceB.cs", "MethodB", 5, 10, 10, "fake_whole_method_hash", "void MethodB()")
            ]);

        var blocks = _detector.ExtractBlocks(tree1, "ServiceA.cs", _settings)
            .Concat(_detector.ExtractBlocks(tree2, "ServiceB.cs", _settings));

        var groups = _detector.DetectClones(blocks, threshold: 10, wholeMethodClones: [wholeMethodClone]);

        // Since MethodA and MethodB are already a whole-method clone, sub-method clone is subsumed!
        groups.Should().BeEmpty();
    }

    [Fact]
    public void DetectClones_PartialDivergence_CapturesSubMethodDuplication()
    {
        // MethodA has 17 lines, MethodB has 14 lines. They share 12 lines in the middle.
        // Whole-method clone detector will NOT detect them because total methods don't match.
        // Sub-method detector MUST detect them!
        var code1 = @"
namespace App;
public class ServiceA
{
    public void MethodA()
    {
        return;
        throw new System.InvalidOperationException();
        while (true) break;
        int a1 = 1;
        string a2 = ""2"";
        bool a3 = true;
        double a4 = 4.0;
        char a5 = '5';
        long a6 = 6L;
        short a7 = 7;
        byte a8 = 8;
        float a9 = 9f;
        decimal a10 = 10m;
        uint a11 = 11u;
        ulong a12 = 12ul;
        lock (this) { }
        checked { }
    }
}";

        var code2 = @"
namespace App;
public class ServiceB
{
    public void MethodB()
    {
        try { } catch { }
        int b1 = 10;
        string b2 = ""20"";
        bool b3 = false;
        double b4 = 40.0;
        char b5 = 'z';
        long b6 = 60L;
        short b7 = 70;
        byte b8 = 80;
        float b9 = 90f;
        decimal b10 = 100m;
        uint b11 = 110u;
        ulong b12 = 120ul;
        for (;;);
    }
}";

        var tree1 = CSharpSyntaxTree.ParseText(code1, path: "ServiceA.cs");
        var tree2 = CSharpSyntaxTree.ParseText(code2, path: "ServiceB.cs");

        var blocks = _detector.ExtractBlocks(tree1, "ServiceA.cs", _settings)
            .Concat(_detector.ExtractBlocks(tree2, "ServiceB.cs", _settings));

        var groups = _detector.DetectClones(blocks, threshold: 10);

        groups.Should().HaveCount(1);
        groups[0].StatementCount.Should().Be(12);
        groups[0].ImpactScore.Should().Be(12);
    }

    [Fact]
    public void ExtractBlocks_IgnoredMethod_IsSkipped()
    {
        var code = @"
using System.Diagnostics.CodeAnalysis;
namespace App;
public class ServiceA
{
    [SuppressMessage(""CodeCondenser"", ""Duplication"")]
    public void ProcessA()
    {
        int a1 = 1; int a2 = 2; int a3 = 3; int a4 = 4; int a5 = 5;
        int a6 = 6; int a7 = 7; int a8 = 8; int a9 = 9; int a10 = 10;
    }

    // codecondenser:ignore-duplicate
    public void ProcessB()
    {
        int b1 = 1; int b2 = 2; int b3 = 3; int b4 = 4; int b5 = 5;
        int b6 = 6; int b7 = 7; int b8 = 8; int b9 = 9; int b10 = 10;
    }
}";

        var tree = CSharpSyntaxTree.ParseText(code, path: "ServiceA.cs");
        var blocks = _detector.ExtractBlocks(tree, "ServiceA.cs", _settings);

        blocks.Should().BeEmpty();
    }

    [Fact]
    public void DetectClones_Categorization_ClassifiesProductionTestAndCrossBoundary()
    {
        var code = @"
namespace App;
public class S
{
    public void M()
    {
        int a1 = 1; int a2 = 2; int a3 = 3; int a4 = 4; int a5 = 5;
        int a6 = 6; int a7 = 7; int a8 = 8; int a9 = 9; int a10 = 10;
    }
}";

        var treeProd1 = CSharpSyntaxTree.ParseText(code, path: "src/Service1.cs");
        var treeProd2 = CSharpSyntaxTree.ParseText(code, path: "src/Service2.cs");
        var treeTest1 = CSharpSyntaxTree.ParseText(code, path: "tests/ServiceTest1.cs");

        var blocksProd = _detector.ExtractBlocks(treeProd1, "src/Service1.cs", _settings)
            .Concat(_detector.ExtractBlocks(treeProd2, "src/Service2.cs", _settings));

        var prodGroup = _detector.DetectClones(blocksProd, 10, fileClassifier: p => p.StartsWith("tests") ? FileCategory.Test : FileCategory.Production);
        prodGroup[0].Category.Should().Be(CloneCategory.Production);

        var blocksCross = _detector.ExtractBlocks(treeProd1, "src/Service1.cs", _settings)
            .Concat(_detector.ExtractBlocks(treeTest1, "tests/ServiceTest1.cs", _settings));

        var crossGroup = _detector.DetectClones(blocksCross, 10, fileClassifier: p => p.StartsWith("tests") ? FileCategory.Test : FileCategory.Production);
        crossGroup[0].Category.Should().Be(CloneCategory.CrossBoundary);
    }

    [Fact]
    public void DetectClones_OverloadedMethodsInSameFile_DetectsSubMethodClone()
    {
        var code = @"
namespace App;
public class ServiceWithOverloads
{
    public void Execute(int id)
    {
        int a1 = 1;
        string a2 = ""2"";
        bool a3 = true;
        double a4 = 4.0;
        char a5 = '5';
        long a6 = 6L;
        short a7 = 7;
        byte a8 = 8;
        float a9 = 9f;
        decimal a10 = 10m;
        return;
    }

    public void Execute(string name)
    {
        throw new System.InvalidOperationException();
        int b1 = 10;
        string b2 = ""20"";
        bool b3 = false;
        double b4 = 40.0;
        char b5 = 'z';
        long b6 = 60L;
        short b7 = 70;
        byte b8 = 80;
        float b9 = 90f;
        decimal b10 = 100m;
    }
}";

        var tree = CSharpSyntaxTree.ParseText(code, path: "ServiceWithOverloads.cs");
        var blocks = _detector.ExtractBlocks(tree, "ServiceWithOverloads.cs", _settings);

        var groups = _detector.DetectClones(blocks, threshold: 10);

        groups.Should().HaveCount(1);
        groups[0].StatementCount.Should().Be(10);
        groups[0].Occurrences.Should().HaveCount(2);
        groups[0].Occurrences.All(o => o.MethodName == "Execute").Should().BeTrue();
        groups[0].Occurrences.Select(o => o.StartLine).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public void DetectClones_SameMethodNameInDifferentClassesInSameFile_DetectsSubMethodClone()
    {
        var code = @"
namespace App;
public class HandlerA
{
    public void Handle()
    {
        int a1 = 1;
        string a2 = ""2"";
        bool a3 = true;
        double a4 = 4.0;
        char a5 = '5';
        long a6 = 6L;
        short a7 = 7;
        byte a8 = 8;
        float a9 = 9f;
        decimal a10 = 10m;
        return;
    }
}

public class HandlerB
{
    public void Handle()
    {
        throw new System.InvalidOperationException();
        int b1 = 10;
        string b2 = ""20"";
        bool b3 = false;
        double b4 = 40.0;
        char b5 = 'z';
        long b6 = 60L;
        short b7 = 70;
        byte b8 = 80;
        float b9 = 90f;
        decimal b10 = 100m;
    }
}";

        var tree = CSharpSyntaxTree.ParseText(code, path: "Handlers.cs");
        var blocks = _detector.ExtractBlocks(tree, "Handlers.cs", _settings);

        var groups = _detector.DetectClones(blocks, threshold: 10);

        groups.Should().HaveCount(1);
        groups[0].StatementCount.Should().Be(10);
        groups[0].Occurrences.Should().HaveCount(2);
        groups[0].Occurrences.All(o => o.MethodName == "Handle").Should().BeTrue();
        groups[0].Occurrences.Select(o => o.StartLine).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public void DetectClones_WholeMethodSubsumption_WithDifferentPathSeparatorsAndCasing_IsSubsumed()
    {
        var code1 = @"
namespace App;
public class ServiceA
{
    public void MethodA()
    {
        int a1 = 1; int a2 = 2; int a3 = 3; int a4 = 4; int a5 = 5;
        int a6 = 6; int a7 = 7; int a8 = 8; int a9 = 9; int a10 = 10;
    }
}";

        var code2 = @"
namespace App;
public class ServiceB
{
    public void MethodB()
    {
        int b1 = 1; int b2 = 2; int b3 = 3; int b4 = 4; int b5 = 5;
        int b6 = 6; int b7 = 7; int b8 = 8; int b9 = 9; int b10 = 10;
    }
}";

        var tree1 = CSharpSyntaxTree.ParseText(code1, path: "src/Services/ServiceA.cs");
        var tree2 = CSharpSyntaxTree.ParseText(code2, path: "src/Services/ServiceB.cs");

        // Notice: whole-method clone has backslashes and different casing
        var wholeMethodClone = new CodeCloneGroup(
            NormalizedHash: "fake_whole_method_hash",
            Methods:
            [
                new MethodCloneInfo("src\\services\\servicea.cs", "MethodA", 5, 10, 10, "fake_whole_method_hash", "void MethodA()"),
                new MethodCloneInfo("src\\SERVICES\\SERVICEB.CS", "MethodB", 5, 10, 10, "fake_whole_method_hash", "void MethodB()")
            ]);

        var blocks = _detector.ExtractBlocks(tree1, "src/Services/ServiceA.cs", _settings)
            .Concat(_detector.ExtractBlocks(tree2, "src/Services/ServiceB.cs", _settings));

        var groups = _detector.DetectClones(blocks, threshold: 10, wholeMethodClones: [wholeMethodClone]);

        // Path normalization should allow wholeMethodClone to correctly subsume the sub-method clone!
        groups.Should().BeEmpty();
    }
}
