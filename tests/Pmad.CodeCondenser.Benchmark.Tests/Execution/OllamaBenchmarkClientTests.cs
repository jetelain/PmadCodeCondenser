using FluentAssertions;
using Pmad.CodeCondenser.Benchmark.Execution;
using Pmad.CodeCondenser.Benchmark.Models;

namespace Pmad.CodeCondenser.Benchmark.Tests.Execution;

public class OllamaBenchmarkClientTests
{
    [Theory]
    [InlineData("http://localhost:11434", "http://localhost:11434")]
    [InlineData("http://127.0.0.1:11435/", "http://127.0.0.1:11435")]
    [InlineData("127.0.0.1:11435", "http://127.0.0.1:11435")]
    [InlineData("https://ollama.example.com", "https://ollama.example.com")]
    public void ResolveEndpoint_ExplicitValues_NormalizesCorrectly(string input, string expected)
    {
        var resolved = OllamaBenchmarkClient.ResolveEndpoint(input);
        resolved.Should().Be(expected);
    }

    [Fact]
    public void ParseJudgeResponse_ValidJson_ParsesAccurately()
    {
        var json = """
            {
              "accuracy": 8.5,
              "conciseness": 9.0,
              "formatting": 10.0,
              "overall": 9.0,
              "critique": "Crisp and faithful to original intent."
            }
            """;

        var eval = new JudgeEvaluation();
        OllamaBenchmarkClient.ParseJudgeResponse(json, eval);

        eval.Success.Should().BeTrue();
        eval.AccuracyScore.Should().Be(8.5);
        eval.ConcisenessScore.Should().Be(9.0);
        eval.FormatComplianceScore.Should().Be(10.0);
        eval.OverallScore.Should().Be(9.0);
        eval.Critique.Should().Be("Crisp and faithful to original intent.");
    }

    [Fact]
    public void ParseJudgeResponse_MarkdownWrappedJson_ParsesAccurately()
    {
        var raw = """
            Here is my evaluation:
            ```json
            {
              "accuracy": 7.0,
              "conciseness": 8.0,
              "formatting": 6.5,
              "overall": 7.2,
              "critique": "Dropped some remarks but maintained core summary."
            }
            ```
            Hope this helps!
            """;

        var eval = new JudgeEvaluation();
        OllamaBenchmarkClient.ParseJudgeResponse(raw, eval);

        eval.Success.Should().BeTrue();
        eval.AccuracyScore.Should().Be(7.0);
        eval.ConcisenessScore.Should().Be(8.0);
        eval.FormatComplianceScore.Should().Be(6.5);
        eval.OverallScore.Should().Be(7.2);
        eval.Critique.Should().Be("Dropped some remarks but maintained core summary.");
    }

    [Fact]
    public void ParseJudgeResponse_EmptyResponse_SetsError()
    {
        var eval = new JudgeEvaluation();
        OllamaBenchmarkClient.ParseJudgeResponse("", eval);

        eval.Success.Should().BeFalse();
        eval.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}
