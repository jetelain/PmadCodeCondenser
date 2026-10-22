using System.Text.Json;
using FluentAssertions;
using Pmad.CodeCondenser.Cli.Formatting.Json;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Cli.Tests;

public class JsonReportFormatterTests
{
    private readonly JsonReportFormatter _formatter = new();
    private readonly CodeCondenserSettings _settings = new();

    [Fact]
    public async Task FormatAsync_FullAnalysisResult_EmitsConformingJson()
    {
        var methods = new List<MethodCloneInfo>
        {
            new("c:/repo/src/A.cs", "DoSomething", 10, 20, 10, "astHashA", "void DoSomething()"),
            new("c:/repo/src/B.cs", "DoSomethingElse", 30, 40, 10, "astHashA", "void DoSomethingElse()")
        };
        var cloneGroup = new CodeCloneGroup("astHashA", methods, CloneCategory.Production, ImpactScore: 10);

        var occurrences = new List<SubMethodCloneOccurrence>
        {
            new("c:/repo/src/SubA.cs", "MethodA", 50, 65, 15),
            new("c:/repo/src/SubB.cs", "MethodB", 70, 85, 15)
        };
        var subGroup = new SubMethodCloneGroup("subHash1", occurrences, 15, CloneCategory.Production, ImpactScore: 15);

        var comment = new VerboseComment(
            FilePath: "c:/repo/src/A.cs",
            StartLine: 1,
            EndLine: 5,
            SpanStart: 0,
            SpanLength: 120,
            Kind: CommentKind.SingleLine,
            OriginalText: new string('c', 120),
            LeadingWhitespace: "");

        var summaries = new Dictionary<string, string>
        {
            [comment.ContentHash] = "Summarized comment text."
        };

        var result = new AnalysisResult
        {
            AnalyzedFilesCount = 4,
            ProductionFilesCount = 4,
            TestFilesCount = 0,
            SkippedGeneratedFilesCount = 1,
            TotalMethodsScanned = 15,
            CloneGroups = [cloneGroup],
            SubMethodCloneGroups = [subGroup],
            VerboseComments = [comment],
            SummarizedComments = summaries
        };

        using var writer = new StringWriter();
        await _formatter.FormatAsync(result, _settings, writer, "c:/repo");

        var json = writer.ToString();
        var report = JsonSerializer.Deserialize<CodeCondenserJsonReport>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        report.Should().NotBeNull();
        report!.Schema.Should().Be("https://raw.githubusercontent.com/jetelain/PmadCodeCondenser/main/schemas/codecondenser-report.json");
        report.SchemaVersion.Should().Be("1.0.0");
        report.Timestamp.Should().NotBeNullOrWhiteSpace();

        // Summary verification
        report.Summary.FilesAnalyzed.Should().Be(4);
        report.Summary.ProductionFiles.Should().Be(4);
        report.Summary.TestFiles.Should().Be(0);
        report.Summary.GeneratedFilesSkipped.Should().Be(1);
        report.Summary.MethodsScanned.Should().Be(15);
        report.Summary.CloneClustersCount.Should().Be(1);
        report.Summary.ProductionClustersCount.Should().Be(1);
        report.Summary.TestClustersCount.Should().Be(0);
        report.Summary.CrossBoundaryClustersCount.Should().Be(0);
        report.Summary.SubMethodClustersCount.Should().Be(1);
        report.Summary.VerboseCommentsCount.Should().Be(1);
        report.Summary.TotalCommentGainCharacters.Should().Be(96);
        report.Summary.TotalCommentReductionPercent.Should().Be(80);
        report.Summary.TotalCommentGainTokens.Should().NotBeNull();
        report.Summary.TotalCommentGainTokens.Should().BeGreaterThan(0);

        // Clone Clusters
        report.CloneClusters.Should().HaveCount(1);
        var cluster = report.CloneClusters[0];
        cluster.ClusterId.Should().Be(1);
        cluster.Category.Should().Be("Production");
        cluster.ImpactScore.Should().Be(10);
        cluster.AstHash.Should().Be("astHashA");
        cluster.StatementCount.Should().Be(10);
        cluster.Methods.Should().HaveCount(2);
        cluster.Methods[0].FilePath.Should().Be("src/A.cs");
        cluster.Methods[0].MethodName.Should().Be("DoSomething");
        cluster.Methods[0].StartLine.Should().Be(10);
        cluster.Methods[0].EndLine.Should().Be(20);
        cluster.Recommendation.Should().Contain("Extract duplicate structural logic");

        // SubMethod Clusters
        report.SubMethodClusters.Should().HaveCount(1);
        var subCluster = report.SubMethodClusters[0];
        subCluster.ClusterId.Should().Be(1);
        subCluster.Category.Should().Be("Production");
        subCluster.StatementCount.Should().Be(15);
        subCluster.Occurrences.Should().HaveCount(2);
        subCluster.Occurrences[0].FilePath.Should().Be("src/SubA.cs");
        subCluster.Occurrences[0].MethodName.Should().Be("MethodA");
        subCluster.Occurrences[0].StartLine.Should().Be(50);
        subCluster.Occurrences[0].EndLine.Should().Be(65);
        subCluster.Recommendation.Should().Contain("Extract duplicate 15-statement block");

        // Verbose Comments
        report.VerboseComments.Should().HaveCount(1);
        var verbComment = report.VerboseComments[0];
        verbComment.FilePath.Should().Be("src/A.cs");
        verbComment.StartLine.Should().Be(1);
        verbComment.EndLine.Should().Be(5);
        verbComment.CharacterCount.Should().Be(120);
        verbComment.EstimatedTokens.Should().BeGreaterThan(0);
        verbComment.Kind.Should().Be("SingleLine");
        verbComment.Summary.Should().Be("Summarized comment text.");
        verbComment.GainCharacters.Should().Be(96);
        verbComment.GainTokens.Should().Be(report.Summary.TotalCommentGainTokens);
    }

