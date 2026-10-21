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

    [Theory]
    [InlineData("--sub-method-clones")]
    [InlineData("--submethod-clones")]
    [InlineData("--sub-method")]
    public void TryParse_SubMethodClonesFlag_SetsSubMethodClones(string flag)
    {
        var success = CliArgumentParser.TryParse([flag], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.SubMethodClones.Should().BeTrue();
    }

    [Fact]
    public void TryParse_ThresholdSubMethodStatementsOption_SetsValue()
    {
        var success = CliArgumentParser.TryParse(["--threshold-submethod-statements", "15"], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.ThresholdSubMethodStatements.Should().Be(15);
    }

    [Fact]
    public void TryParse_ThresholdSubMethodStatementsInvalidValue_ReturnsError()
    {
        var success = CliArgumentParser.TryParse(["--threshold-submethod-statements", "-5"], out _, out var error);

        success.Should().BeFalse();
        error.Should().Contain("Invalid value for --threshold-submethod-statements");
    }

    [Theory]
    [InlineData("--format", "console", OutputFormat.Console)]
    [InlineData("--format", "sarif", OutputFormat.Sarif)]
    [InlineData("--format", "json", OutputFormat.Json)]
    [InlineData("-f", "CONSOLE", OutputFormat.Console)]
    [InlineData("-f", "SARIF", OutputFormat.Sarif)]
    [InlineData("-f", "JSON", OutputFormat.Json)]
    [InlineData("--format=sarif", null, OutputFormat.Sarif)]
    [InlineData("-f=json", null, OutputFormat.Json)]
    public void TryParse_FormatOption_SetsOutputFormat(string arg1, string? arg2, OutputFormat expectedFormat)
    {
        string[] args = arg2 != null ? [arg1, arg2] : [arg1];
        var success = CliArgumentParser.TryParse(args, out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.Format.Should().Be(expectedFormat);
    }

    [Fact]
    public void TryParse_FormatOptionInvalidValue_ReturnsError()
    {
        var success = CliArgumentParser.TryParse(["--format", "xml"], out _, out var error);

        success.Should().BeFalse();
        error.Should().Be("Invalid value for --format: 'xml'. Allowed values are: console, sarif, json.");
    }

    [Theory]
    [InlineData("--output", "results.sarif")]
    [InlineData("-o", "results.json")]
    [InlineData("--output=out/results.sarif", "out/results.sarif")]
    [InlineData("-o=out/results.json", "out/results.json")]
    public void TryParse_OutputOption_SetsOutputFilePath(string arg1, string? expectedPath)
    {
        string[] args;
        if (arg1.Contains('='))
        {
            args = [arg1];
        }
        else
        {
            args = [arg1, expectedPath!];
        }

        var success = CliArgumentParser.TryParse(args, out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.OutputFilePath.Should().Be(expectedPath);
    }

    [Theory]
    [InlineData("results.sarif", OutputFormat.Sarif)]
    [InlineData("results.SARIF", OutputFormat.Sarif)]
    [InlineData("path/to/report.json", OutputFormat.Json)]
    [InlineData("path/to/report.JSON", OutputFormat.Json)]
    [InlineData("results.txt", OutputFormat.Console)]
    [InlineData("results.log", OutputFormat.Console)]
    public void TryParse_OutputOption_InfersFormatFromExtensionWhenNotExplicitlySet(string filePath, OutputFormat expectedFormat)
    {
        var success = CliArgumentParser.TryParse(["--output", filePath], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.OutputFilePath.Should().Be(filePath);
        options.Format.Should().Be(expectedFormat);
    }

    [Fact]
    public void TryParse_ExplicitFormat_OverridesOutputExtensionInference()
    {
        var success = CliArgumentParser.TryParse(["--format", "console", "--output", "results.sarif"], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.Format.Should().Be(OutputFormat.Console);
        options.OutputFilePath.Should().Be("results.sarif");
    }

    [Fact]
    public void TryParse_HelpText_ContainsFormatAndOutput()
    {
        var help = CliArgumentParser.GetHelpText();

        help.Should().Contain("-f, --format <type>");
        help.Should().Contain("-o, --output <path>");
        help.Should().Contain("--summary <mode>");
        help.Should().Contain("-ct, --comment-tolerance <n>");
    }

    [Theory]
    [InlineData("report", Pmad.CodeCondenser.Core.Configuration.SummaryMode.Report)]
    [InlineData("summarize", Pmad.CodeCondenser.Core.Configuration.SummaryMode.Summarize)]
    [InlineData("disabled", Pmad.CodeCondenser.Core.Configuration.SummaryMode.Disabled)]
    [InlineData("Report", Pmad.CodeCondenser.Core.Configuration.SummaryMode.Report)]
    [InlineData("Disabled", Pmad.CodeCondenser.Core.Configuration.SummaryMode.Disabled)]
    public void TryParse_SummaryFlag_SetsSummaryMode(string modeStr, Pmad.CodeCondenser.Core.Configuration.SummaryMode expected)
    {
        var success = CliArgumentParser.TryParse(["--summary", modeStr], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.SummaryMode.Should().Be(expected);
    }

    [Fact]
    public void TryParse_SummaryModeAlias_SetsSummaryMode()
    {
        var success = CliArgumentParser.TryParse(["--summary-mode=report"], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.SummaryMode.Should().Be(Pmad.CodeCondenser.Core.Configuration.SummaryMode.Report);
    }

    [Fact]
    public void TryParse_ReportOnlyFlag_SetsSummaryModeReport()
    {
        var success = CliArgumentParser.TryParse(["--report-only"], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.SummaryMode.Should().Be(Pmad.CodeCondenser.Core.Configuration.SummaryMode.Report);
    }

    [Fact]
    public void TryParse_NoCommentsFlag_SetsSummaryModeDisabled()
    {
        var success = CliArgumentParser.TryParse(["--no-comments"], out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.SummaryMode.Should().Be(Pmad.CodeCondenser.Core.Configuration.SummaryMode.Disabled);
    }

    [Fact]
    public void TryParse_InvalidSummaryMode_ReturnsError()
    {
        var success = CliArgumentParser.TryParse(["--summary", "unknown_mode"], out var options, out var error);

        success.Should().BeFalse();
        error.Should().Contain("Invalid value for --summary");
    }

    [Theory]
    [InlineData("--comment-tolerance", "0", 0)]
    [InlineData("-ct", "5", 5)]
    [InlineData("--comment-tolerance=3", null, 3)]
    public void TryParse_CommentTolerance_SetsTolerance(string arg1, string? arg2, int expected)
    {
        string[] args = arg2 != null ? [arg1, arg2] : [arg1];
        var success = CliArgumentParser.TryParse(args, out var options, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        options.CommentTolerance.Should().Be(expected);
    }

    [Theory]
    [InlineData("--comment-tolerance", "not-a-number")]
    [InlineData("-ct", "-1")]
    public void TryParse_InvalidCommentTolerance_ReturnsError(string arg1, string arg2)
    {
        var success = CliArgumentParser.TryParse([arg1, arg2], out var options, out var error);

        success.Should().BeFalse();
        error.Should().NotBeNull();
    }
}
