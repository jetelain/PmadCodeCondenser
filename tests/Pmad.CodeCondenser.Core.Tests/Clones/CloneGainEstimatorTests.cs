using FluentAssertions;
using Pmad.CodeCondenser.Core.Clones;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Tests.Clones;

public class CloneGainEstimatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cc-gain-" + Guid.NewGuid().ToString("N"));

    public CloneGainEstimatorTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteFile(string name, int lines)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllLines(path, Enumerable.Range(1, lines).Select(i => $"    var value{i} = Compute(argument{i}, {i});"));
        return path;
    }

    [Fact]
    public void EstimateGainTokens_MethodGroup_ExcludesOneKeptCopy()
    {
        var a = WriteFile("A.cs", 10);
        var b = WriteFile("B.cs", 10);
        var c = WriteFile("C.cs", 10);
        MethodCloneInfo M(string p) => new(p, "M", 1, 10, 10, "h", "void M()");

        var two = CloneGainEstimator.EstimateGainTokens(new CodeCloneGroup("h", [M(a), M(b)]));
        var three = CloneGainEstimator.EstimateGainTokens(new CodeCloneGroup("h", [M(a), M(b), M(c)]));

        two.Should().BeGreaterThan(0);
        three.Should().Be(two * 2);
    }

    [Fact]
    public void EstimateGainTokens_SubMethodGroup_UsesLineRange()
    {
        var a = WriteFile("A.cs", 10);
        var b = WriteFile("B.cs", 10);
        var group = new SubMethodCloneGroup("h",
            [new SubMethodCloneOccurrence(a, "M", 2, 4, 3), new SubMethodCloneOccurrence(b, "N", 5, 7, 3)], 3);

        CloneGainEstimator.EstimateGainTokens(group).Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimateGainTokens_MissingFiles_ReturnsZero()
    {
        var group = new CodeCloneGroup("h",
        [
            new MethodCloneInfo("nope/A.cs", "M", 1, 10, 10, "h", "void M()"),
            new MethodCloneInfo("nope/B.cs", "M", 1, 10, 10, "h", "void M()")
        ]);

        CloneGainEstimator.EstimateGainTokens(group).Should().Be(0);
    }

    [Fact]
    public void EstimateTotalGainTokens_SumsMethodAndSubMethodGroups()
    {
        var a = WriteFile("A.cs", 10);
        var b = WriteFile("B.cs", 10);
        var methodGroup = new CodeCloneGroup("h",
            [new MethodCloneInfo(a, "M", 1, 10, 10, "h", "s"), new MethodCloneInfo(b, "M", 1, 10, 10, "h", "s")]);
        var subGroup = new SubMethodCloneGroup("h2",
            [new SubMethodCloneOccurrence(a, "M", 1, 3, 3), new SubMethodCloneOccurrence(b, "M", 1, 3, 3)], 3);

        var total = CloneGainEstimator.EstimateTotalGainTokens([methodGroup], [subGroup]);

        total.Should().Be(CloneGainEstimator.EstimateGainTokens(methodGroup) + CloneGainEstimator.EstimateGainTokens(subGroup));
    }
}