    [Fact]
    public async Task FormatAsync_WithExistingFiles_PopulatesClonePotentialGainTokens()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "JsonGainTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var srcDir = Path.Combine(tempDir, "src");
            Directory.CreateDirectory(srcDir);

            var fileA = Path.Combine(srcDir, "A.cs");
            var fileB = Path.Combine(srcDir, "B.cs");
            File.WriteAllLines(fileA, Enumerable.Range(1, 10).Select(i => $"    var val{i} = ExecuteStep({i});"));
            File.WriteAllLines(fileB, Enumerable.Range(1, 10).Select(i => $"    var val{i} = ExecuteStep({i});"));

            var methods = new List<MethodCloneInfo>
            {
                new("src/A.cs", "DoStep", 1, 10, 10, "astHashA", "void DoStep()"),
                new("src/B.cs", "DoStep", 1, 10, 10, "astHashA", "void DoStep()")
            };
            var cloneGroup = new CodeCloneGroup("astHashA", methods, CloneCategory.Production, ImpactScore: 10);

            var occurrences = new List<SubMethodCloneOccurrence>
            {
                new("src/A.cs", "DoStep", 1, 5, 5),
                new("src/B.cs", "DoStep", 1, 5, 5)
            };
            var subGroup = new SubMethodCloneGroup("subHash1", occurrences, 5, CloneCategory.Production, ImpactScore: 5);

            var result = new AnalysisResult
            {
                AnalyzedFilesCount = 2,
                ProductionFilesCount = 2,
                TestFilesCount = 0,
                SkippedGeneratedFilesCount = 0,
                TotalMethodsScanned = 2,
                CloneGroups = [cloneGroup],
                SubMethodCloneGroups = [subGroup],
                VerboseComments = [],
                SummarizedComments = new Dictionary<string, string>()
            };

            using var writer = new StringWriter();
            await _formatter.FormatAsync(result, _settings, writer, tempDir);

            var json = writer.ToString();
            var report = JsonSerializer.Deserialize<CodeCondenserJsonReport>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            report.Should().NotBeNull();
            report!.Summary.TotalCloneGainTokens.Should().NotBeNull();
            report.Summary.TotalCloneGainTokens.Should().BeGreaterThan(0);

            report.CloneClusters.Should().HaveCount(1);
            report.CloneClusters[0].PotentialGainTokens.Should().NotBeNull();
            report.CloneClusters[0].PotentialGainTokens.Should().BeGreaterThan(0);

            report.SubMethodClusters.Should().HaveCount(1);
            report.SubMethodClusters[0].PotentialGainTokens.Should().NotBeNull();
            report.SubMethodClusters[0].PotentialGainTokens.Should().BeGreaterThan(0);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
