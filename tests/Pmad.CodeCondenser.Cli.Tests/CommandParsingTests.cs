using System.CommandLine;
using AwesomeAssertions;
using Pmad.CodeCondenser.Cli.Commands;
using Pmad.CodeCondenser.Core.Configuration;

namespace Pmad.CodeCondenser.Cli.Tests;

public class CommandParsingTests
{
    private static (RootCommand Root, GlobalOptions Globals) CreateRoot()
    {
        var services = new CommandServices();
        var globals = new GlobalOptions();

        var root = new RootCommand("codecondenser CLI");
        globals.AddTo(root);

        root.Add(ReportCommand.Create(globals, services));
        root.Add(AnalyzeCommand.Create(globals, services));
        root.Add(ApplyCommand.Create(globals, services));
        root.Add(SnapshotCommand.Create(globals, services));
        root.Add(MeasureCommand.Create(globals, services));
        root.Add(SkillsCommand.Create(globals, services));

        var versionOpt = root.Options.OfType<VersionOption>().FirstOrDefault();
        versionOpt?.Aliases.Add("-v");

        return (root, globals);
    }

    [Theory]
    [InlineData("report", CliOptions.CommandReport)]
    [InlineData("analyze", CliOptions.CommandAnalyze)]
    [InlineData("apply", CliOptions.CommandApply)]
    [InlineData("snapshot", CliOptions.CommandSnapshot)]
    [InlineData("measure", CliOptions.CommandMeasure)]
    [InlineData("skills", CliOptions.CommandSkills)]
    public void Parse_CommandVerbs_MatchesExpectedCommand(string verb, string expectedCommand)
    {
        var (root, _) = CreateRoot();
        var parseResult = root.Parse([verb, "./src"]);

        parseResult.Errors.Should().BeEmpty();
        parseResult.CommandResult.Command.Name.Should().Be(expectedCommand);
    }

    [Theory]
    [InlineData("install-skills")]
    [InlineData("install-skill")]
    public void Parse_SkillsAliases_MatchesSkillsCommand(string alias)
    {
        var (root, _) = CreateRoot();
        var parseResult = root.Parse([alias, "./src"]);

        parseResult.Errors.Should().BeEmpty();
        parseResult.CommandResult.Command.Name.Should().Be("skills");
    }

    [Fact]
    public void Parse_SkillsSubcommands_MatchesSubcommands()
    {
        var (root, _) = CreateRoot();

        var installResult = root.Parse(["skills", "install", "./src"]);
        installResult.Errors.Should().BeEmpty();
        installResult.CommandResult.Command.Name.Should().Be("install");

        var updateResult = root.Parse(["skills", "update", "./src"]);
        updateResult.Errors.Should().BeEmpty();
        updateResult.CommandResult.Command.Name.Should().Be("update");
    }

