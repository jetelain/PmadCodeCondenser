using FluentAssertions;
using Pmad.CodeCondenser.Benchmark.Cli;

namespace Pmad.CodeCondenser.Benchmark.Tests.Cli;

public class BenchmarkCliParserTests
{
    [Fact]
    public void TryParse_EmptyArgs_ReturnsHelp()
    {
        var result = BenchmarkCliParser.TryParse(Array.Empty<string>(), out var options, out var error);

        result.Should().BeTrue();
        options.Command.Should().Be(BenchmarkCommand.Help);
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("help")]
    public void TryParse_HelpOptions_SetsHelp(string arg)
    {
        var result = BenchmarkCliParser.TryParse(new[] { arg }, out var options, out var error);

        result.Should().BeTrue();
        options.Help.Should().BeTrue();
    }

    [Theory]
    [InlineData("extract", BenchmarkCommand.Extract)]
    [InlineData("run", BenchmarkCommand.Run)]
    [InlineData("bench", BenchmarkCommand.Run)]
    [InlineData("judge", BenchmarkCommand.Judge)]
    [InlineData("report", BenchmarkCommand.Report)]
    [InlineData("all", BenchmarkCommand.All)]
    public void TryParse_Commands_SetsCommand(string cmd, BenchmarkCommand expected)
    {
        var result = BenchmarkCliParser.TryParse(new[] { cmd }, out var options, out var error);

        result.Should().BeTrue();
        options.Command.Should().Be(expected);
        error.Should().BeNull();
    }

    [Fact]
    public void TryParse_CandidatesAndJudge_ParsesCorrectly()
    {
        var args = new[]
        {
            "run",
            "-c", "gemma4:e4b,qwen3.5:4b",
            "-j", "Qwen3.6-35B-A3B:latest",
            "-s", "20",
            "-l", "10",
            "--endpoint", "http://127.0.0.1:11435",
            "--skip-judge",
            "--resume",
            "--no-color"
        };

        var result = BenchmarkCliParser.TryParse(args, out var options, out var error);

        result.Should().BeTrue();
        options.Command.Should().Be(BenchmarkCommand.Run);
        options.CandidateModels.Should().BeEquivalentTo(new[] { "gemma4:e4b", "qwen3.5:4b" });
        options.JudgeModel.Should().Be("Qwen3.6-35B-A3B:latest");
        options.SampleSize.Should().Be(20);
        options.Limit.Should().Be(10);
        options.Endpoint.Should().Be("http://127.0.0.1:11435");
        options.SkipJudge.Should().BeTrue();
        options.Resume.Should().BeTrue();
        options.NoColor.Should().BeTrue();
    }

    [Fact]
    public void TryParse_InvalidCommand_ReturnsError()
    {
        var result = BenchmarkCliParser.TryParse(new[] { "unknown_verb" }, out _, out var error);

        result.Should().BeFalse();
        error.Should().Contain("Unknown command");
    }

    [Fact]
    public void TryParse_MissingOptionValue_ReturnsError()
    {
        var result = BenchmarkCliParser.TryParse(new[] { "run", "--candidates" }, out _, out var error);

        result.Should().BeFalse();
        error.Should().Contain("Missing value");
    }
}
