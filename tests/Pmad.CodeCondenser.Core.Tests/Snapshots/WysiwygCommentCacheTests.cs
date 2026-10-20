using FluentAssertions;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Core.Snapshots;

namespace Pmad.CodeCondenser.Core.Tests.Snapshots;

public class WysiwygCommentCacheTests
{
    private static VerboseComment CreateComment(string text, string filePath = "src/Foo.cs")
    {
        return new VerboseComment(
            FilePath: filePath,
            StartLine: 1,
            EndLine: 2,
            SpanStart: 0,
            SpanLength: text.Length,
            Kind: CommentKind.SingleLine,
            OriginalText: text,
            LeadingWhitespace: "    ");
    }

    [Fact]
    public void EmptyCache_HasZeroCountAndReturnsFalseOnLookup()
    {
        var cache = new WysiwygCommentCache();

        cache.Count.Should().Be(0);
        cache.TryGet("anyhash", out var summary).Should().BeFalse();
        summary.Should().BeNull();
        cache.Contains("anyhash").Should().BeFalse();
    }

    [Fact]
    public void SetAndGet_ByContentHash_StoresAndRetrievesSummary()
    {
        var cache = new WysiwygCommentCache();

        cache.Set("hash123", "Short summary");

        cache.Count.Should().Be(1);
        cache.Contains("hash123").Should().BeTrue();
        cache.TryGet("hash123", out var summary).Should().BeTrue();
        summary.Should().Be("Short summary");
    }

    [Fact]
    public void SetAndGet_ByVerboseComment_UsesContentHash()
    {
        var cache = new WysiwygCommentCache();
        var comment = CreateComment("// This is an overly verbose comment that will be summarized.");

        cache.Set(comment, "Concise comment.");

        cache.Contains(comment).Should().BeTrue();
        cache.TryGet(comment, out var summary).Should().BeTrue();
        summary.Should().Be("Concise comment.");
    }

    [Fact]
    public void SetRange_AddsAllEntries()
    {
        var cache = new WysiwygCommentCache();
        var entries = new Dictionary<string, string>
        {
            ["hashA"] = "Summary A",
            ["hashB"] = "Summary B"
        };

        cache.SetRange(entries);

        cache.Count.Should().Be(2);
        cache["hashA"].Should().Be("Summary A");
        cache["hashB"].Should().Be("Summary B");
    }

    [Fact]
    public void LoadFromSnapshot_PopulatesCache()
    {
        var snapshot = new AnalysisSnapshot
        {
            CommentCache =
            {
                ["hash1"] = "Summary 1",
                ["hash2"] = "Summary 2"
            }
        };

        var cache = new WysiwygCommentCache();
        cache.LoadFrom(snapshot);

        cache.Count.Should().Be(2);
        cache["hash1"].Should().Be("Summary 1");
        cache["hash2"].Should().Be("Summary 2");
    }

    [Fact]
    public void SaveToSnapshot_UpdatesSnapshotCommentCache()
    {
        var cache = new WysiwygCommentCache();
        cache.Set("hash1", "Summary 1");
        cache.Set("hash2", "Summary 2");

        var snapshot = new AnalysisSnapshot();
        cache.SaveTo(snapshot);

        snapshot.CommentCache.Should().HaveCount(2);
        snapshot.CommentCache["hash1"].Should().Be("Summary 1");
        snapshot.CommentCache["hash2"].Should().Be("Summary 2");
    }

    [Fact]
    public void FromSnapshot_CreatesPrePopulatedCache()
    {
        var snapshot = new AnalysisSnapshot
        {
            CommentCache = { ["h1"] = "s1" }
        };

        var cache = WysiwygCommentCache.FromSnapshot(snapshot);

        cache.Count.Should().Be(1);
        cache["h1"].Should().Be("s1");
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var cache = new WysiwygCommentCache();
        cache.Set("h1", "s1");
        cache.Clear();

        cache.Count.Should().Be(0);
        cache.Contains("h1").Should().BeFalse();
    }

    [Fact]
    public void ConcurrentAccess_IsThreadSafe()
    {
        var cache = new WysiwygCommentCache();
        const int iterations = 1000;

        Parallel.For(0, iterations, i =>
        {
            var hash = $"hash_{i % 50}";
            cache.Set(hash, $"summary_{i}");
            cache.TryGet(hash, out _);
        });

        cache.Count.Should().Be(50);
    }
}
