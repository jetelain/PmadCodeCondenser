using AwesomeAssertions;
using Pmad.CodeCondenser.Benchmark.Models;
using Pmad.CodeCondenser.Benchmark.Reporting;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Benchmark.Tests.Reporting;

public class BenchmarkReporterTests
{
    [Fact]
    public void ComputeStats_ValidRun_AggregatesAccurately()
    {
        var run = new BenchmarkRun
        {
            JudgeModel = "Judge35B",
            CommentCount = 2,
            CandidateModels = new() { "ModelA", "ModelB" },
            Results = new()
            {
                new ModelExecutionResult
                {
                    ModelName = "ModelA",
                    CommentId = "c1",
                    Kind = CommentKind.XmlDocumentation,
                    ElapsedMilliseconds = 1000,
                    TokensPerSecond = 50,
                    CompressionRatio = 40.0,
                    IsValidFormat = true,
                    Success = true,
                    Evaluation = new JudgeEvaluation
                    {
                        Success = true,
                        OverallScore = 9.0,
                        AccuracyScore = 9.0,
                        ConcisenessScore = 8.5,
                        FormatComplianceScore = 10.0
                    }
                },
                new ModelExecutionResult
                {
                    ModelName = "ModelA",
                    CommentId = "c2",
                    Kind = CommentKind.SingleLine,
                    ElapsedMilliseconds = 500,
                    TokensPerSecond = 70,
                    CompressionRatio = 60.0,
                    IsValidFormat = true,
                    Success = true,
                    Evaluation = new JudgeEvaluation
                    {
                        Success = true,
                        OverallScore = 8.0,
                        AccuracyScore = 8.0,
                        ConcisenessScore = 9.0,
                        FormatComplianceScore = 9.0
                    }
                },
                new ModelExecutionResult
                {
                    ModelName = "ModelB",
                    CommentId = "c1",
                    Kind = CommentKind.XmlDocumentation,
                    ElapsedMilliseconds = 2000,
                    TokensPerSecond = 30,
                    CompressionRatio = 30.0,
                    IsValidFormat = false,
                    Success = true,
                    Evaluation = new JudgeEvaluation
                    {
                        Success = true,
                        OverallScore = 6.0,
                        AccuracyScore = 7.0,
                        ConcisenessScore = 6.0,
                        FormatComplianceScore = 5.0
                    }
                }
            }
        };

        var reporter = new BenchmarkReporter();
        var stats = reporter.ComputeStats(run);

        stats.Should().HaveCount(2);

        var statA = stats.First(s => s.ModelName == "ModelA");
        statA.TotalEvaluated.Should().Be(2);
        statA.SuccessRate.Should().Be(100.0);
        statA.AverageLatencyMs.Should().Be(750.0);
        statA.AverageOverallScore.Should().Be(8.5);
        statA.AverageCompressionRatio.Should().Be(50.0);
        statA.FormatComplianceRate.Should().Be(100.0);
        statA.ByCategory.Should().ContainKey(CommentKind.XmlDocumentation);
        statA.ByCategory.Should().ContainKey(CommentKind.SingleLine);

        var statB = stats.First(s => s.ModelName == "ModelB");
        statB.FormatComplianceRate.Should().Be(0.0);
        statB.AverageOverallScore.Should().Be(6.0);
    }

    [Fact]
    public async Task GenerateMarkdownReportAsync_CreatesValidFile()
    {
        var run = new BenchmarkRun
        {
            JudgeModel = "TestJudge",
            CommentCount = 1,
            Results = new()
            {
                new ModelExecutionResult
                {
                    ModelName = "Candidate1",
                    CommentId = "c1",
                    Kind = CommentKind.XmlDocumentation,
                    OriginalText = "/// <summary>Original long comment</summary>",
                    CleanedSummary = "/// <summary>Short summary</summary>",
                    ElapsedMilliseconds = 800,
                    TokensPerSecond = 45,
                    CompressionRatio = 50.0,
                    IsValidFormat = true,
                    Success = true,
                    Evaluation = new JudgeEvaluation
                    {
                        Success = true,
                        OverallScore = 8.5,
                        AccuracyScore = 8.0,
                        ConcisenessScore = 9.0,
                        FormatComplianceScore = 10.0,
                        Critique = "Great job"
                    }
                }
            }
        };

        var reporter = new BenchmarkReporter();
        var stats = reporter.ComputeStats(run);

        var tempFile = Path.Combine(Path.GetTempPath(), $"bench_report_{Guid.NewGuid():N}.md");
        try
        {
            await reporter.GenerateMarkdownReportAsync(run, stats, tempFile);

            File.Exists(tempFile).Should().BeTrue();
            var content = await File.ReadAllTextAsync(tempFile);
            content.Should().Contain("Comment Summarization Model Benchmark Report");
            content.Should().Contain("Candidate1");
            content.Should().Contain("8.50");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
