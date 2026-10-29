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

    [Theory]
    [InlineData(new string[] { "--no-comments" }, "Applying single-type file splits and condensation patterns to target:")]
    [InlineData(new string[] { "--no-comments", "--no-type-split" }, "Applying condensation patterns to target:")]
    [InlineData(new string[] { "--no-patterns", "--no-comments" }, "Applying single-type file splits to target:")]
    [InlineData(new string[] { }, "Applying single-type file splits, condensation patterns, and comment summaries to target:")]
    public async Task Apply_IntroBanner_MentionsConfiguredTransformations(string[] extraArgs, string expectedBanner)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ApplyBanner_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var args = new List<string> { "apply", tempDir, "--no-color" };
            args.AddRange(extraArgs);

            var exitCode = await Program.RunAsync([.. args], output, error);

            exitCode.Should().Be(0);
            output.ToString().Should().Contain(expectedBanner);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Apply_WithPatternsAndTypeSplit_ModernizesPatternsInSplitTypes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ApplyPatternSplit_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var csprojContent = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """;
            await File.WriteAllTextAsync(Path.Combine(tempDir, "TestApp.csproj"), csprojContent);

            var multiTypeContent = """
                namespace TestNamespace;

                public class ClassA
                {
                    public void Run()
                    {
                    }
                }

                public class ClassB
                {
                    public void Validate(string name)
                    {
                        if (name == null)
                            throw new System.ArgumentNullException(nameof(name));
                    }
                }
                """;
            var combinedPath = Path.Combine(tempDir, "ClassA.cs");
            await File.WriteAllTextAsync(combinedPath, multiTypeContent);

            using var output = new StringWriter();
            using var error = new StringWriter();
            var exitCode = await Program.RunAsync(["apply", tempDir, "--no-color", "--no-comments"], output, error);

            exitCode.Should().Be(0);

            var classBPath = Path.Combine(tempDir, "ClassB.cs");
            File.Exists(classBPath).Should().BeTrue();
            var classBText = await File.ReadAllTextAsync(classBPath);
            classBText.Should().Contain("ArgumentNullException.ThrowIfNull(name);");
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

