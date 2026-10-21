using FluentAssertions;

namespace Pmad.CodeCondenser.Cli.Tests;

public class CliArgumentParserTests
{
    [Fact]
    public void TryParse_EmptyArgs_ReturnsDefaults()
    {
        var success = CliArgumentParser.TryParse([], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.Command.Should().Be(CliOptions.CommandAnalyze);
        options.TargetPath.Should().Be(".");
        options.Apply.Should().BeFalse();
        options.Incremental.Should().BeFalse();
        options.UpdateSnapshot.Should().BeFalse();
        options.ThresholdStatements.Should().BeNull();
        options.ThresholdComments.Should().BeNull();
    }

    [Theory]
    [InlineData("analyze", CliOptions.CommandAnalyze)]
    [InlineData("apply", CliOptions.CommandApply)]
    [InlineData("snapshot", CliOptions.CommandSnapshot)]
    public void TryParse_CommandVerbs_SetsCommandCorrectly(string verb, string expectedCommand)
    {
        var success = CliArgumentParser.TryParse([verb, "./src"], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.Command.Should().Be(expectedCommand);
        options.TargetPath.Should().Be("./src");
    }

    [Fact]
    public void TryParse_PositionalPathOnly_SetsTargetPath()
    {
        var success = CliArgumentParser.TryParse(["src/MyApp"], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.Command.Should().Be(CliOptions.CommandAnalyze);
        options.TargetPath.Should().Be("src/MyApp");
    }

    [Fact]
    public void TryParse_ApplyFlag_SetsApplyMode()
    {
        var success = CliArgumentParser.TryParse(["--apply", "src/MyApp"], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.Apply.Should().BeTrue();
        options.Command.Should().Be(CliOptions.CommandApply);
        options.TargetPath.Should().Be("src/MyApp");
    }

    [Fact]
    public void TryParse_IncrementalFlag_SetsIncrementalTrue()
    {
        var success = CliArgumentParser.TryParse(["-i"], out var options, out var error);

        success.Should().BeTrue();
        options.Incremental.Should().BeTrue();
    }

    [Fact]
    public void TryParse_UpdateSnapshotFlag_SetsUpdateSnapshot()
    {
        var success = CliArgumentParser.TryParse(["-u"], out var options, out var error);

        success.Should().BeTrue();
        options.UpdateSnapshot.Should().BeTrue();
        options.Command.Should().Be(CliOptions.CommandSnapshot);
    }

    [Fact]
    public void TryParse_ExplicitAnalyzeWithUpdateSnapshotFlag_PreservesAnalyzeCommand()
    {
        var success = CliArgumentParser.TryParse(["analyze", "-u", "src/MyApp"], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.Command.Should().Be(CliOptions.CommandAnalyze);
        options.UpdateSnapshot.Should().BeTrue();
        options.TargetPath.Should().Be("src/MyApp");
    }

    [Fact]
    public void TryParse_ThresholdOptions_ParsesIntegerValues()
    {
        var success = CliArgumentParser.TryParse(
            ["--threshold-statements", "12", "-tc", "150"],
            out var options,
            out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.ThresholdStatements.Should().Be(12);
        options.ThresholdComments.Should().Be(150);
    }

    [Fact]
    public void TryParse_XmlThresholdOptions_ParsesIntegerValues()
    {
        var success = CliArgumentParser.TryParse(
            ["--threshold-xml-summary", "80", "-txp", "35"],
            out var options,
            out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.ThresholdXmlSummary.Should().Be(80);
        options.ThresholdXmlParam.Should().Be(35);
    }

    [Fact]
    public void TryParse_XmlThresholdOptionsWithEquals_ParsesIntegerValues()
    {
        var success = CliArgumentParser.TryParse(
            ["-txs=75", "--threshold-xml-param=25"],
            out var options,
            out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.ThresholdXmlSummary.Should().Be(75);
        options.ThresholdXmlParam.Should().Be(25);
    }

    [Fact]
    public void TryParse_InvalidXmlThreshold_ReturnsError()
    {
        var success1 = CliArgumentParser.TryParse(["--threshold-xml-summary", "invalid"], out _, out var error1);
        success1.Should().BeFalse();
        error1.Should().Contain("Invalid value for --threshold-xml-summary");

        var success2 = CliArgumentParser.TryParse(["--threshold-xml-param", "0"], out _, out var error2);
        success2.Should().BeFalse();
        error2.Should().Contain("Invalid value for --threshold-xml-param");
    }

    [Fact]
    public void TryParse_EqualsSyntax_ParsesCorrectly()
    {
        var success = CliArgumentParser.TryParse(
            ["--threshold-statements=10", "--threshold-comments=80", "--model=deepseek"],
            out var options,
            out var error);

        success.Should().BeTrue();
        options.ThresholdStatements.Should().Be(10);
        options.ThresholdComments.Should().Be(80);
        options.Model.Should().Be("deepseek");
    }

    [Fact]
    public void TryParse_ShortOptionsWithEqualsSyntax_ParsesCorrectly()
    {
        var success = CliArgumentParser.TryParse(
            ["-ts=15", "-tc=120", "-m=llama3.2", "-e=http://myollama:11434", "-c=myconfig.json"],
            out var options,
            out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.ThresholdStatements.Should().Be(15);
        options.ThresholdComments.Should().Be(120);
        options.Model.Should().Be("llama3.2");
        options.Endpoint.Should().Be("http://myollama:11434");
        options.ConfigPath.Should().Be("myconfig.json");
    }

    [Fact]
    public void TryParse_OllamaSettings_ParsesModelAndEndpoint()
    {
        var success = CliArgumentParser.TryParse(
            ["--model", "mistral:7b", "--endpoint", "http://ai.local:11434", "--context-size", "16384"],
            out var options,
            out var error);

        success.Should().BeTrue();
        options.Model.Should().Be("mistral:7b");
        options.Endpoint.Should().Be("http://ai.local:11434");
        options.ContextSize.Should().Be(16384);
    }

    [Fact]
    public void TryParse_ContextSizeShortOption_ParsesCorrectly()
    {
        var success = CliArgumentParser.TryParse(["-cs=32768"], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.ContextSize.Should().Be(32768);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("256")] // < 512
    [InlineData("-1")]
    public void TryParse_InvalidContextSize_ReturnsError(string value)
    {
        var success = CliArgumentParser.TryParse(["--context-size", value], out _, out var error);

        success.Should().BeFalse();
        error.Should().Contain("Invalid value for --context-size");
    }

    [Fact]
    public void TryParse_ConfigPath_ParsesCorrectly()
    {
        var success = CliArgumentParser.TryParse(["--config", "custom.json"], out var options, out var error);

        success.Should().BeTrue();
        options.ConfigPath.Should().Be("custom.json");
    }

    [Fact]
    public void TryParse_NoColorFlag_SetsNoColor()
    {
        var success = CliArgumentParser.TryParse(["--no-color"], out var options, out var error);

        success.Should().BeTrue();
        options.NoColor.Should().BeTrue();
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("-?")]
    public void TryParse_HelpFlags_SetsHelp(string flag)
    {
        var success = CliArgumentParser.TryParse([flag], out var options, out var error);

        success.Should().BeTrue();
        options.Help.Should().BeTrue();
    }

    [Theory]
    [InlineData("-v")]
    [InlineData("--version")]
    public void TryParse_VersionFlags_SetsVersion(string flag)
    {
        var success = CliArgumentParser.TryParse([flag], out var options, out var error);

        success.Should().BeTrue();
        options.Version.Should().BeTrue();
    }

    [Fact]
    public void TryParse_UnknownOption_ReturnsError()
    {
        var success = CliArgumentParser.TryParse(["--invalid-option"], out _, out var error);

        success.Should().BeFalse();
        error.Should().Contain("Unrecognized option: '--invalid-option'");
    }

    [Fact]
    public void TryParse_InvalidThresholdStatement_ReturnsError()
    {
        var success = CliArgumentParser.TryParse(["--threshold-statements", "not-a-number"], out _, out var error);

        success.Should().BeFalse();
        error.Should().Contain("Invalid value for --threshold-statements");
    }

    [Fact]
    public void TryParse_MissingValue_ReturnsError()
    {
        var success = CliArgumentParser.TryParse(["--threshold-comments"], out _, out var error);

        success.Should().BeFalse();
        error.Should().Contain("Missing required value for option '--threshold-comments'");
    }

    [Fact]
    public void GetHelpText_ReturnsNonEmptyDocumentation()
    {
        var help = CliArgumentParser.GetHelpText();
        help.Should().Contain("pmad-codecondenser");
        help.Should().Contain("--apply");
        help.Should().Contain("--incremental");
        help.Should().Contain("--update-snapshot");
    }

    [Fact]
    public void GetVersionText_ReturnsVersionString()
    {
        var version = CliArgumentParser.GetVersionText();
        version.Should().Contain("pmad-codecondenser version");
    }

    [Theory]
    [InlineData("--no-comments")]
    [InlineData("--disable-comments")]
    [InlineData("--no-comment-summarization")]
    public void TryParse_DisableComments_SetsOptionFalse(string flag)
    {
        var success = CliArgumentParser.TryParse([flag], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.EnableCommentSummarization.Should().Be(false);
    }

    [Theory]
    [InlineData("--no-duplication")]
    [InlineData("--disable-duplication")]
    [InlineData("--no-clone-detection")]
    public void TryParse_DisableDuplication_SetsOptionFalse(string flag)
    {
        var success = CliArgumentParser.TryParse([flag], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.EnableDuplicationDetection.Should().Be(false);
    }

    [Theory]
    [InlineData("--duplication-tolerance", "3", 3)]
    [InlineData("-dt", "0", 0)]
    public void TryParse_DuplicationTolerance_SetsValue(string flag, string val, int expected)
    {
        var success = CliArgumentParser.TryParse([flag, val], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.DuplicationTolerance.Should().Be(expected);
    }

    [Fact]
    public void TryParse_NegativeDuplicationTolerance_ReturnsError()
    {
        var success = CliArgumentParser.TryParse(["--duplication-tolerance", "-1"], out _, out var error);

        success.Should().BeFalse();
        error.Should().Contain("Invalid value for --duplication-tolerance");
    }

    [Theory]
    [InlineData("--summarized-comments", ".codecondenser/custom-comments.json")]
    [InlineData("-sc", ".codecondenser/custom-comments.json")]
    [InlineData("--comments-file", ".codecondenser/custom-comments.json")]
    public void TryParse_SummarizedCommentsOption_SetsPath(string flag, string path)
    {
        var success = CliArgumentParser.TryParse([flag, path], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.SummarizedCommentsPath.Should().Be(path);
    }

    [Theory]
    [InlineData("--max-line-length", "100", 100)]
    [InlineData("-mll", "120", 120)]
    public void TryParse_MaxLineLength_SetsValue(string flag, string val, int expected)
    {
        var success = CliArgumentParser.TryParse([flag, val], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.MaxLineLength.Should().Be(expected);
    }

    [Fact]
    public void TryParse_TooShortMaxLineLength_ReturnsError()
    {
        var success = CliArgumentParser.TryParse(["--max-line-length", "10"], out _, out var error);

        success.Should().BeFalse();
        error.Should().Contain("Invalid value for --max-line-length");
    }

    [Fact]
    public void TryParse_IncludeAndExcludeFlags_CollectMultiplePatterns()
    {
        var args = new[]
        {
            "--include", "src/**",
            "-inc", "app/**",
            "--exclude", "**/Migrations/**",
            "-exc", "**/bin/**"
        };

        var success = CliArgumentParser.TryParse(args, out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.Include.Should().ContainInOrder("src/**", "app/**");
        options.Exclude.Should().ContainInOrder("**/Migrations/**", "**/bin/**");
    }

    [Theory]
    [InlineData("--exclude-tests")]
    [InlineData("--production-only")]
    public void TryParse_ExcludeTestsFlags_SetsExcludeTests(string flag)
    {
        var success = CliArgumentParser.TryParse([flag], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.ExcludeTests.Should().BeTrue();
    }

    [Fact]
    public void TryParse_EnforceTestDuplicationFlag_SetsProperty()
    {
        var success = CliArgumentParser.TryParse(["--enforce-test-duplication"], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.EnforceTestDuplication.Should().BeTrue();
    }

    [Fact]
    public void TryParse_IncludeGeneratedFlag_SetsProperty()
    {
        var success = CliArgumentParser.TryParse(["--include-generated"], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.IncludeGenerated.Should().BeTrue();
    }
}