    [Fact]
    public void Parse_AnalyzeWithTargetPath_BindsCorrectly()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "src/MyApp"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, "src/MyApp");

        options.Command.Should().Be(CliOptions.CommandAnalyze);
        options.TargetPath.Should().Be("src/MyApp");
    }

    [Fact]
    public void Parse_ReportCommand_BindsDefaults()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["report"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandReport, ".");

        options.Command.Should().Be(CliOptions.CommandReport);
        options.TargetPath.Should().Be(".");
    }

    [Fact]
    public void Parse_IncrementalFlag_SetsIncrementalTrue()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "-i"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.Incremental.Should().BeTrue();
    }

    [Fact]
    public void Parse_ThresholdOptions_BindsIntegerValues()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "--threshold-statements", "12", "-tc", "150"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.ThresholdStatements.Should().Be(12);
        options.ThresholdComments.Should().Be(150);
    }

    [Fact]
    public void Parse_XmlThresholdOptions_BindsValues()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "--threshold-xml-summary", "80", "--threshold-xml-param", "40"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.ThresholdXmlSummary.Should().Be(80);
        options.ThresholdXmlParam.Should().Be(40);
    }

    [Fact]
    public void Parse_RepeatedStringThresholds_BindsValues()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "--threshold-strings", "80", "--string-occurrences", "3", "--threshold-existing-constants", "25"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.ThresholdRepeatedStrings.Should().Be(80);
        options.ThresholdStringOccurrences.Should().Be(3);
        options.ThresholdExistingConstants.Should().Be(25);
    }

    [Fact]
    public void Parse_ThresholdExistingConstantsShortAlias_BindsValues()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "-tec", "15"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.ThresholdExistingConstants.Should().Be(15);
    }

    [Fact]
    public void Parse_SubMethodOptions_BindsValues()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "--sub-method-clones", "--threshold-submethod-statements", "15"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.SubMethodClones.Should().BeTrue();
        options.ThresholdSubMethodStatements.Should().Be(15);
    }

    [Fact]
    public void Parse_LlmOptions_BindsValues()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "--model", "gemma4:e4b", "--endpoint", "http://localhost:11434", "-cs", "24576", "-mr", "2", "-te", "gemma"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.Model.Should().Be("gemma4:e4b");
        options.Endpoint.Should().Be("http://localhost:11434");
        options.ContextSize.Should().Be(24576);
        options.MaxSummarizationRetries.Should().Be(2);
        options.TokenEstimator.Should().Be("gemma");
    }

    [Fact]
    public void Parse_ConfigPaths_BindsValues()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "--config", "custom.json", "--global-config", "global.json", "--no-global-config"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.ConfigPath.Should().Be("custom.json");
        options.GlobalConfigPath.Should().Be("global.json");
        options.NoGlobalConfig.Should().BeTrue();
    }

    [Fact]
    public void Parse_FormatAndOutputOptions_BindsValues()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "--format", "json", "-o", "report.json", "--no-color"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.Format.Should().Be(OutputFormat.Json);
        options.OutputFilePath.Should().Be("report.json");
        options.NoColor.Should().BeTrue();
    }

    [Fact]
    public void Parse_IncludeAndExcludeGlobs_BindsLists()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "--include", "src/**/*.cs", "--include", "lib/**/*.cs", "--exclude", "**/obj/**"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.Include.Should().Contain("src/**/*.cs");
        options.Include.Should().Contain("lib/**/*.cs");
        options.Exclude.Should().Contain("**/obj/**");
    }

    [Fact]
    public void Parse_Tolerances_BindsValues()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "-dt", "5", "-ct", "10", "-st", "2", "--enforce-test-duplication"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.DuplicationTolerance.Should().Be(5);
        options.CommentTolerance.Should().Be(10);
        options.RepeatedStringTolerance.Should().Be(2);
        options.EnforceTestDuplication.Should().BeTrue();
    }

    [Fact]
    public void Parse_ExclusionsAndGenerated_BindsValues()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "--exclude-tests", "--include-generated", "--dry-run"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.ExcludeTests.Should().BeTrue();
        options.IncludeGenerated.Should().BeTrue();
        options.DryRun.Should().BeTrue();
    }

    [Fact]
    public void Parse_DisableEnableFlags_BindsCorrectly()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "--no-duplication", "--no-strings", "--no-comments"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.EnableDuplicationDetection.Should().BeFalse();
        options.EnableRepeatedStrings.Should().BeFalse();
        options.EnableCommentSummarization.Should().BeFalse();
        options.SummaryMode.Should().Be(SummaryMode.Disabled);
    }

    [Fact]
    public void Parse_EnableFlags_BindsCorrectly()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "--enable-duplication", "--enable-strings", "--enable-comments"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.EnableDuplicationDetection.Should().BeTrue();
        options.EnableRepeatedStrings.Should().BeTrue();
        options.EnableCommentSummarization.Should().BeTrue();
        options.SummaryMode.Should().Be(SummaryMode.Summarize);
    }

    [Fact]
    public void Parse_VersionAliases_ContainsDashV()
    {
        var (root, _) = CreateRoot();
        var versionOpt = root.Options.OfType<VersionOption>().FirstOrDefault();

        versionOpt.Should().NotBeNull();
        versionOpt!.Aliases.Should().Contain("-v");
    }

    [Fact]
    public void Parse_LinesOfCodeFlags_BindsCorrectly()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "--no-loc", "--threshold-method-lines", "200", "--threshold-class-lines", "2000", "--loc-tolerance", "5"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.EnableLinesOfCodeDetection.Should().BeFalse();
        options.MethodLinesOfCodeThreshold.Should().Be(200);
        options.ClassLinesOfCodeThreshold.Should().Be(2000);
        options.LinesOfCodeTolerance.Should().Be(5);
    }

    [Fact]
    public void Parse_LinesOfCodeAliases_BindsCorrectly()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "--enable-loc", "-tml", "120", "-tcl", "1200", "-lt", "3"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.EnableLinesOfCodeDetection.Should().BeTrue();
        options.MethodLinesOfCodeThreshold.Should().Be(120);
        options.ClassLinesOfCodeThreshold.Should().Be(1200);
        options.LinesOfCodeTolerance.Should().Be(3);
    }

    [Fact]
    public void Parse_PredicateFlags_BindsCorrectly()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "--no-predicates", "--threshold-predicates", "30", "--threshold-existing-helpers", "15", "--predicate-occurrences", "3", "--predicate-tolerance", "0"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.EnablePredicateCloneDetection.Should().BeFalse();
        options.PredicateLengthThreshold.Should().Be(30);
        options.ExistingHelperPredicateLengthThreshold.Should().Be(15);
        options.PredicateMinOccurrences.Should().Be(3);
        options.PredicateTolerance.Should().Be(0);
    }

    [Fact]
    public void Parse_PredicateAliases_BindsCorrectly()
    {
        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze", "--enable-predicates", "-tpred", "25", "-thelp", "20", "-po", "2", "-pt", "1"]);

        parseResult.Errors.Should().BeEmpty();
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");

        options.EnablePredicateCloneDetection.Should().BeTrue();
        options.PredicateLengthThreshold.Should().Be(25);
        options.ExistingHelperPredicateLengthThreshold.Should().Be(20);
        options.PredicateMinOccurrences.Should().Be(2);
        options.PredicateTolerance.Should().Be(1);
    }

    [Fact]
    public void Parse_PredicateDefaults_UsesExpectedDefaultSettings()
    {
        var settings = new Pmad.CodeCondenser.Core.Configuration.CodeCondenserSettings();
        settings.PredicateLengthThreshold.Should().Be(100);
        settings.ExistingHelperPredicateLengthThreshold.Should().Be(20);
        settings.PredicateMinOccurrences.Should().Be(2);

        var (root, globals) = CreateRoot();
        var parseResult = root.Parse(["analyze"]);
        var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, ".");
        options.PredicateLengthThreshold.Should().BeNull();
        options.ExistingHelperPredicateLengthThreshold.Should().BeNull();
    }
}
