using AwesomeAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Cli.Commands;
using Pmad.CodeCondenser.Cli.Formatting;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;
using Pmad.CodeCondenser.Core.Models;
using Xunit;

namespace Pmad.CodeCondenser.Cli.Tests;

public class PatternsCliTests
{
    private readonly ConsoleFormatter _formatter = new();

    [Fact]
    public void PrintCondensationCandidates_OutputsExpectedDetails()
    {
        using var sw = new StringWriter();
        var candidates = new List<CondensationCandidate>
        {
            new()
            {
                RuleId = "PCC401",
                RuleName = "Use ReplaceLineEndings",
                Category = PatternCategory.Text,
                FilePath = "src/File.cs",
                StartLine = 12,
                EndLine = 12,
                StartColumn = 15,
                EndColumn = 60,
                OriginalCode = "s.Replace(\"\\r\\n\", \"\\n\").Replace(\"\\r\", \"\\n\")",
                SuggestedCode = "s.ReplaceLineEndings(\"\\n\")",
                CanAutoFix = true
            }
        };

        _formatter.PrintCondensationCandidates(sw, candidates, baseDirectory: "src");
        var output = sw.ToString();

        output.Should().Contain("Found 1 Modern Framework Condensation Candidate(s)");
        output.Should().Contain("[CANDIDATE #1] [PCC401] Use ReplaceLineEndings");
        output.Should().Contain("s.ReplaceLineEndings(\"\\n\")");
        output.Should().Contain("Auto-fix:    Supported");
    }

    [Fact]
    public void DisplayedItemLimiter_LimitsCondensationCandidates()
    {
        var candidates = new List<CondensationCandidate>
        {
            new()
            {
                RuleId = "PCC401",
                RuleName = "Rule 1",
                Category = PatternCategory.Text,
                FilePath = "A.cs",
                StartLine = 1,
                EndLine = 1,
                StartColumn = 1,
                EndColumn = 20,
                OriginalCode = "long_legacy_expression_1234567890",
                SuggestedCode = "short",
                CanAutoFix = true
            },
            new()
            {
                RuleId = "PCC402",
                RuleName = "Rule 2",
                Category = PatternCategory.Predicate,
                FilePath = "B.cs",
                StartLine = 2,
                EndLine = 2,
                StartColumn = 1,
                EndColumn = 20,
                OriginalCode = "tiny",
                SuggestedCode = "t",
                CanAutoFix = true
            }
        };

        var result = new AnalysisResult
        {
            VerboseComments = [],
            CloneGroups = [],
            SummarizedComments = new Dictionary<string, string>(),
            AnalyzedFilesCount = 2,
            TotalMethodsScanned = 2,
            CondensationCandidates = candidates
        };

        var settings = new CodeCondenserSettings
        {
            EnablePatternCondensation = true,
            Limit = 1
        };

        var limited = DisplayedItemLimiter.SelectItems(result, settings, baseDirectory: ".");
        limited.IsLimited.Should().BeTrue();
        limited.DisplayedItemCount.Should().Be(1);
        limited.CondensationCandidates.Should().HaveCount(1);
        limited.CondensationCandidates[0].RuleId.Should().Be("PCC401"); // Larger benefit candidate
    }

    [Fact]
    public void GlobalOptions_BindsPatternOptions()
    {
        var globals = new GlobalOptions();
        var root = new System.CommandLine.RootCommand("codecondenser CLI");
        globals.AddTo(root);
        root.Add(AnalyzeCommand.Create(globals, new CommandServices()));

        var result = root.Parse(["analyze", ".", "--no-patterns", "--patterns-tfm", "net8.0", "--pattern-rules", "PCC401,PCC402"]);
        var options = globals.BindCliOptions(result, "analyze", ".");

        options.EnablePatternCondensation.Should().Be(false);
        options.TargetDotNetVersion.Should().Be("net8.0");
        options.CondensationRules.Should().NotBeNull();
        options.CondensationRules.Should().ContainInOrder(["PCC401", "PCC402"]);
    }
}
