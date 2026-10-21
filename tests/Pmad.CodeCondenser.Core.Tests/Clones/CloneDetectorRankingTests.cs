using FluentAssertions;
using Pmad.CodeCondenser.Core.Clones;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Tests.Clones;

public class CloneDetectorRankingTests
{
    private readonly CloneDetector _detector = new();

    [Fact]
    public void GroupClones_SortsClustersDescendingByImpactScore()
    {
        // Cluster A: 10 statements x 4 methods -> Impact: 30
        var methodsA = Enumerable.Range(1, 4).Select(i =>
            new MethodCloneInfo($"src/A{i}.cs", $"MethodA{i}", 1, 10, 10, "hash_A", "sigA")
        ).ToList();

        // Cluster B: 25 statements x 2 methods -> Impact: 25
        var methodsB = Enumerable.Range(1, 2).Select(i =>
            new MethodCloneInfo($"src/B{i}.cs", $"MethodB{i}", 1, 25, 25, "hash_B", "sigB")
        ).ToList();

        // Cluster C: 8 statements x 2 methods -> Impact: 8
        var methodsC = Enumerable.Range(1, 2).Select(i =>
            new MethodCloneInfo($"src/C{i}.cs", $"MethodC{i}", 1, 8, 8, "hash_C", "sigC")
        ).ToList();

        var all = methodsC.Concat(methodsA).Concat(methodsB).ToList();
        var groups = _detector.GroupClones(all);

        groups.Should().HaveCount(3);
        groups[0].NormalizedHash.Should().Be("hash_A");
        groups[0].ImpactScore.Should().Be(30);

        groups[1].NormalizedHash.Should().Be("hash_B");
        groups[1].ImpactScore.Should().Be(25);

        groups[2].NormalizedHash.Should().Be("hash_C");
        groups[2].ImpactScore.Should().Be(8);
    }

    [Fact]
    public void GroupClones_BreaksTiesByStatementCountDescending_ThenByHashAscending()
    {
        // Both have impact score = 24
        // Cluster 1: 24 statements x 2 methods = 24
        var methods1 = Enumerable.Range(1, 2).Select(i =>
            new MethodCloneInfo($"src/M{i}.cs", $"M{i}", 1, 24, 24, "hash_Z", "sig")
        ).ToList();

        // Cluster 2: 12 statements x 3 methods = 24
        var methods2 = Enumerable.Range(1, 3).Select(i =>
            new MethodCloneInfo($"src/N{i}.cs", $"N{i}", 1, 12, 12, "hash_A", "sig")
        ).ToList();

        // Cluster 3: identical impact (24) and identical statement count (24), tie broken by hash string: "hash_M" vs "hash_Z"
        var methods3 = Enumerable.Range(1, 2).Select(i =>
            new MethodCloneInfo($"src/P{i}.cs", $"P{i}", 1, 24, 24, "hash_M", "sig")
        ).ToList();

        var all = methods1.Concat(methods2).Concat(methods3).ToList();
        var groups = _detector.GroupClones(all);

        groups.Should().HaveCount(3);
        // Between methods3 (hash_M) and methods1 (hash_Z), both have statement count 24. hash_M < hash_Z alphabetically
        groups[0].NormalizedHash.Should().Be("hash_M");
        groups[0].StatementCount.Should().Be(24);

        groups[1].NormalizedHash.Should().Be("hash_Z");
        groups[1].StatementCount.Should().Be(24);

        // methods2 has statement count 12, so it comes after the 24-statement clusters
        groups[2].NormalizedHash.Should().Be("hash_A");
        groups[2].StatementCount.Should().Be(12);
    }

    [Fact]
    public void GroupClones_ClassifiesCategories_ProductionTestAndCrossBoundary()
    {
        var prodMethods = new List<MethodCloneInfo>
        {
            new("src/Service1.cs", "M1", 1, 10, 10, "hash_prod", "void M1()"),
            new("src/Service2.cs", "M2", 1, 10, 10, "hash_prod", "void M2()")
        };

        var testMethods = new List<MethodCloneInfo>
        {
            new("tests/Service1Tests.cs", "T1", 1, 10, 10, "hash_test", "void T1()"),
            new("tests/Service2Tests.cs", "T2", 1, 10, 10, "hash_test", "void T2()")
        };

        var crossMethods = new List<MethodCloneInfo>
        {
            new("src/Service3.cs", "M3", 1, 10, 10, "hash_cross", "void M3()"),
            new("tests/Service3Tests.cs", "T3", 1, 10, 10, "hash_cross", "void T3()")
        };

        FileCategory Classifier(string path) => path.StartsWith("tests/") ? FileCategory.Test : FileCategory.Production;

        var all = prodMethods.Concat(testMethods).Concat(crossMethods);
        var groups = _detector.GroupClones(all, Classifier);

        groups.Should().HaveCount(3);
        var prodGroup = groups.Single(g => g.NormalizedHash == "hash_prod");
        prodGroup.Category.Should().Be(CloneCategory.Production);

        var testGroup = groups.Single(g => g.NormalizedHash == "hash_test");
        testGroup.Category.Should().Be(CloneCategory.Test);

        var crossGroup = groups.Single(g => g.NormalizedHash == "hash_cross");
        crossGroup.Category.Should().Be(CloneCategory.CrossBoundary);
    }

    [Fact]
    public void StatementCount_ReturnsMaxStatementCount_WhenBaselineMethodHasZeroStatements()
    {
        // In incremental mode, baseline methods have StatementCount = 0
        var baselineMethod = new MethodCloneInfo("src/Old.cs", "OldMethod", 1, 1, 0, "hash_dup", "void OldMethod()");
        var scannedMethod = new MethodCloneInfo("src/New.cs", "NewMethod", 10, 25, 15, "hash_dup", "void NewMethod()");

        // Baseline method is first in the list
        var group = new CodeCloneGroup("hash_dup", [baselineMethod, scannedMethod], CloneCategory.Production, ImpactScore: 15);

        group.StatementCount.Should().Be(15);
    }
}
