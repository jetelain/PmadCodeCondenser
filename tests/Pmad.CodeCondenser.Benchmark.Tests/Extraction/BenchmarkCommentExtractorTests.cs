using FluentAssertions;
using Pmad.CodeCondenser.Benchmark.Extraction;
using Pmad.CodeCondenser.Benchmark.Models;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Benchmark.Tests.Extraction;

public class BenchmarkCommentExtractorTests
{
    [Fact]
    public void StratifiedSample_LessThanTarget_ReturnsAll()
    {
        var comments = new List<BenchmarkComment>
        {
            new("c1", "ProjA", "File1.cs", 1, 5, CommentKind.XmlDocumentation, "/// <summary>test</summary>", "h1", 50, 5),
            new("c2", "ProjB", "File2.cs", 10, 15, CommentKind.SingleLine, "// some single line", "h2", 30, 1)
        };

        var sampled = BenchmarkCommentExtractor.StratifiedSample(comments, 10);

        sampled.Should().HaveCount(2);
    }

    [Fact]
    public void StratifiedSample_DiverseSet_BalancesProjectsAndKinds()
    {
        var comments = new List<BenchmarkComment>();

        for (int i = 0; i < 20; i++)
        {
            comments.Add(new($"c_a_{i}", "ProjA", $"File{i}.cs", 1, 5, CommentKind.XmlDocumentation, $"/// <summary>text {i}</summary>", $"ha_{i}", 100 + i * 20, 5));
            comments.Add(new($"c_b_{i}", "ProjB", $"File{i}.cs", 1, 3, CommentKind.SingleLine, $"// single line comment {i}", $"hb_{i}", 80 + i * 10, 1));
        }

        var sampled = BenchmarkCommentExtractor.StratifiedSample(comments, 10);

        sampled.Should().HaveCount(10);
        sampled.Select(s => s.Project).Distinct().Should().Contain("ProjA");
        sampled.Select(s => s.Project).Distinct().Should().Contain("ProjB");
        sampled.Select(s => s.Kind).Distinct().Should().Contain(CommentKind.XmlDocumentation);
        sampled.Select(s => s.Kind).Distinct().Should().Contain(CommentKind.SingleLine);
    }
}
