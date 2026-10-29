using AwesomeAssertions;
using Pmad.CodeCondenser.Cli.Formatting;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Cli.Tests;

public class ConsoleFormatterTests
{
    [Fact]
    public void PrintBanner_OutputsBanner()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        formatter.PrintBanner(writer);

        var output = writer.ToString();
        output.Should().Contain("=== Pmad.CodeCondenser ===");
        output.Should().Contain("Static analysis tool");
    }

    [Fact]
    public void PrintCloneGroups_WhenEmpty_PrintsNoDuplication()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        formatter.PrintCloneGroups(writer, [], ".");

        writer.ToString().Should().Contain("No structural code duplication detected.");
    }

    [Fact]
    public void PrintCloneGroups_WithClones_PrintsClusterDetails()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var methods = new List<MethodCloneInfo>
        {
            new("src/Service1.cs", "Process", 10, 20, 8, "hash1234567890", "void Process()"),
            new("src/Service2.cs", "Execute", 15, 25, 8, "hash1234567890", "void Execute()")
        };
        var group = new CodeCloneGroup("hash1234567890", methods);

        formatter.PrintCloneGroups(writer, [group], ".");

        var output = writer.ToString();
        output.Should().Contain("Found 1 Production Duplication Cluster(s) (Highest Impact):");
        output.Should().Contain("[PRODUCTION CLUSTER #1]");
        output.Should().Contain("src/Service1.cs:10-20 in Process (8 statements)");
        output.Should().Contain("src/Service2.cs:15-25 in Execute (8 statements)");
        output.Should().Contain("Recommendation: Extract duplicate structural logic");
        output.Should().Contain("To suppress: Add '[SuppressMessage(\"CodeCondenser\", \"Duplication\", Justification = \"<why>\")]' on method.");
    }

    [Fact]
    public void PrintCloneGroups_WithTestClones_PrintsTestClusterDetails()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var methods = new List<MethodCloneInfo>
        {
            new("tests/Service1Tests.cs", "Test1", 10, 20, 8, "hashTest123", "void Test1()"),
            new("tests/Service2Tests.cs", "Test2", 15, 25, 8, "hashTest123", "void Test2()")
        };
        var group = new CodeCloneGroup("hashTest123", methods, CloneCategory.Test, ImpactScore: 8);

        formatter.PrintCloneGroups(writer, [group], ".");

        var output = writer.ToString();
        output.Should().Contain("Found 1 Test Duplication Cluster(s) (Review for DAMP vs DRY):");
        output.Should().Contain("[TEST CLUSTER #1]");
        output.Should().Contain("Recommendation: Consider parameterized tests ([Theory] + [InlineData])");
        output.Should().Contain("To suppress: Add '[SuppressMessage(\"CodeCondenser\", \"Duplication\", Justification = \"<why>\")]' on method.");
    }

    [Fact]
    public void PrintVerboseComments_WhenEmpty_PrintsNoVerboseComments()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        formatter.PrintVerboseComments(writer, [], new Dictionary<string, string>(), ".");

        writer.ToString().Should().Contain("No verbose comments detected.");
    }

    [Fact]
    public void PrintVerboseComments_WithCommentsAndSummaries_PrintsDiff()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var comment = new VerboseComment(
            FilePath: "src/Foo.cs",
            StartLine: 5,
            EndLine: 7,
            SpanStart: 0,
            SpanLength: 120,
            Kind: CommentKind.SingleLine,
            OriginalText: "// " + new string('x', 115),
            LeadingWhitespace: "    ");

        var summaries = new Dictionary<string, string>
        {
            [comment.ContentHash] = "// Concise summary"
        };

        formatter.PrintVerboseComments(writer, [comment], summaries, ".");

        var output = writer.ToString();
        output.Should().Contain("Found 1 verbose comment(s) exceeding threshold:");
        output.Should().Contain("[VERBOSE COMMENT #1] src/Foo.cs:5-7 [SingleLine]");
        output.Should().Contain("- Original:");
        output.Should().Contain("+ Condensed: // Concise summary");
        output.Should().Contain("reduction");
        output.Should().Contain("Total Gain:");
    }

    [Fact]
    public void PrintVerboseComments_WhenSummaryIsKeptOldComment_PrintsKeptAndZeroReduction()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var comment = new VerboseComment(
            FilePath: "src/Foo.cs",
            StartLine: 5,
            EndLine: 7,
            SpanStart: 0,
            SpanLength: 120,
            Kind: CommentKind.SingleLine,
            OriginalText: "// Original comment text exceeding length threshold",
            LeadingWhitespace: "    ");

        var summaries = new Dictionary<string, string>
        {
            [comment.ContentHash] = "// Original comment text exceeding length threshold"
        };

        formatter.PrintVerboseComments(writer, [comment], summaries, ".");

        var output = writer.ToString();
        output.Should().Contain("= Kept:      // Original comment text exceeding length threshold");
        output.Should().Contain("0% reduction, kept original comment");
        output.Should().Contain("Total Gain: 0 chars, ~0 tokens (0% reduction");
    }

    [Fact]
    public void PrintSummary_OutputsCorrectCounts()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        formatter.PrintSummary(writer, filesAnalyzed: 5, methodsScanned: 12, cloneGroupsCount: 2, verboseCommentsCount: 3, mode: "Dry-Run");

        var output = writer.ToString();
        output.Should().Contain("Mode:              Dry-Run");
        output.Should().Contain("Files Analyzed:    5");
        output.Should().Contain("Methods Scanned:   12");
        output.Should().Contain("Clone Clusters:    2");
        output.Should().Contain("Verbose Comments:  3");
        output.Should().NotContain("Comment Gain:");
    }

    [Fact]
    public void PrintSummary_WithCommentGain_OutputsCommentGain()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        formatter.PrintSummary(
            writer,
            filesAnalyzed: 5,
            methodsScanned: 12,
            cloneGroupsCount: 2,
            verboseCommentsCount: 3,
            mode: "Dry-Run",
            commentGainChars: 150,
            commentReductionPercent: 45);

        var output = writer.ToString();
        output.Should().Contain("Comment Gain:      +150 chars (45% reduction)");
    }

    [Fact]
    public void PrintSummary_WithCommentGainAndTokens_OutputsCommentGainWithTokens()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        formatter.PrintSummary(
            writer,
            filesAnalyzed: 5,
            methodsScanned: 12,
            cloneGroupsCount: 2,
            verboseCommentsCount: 3,
            mode: "Dry-Run",
            commentGainChars: 150,
            commentReductionPercent: 45,
            commentGainTokens: 38);

        var output = writer.ToString();
        output.Should().Contain("Comment Gain:      +150 chars, ~38 tokens (45% reduction)");
    }

    [Fact]
    public void PrintSummary_WithCloneGainTokens_OutputsCloneGain()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        formatter.PrintSummary(
            writer,
            filesAnalyzed: 5,
            methodsScanned: 12,
            cloneGroupsCount: 2,
            verboseCommentsCount: 3,
            mode: "Dry-Run",
            cloneGainTokens: 250);

        var output = writer.ToString();
        output.Should().Contain("Clone Gain:        ~250 tokens (potential)");
    }

    [Fact]
    public void PrintSummary_WithModernizations_OutputsModernizationsAndGain()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        formatter.PrintSummary(
            writer,
            filesAnalyzed: 5,
            methodsScanned: 12,
            cloneGroupsCount: 0,
            verboseCommentsCount: 0,
            mode: "Dry-Run",
            modernizationsCount: 4,
            modernizationGainChars: 120,
            modernizationGainTokens: 30);

        var output = writer.ToString();
        output.Should().Contain("Modernizations:    4");
        output.Should().Contain("Modernization Gain: +120 chars, ~30 tokens");
    }

    [Fact]
    public void PrintSummary_WhenModernizationsZero_OutputsZeroAndNoGain()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        formatter.PrintSummary(
            writer,
            filesAnalyzed: 5,
            methodsScanned: 12,
            cloneGroupsCount: 0,
            verboseCommentsCount: 0,
            mode: "Dry-Run",
            modernizationsCount: 0);

        var output = writer.ToString();
        output.Should().Contain("Modernizations:    0");
        output.Should().NotContain("Modernization Gain:");
    }

    [Fact]
    public void PrintSummary_WhenModernizationsNotAnalyzed_DoesNotPrintModernizationsOrGain()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        formatter.PrintSummary(
            writer,
            filesAnalyzed: 5,
            methodsScanned: 12,
            cloneGroupsCount: 0,
            verboseCommentsCount: 0,
            mode: "Dry-Run",
            modernizationsCount: null);

        var output = writer.ToString();
        output.Should().NotContain("Modernizations:");
        output.Should().NotContain("Modernization Gain:");
    }

    [Fact]
    public void PrintCloneGroups_WithEstimatorAndBaseDirectory_OutputsPotentialGain()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CFTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "A.cs");
            File.WriteAllLines(filePath, Enumerable.Range(1, 10).Select(i => $"    var x{i} = Compute({i});"));

            var methods = new List<MethodCloneInfo>
            {
                new("A.cs", "M1", 1, 10, 10, "hashA", "void M1()"),
                new("A.cs", "M2", 1, 10, 10, "hashA", "void M2()")
            };
            var group = new CodeCloneGroup("hashA", methods, CloneCategory.Production, ImpactScore: 10);

            var formatter = new ConsoleFormatter(enableColor: false);
            using var writer = new StringWriter();
            formatter.PrintCloneGroups(writer, [group], tempDir, Pmad.AI.Tokens.HeuristicTokenEstimator.Default);

            var output = writer.ToString();
            output.Should().Contain("Potential Gain: ~");
            output.Should().Contain("tokens");
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
    public void PrintSubMethodCloneGroups_WithEstimatorAndBaseDirectory_OutputsPotentialGain()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CFTests_Sub_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "B.cs");
            File.WriteAllLines(filePath, Enumerable.Range(1, 10).Select(i => $"    var y{i} = Compute({i});"));

            var occurrences = new List<SubMethodCloneOccurrence>
            {
                new("B.cs", "M1", 1, 5, 5),
                new("B.cs", "M2", 1, 5, 5)
            };
            var group = new SubMethodCloneGroup("subHash", occurrences, 5, CloneCategory.Production, ImpactScore: 5);

            var formatter = new ConsoleFormatter(enableColor: false);
            using var writer = new StringWriter();
            formatter.PrintSubMethodCloneGroups(writer, [group], tempDir, Pmad.AI.Tokens.HeuristicTokenEstimator.Default);

            var output = writer.ToString();
            output.Should().Contain("Potential Gain: ~");
            output.Should().Contain("tokens");
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
    public void PrintStatusMessages_OutputsCorrectly()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        formatter.PrintFileApplied(writer, "src/Foo.cs", ".");
        formatter.PrintSnapshotSaved(writer, ".codecondenser/snapshot.json");
        formatter.PrintError(writer, "Sample error");
        formatter.PrintSuccess(writer, "Sample success");
        formatter.PrintWarning(writer, "Sample warning");
        formatter.PrintInfo(writer, "Sample info");

        var output = writer.ToString();
        output.Should().Contain("Applied: src/Foo.cs");
        output.Should().Contain("Snapshot baseline saved: .codecondenser/snapshot.json");
        output.Should().Contain("[Error] Sample error");
        output.Should().Contain("[Success] Sample success");
        output.Should().Contain("[Warning] Sample warning");
        output.Should().Contain("[Info] Sample info");
    }

    [Fact]
    public void PrintCommentProgress_OutputsProgressLine()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        formatter.PrintCommentProgress(writer, 1, 4);
        formatter.PrintCommentProgress(writer, 4, 4);

        var output = writer.ToString();
        output.Should().Contain("Summarizing comments: 1/4 (25%)");
        output.Should().Contain("Summarizing comments: 4/4 (100%)");
    }

    [Fact]
    public void PrintAiActionableDuplicationFeedback_OutputsStructuredGuidance()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var methods = new List<MethodCloneInfo>
        {
            new("src/ServiceA.cs", "ProcessA", 10, 25, 12, "hashXYZ", "void ProcessA(int x)"),
            new("src/ServiceB.cs", "ProcessB", 30, 45, 12, "hashXYZ", "void ProcessB(int y)")
        };
        var cloneGroup = new CodeCloneGroup("hashXYZ", methods);

        formatter.PrintAiActionableDuplicationFeedback(writer, [cloneGroup], ".", tolerance: 0);

        var output = writer.ToString();
        output.Should().Contain("[AI AGENT ACTION REQUIRED]");
        output.Should().Contain("Code Duplication Policy Violation (Exit Code: 2)");
        output.Should().Contain("exceeding tolerance margin (allowed: 0)");
        output.Should().Contain("src/ServiceA.cs:10-25");
        output.Should().Contain("src/ServiceB.cs:30-45");
        output.Should().Contain("Actionable Instructions for AI Agent:");
        output.Should().Contain("Extract the identical structural logic into a shared helper method");
    }

    [Fact]
    public void PrintCloneGroups_WithComplementaryPair_PrintsAdjustedRecommendation()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var methods = new List<MethodCloneInfo>
        {
            new("src/Connection.cs", "JoinTaskGroupAsync", 10, 20, 8, "hashJoinLeave", "Task JoinTaskGroupAsync()"),
            new("src/Connection.cs", "LeaveTaskGroupAsync", 25, 35, 8, "hashJoinLeave", "Task LeaveTaskGroupAsync()")
        };
        var group = new CodeCloneGroup("hashJoinLeave", methods, CloneCategory.Production, ImpactScore: 8, IsComplementaryPair: true);

        formatter.PrintCloneGroups(writer, [group], ".");

        var output = writer.ToString();
        output.Should().Contain("Symmetric opposite methods. Extracting a shared helper may impair readability; evaluate whether keeping explicit paired methods is preferable.");
    }

    [Fact]
    public void PrintAiActionableDuplicationFeedback_UsesGroupStatementCount_EvenWhenFirstMethodHasZeroStatements()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var methods = new List<MethodCloneInfo>
        {
            // First method restored from baseline has 0 statements
            new("src/ServiceA.cs", "DoWork", 1, 1, 0, "hash1", "void DoWork()"),
            // Second method scanned has 15 statements
            new("src/ServiceB.cs", "DoWork", 10, 25, 15, "hash1", "void DoWork()")
        };
        var group = new CodeCloneGroup("hash1", methods, CloneCategory.Production, ImpactScore: 15);

        formatter.PrintAiActionableDuplicationFeedback(writer, [group], ".", tolerance: 0);

        var output = writer.ToString();
        output.Should().Contain("15 statements");
        output.Should().NotContain("0 statements");
    }

    [Fact]
    public void PrintSubMethodCloneGroups_WhenEmpty_PrintsNothing()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        formatter.PrintSubMethodCloneGroups(writer, [], ".");

        writer.ToString().Should().BeEmpty();
    }

    [Fact]
    public void PrintSubMethodCloneGroups_WithClones_PrintsClusterDetails()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var occurrences = new List<SubMethodCloneOccurrence>
        {
            new("src/Service1.cs", "Process", 10, 20, 10, 0, "scope1", "Project1"),
            new("src/Service2.cs", "Execute", 15, 25, 10, 0, "scope2", "Project1")
        };
        var group = new SubMethodCloneGroup("hash123", occurrences, 10, CloneCategory.Production, 10);

        formatter.PrintSubMethodCloneGroups(writer, [group], ".");

        var output = writer.ToString();
        output.Should().Contain("Found 1 Sub-Method Duplication Cluster(s):");
        output.Should().Contain("[SUB-METHOD DUPLICATION CLUSTER #1]");
        output.Should().Contain("src/Service1.cs:10-20 in Process");
        output.Should().Contain("src/Service2.cs:15-25 in Execute");
        output.Should().Contain("Recommendation: Extract duplicate 10-statement block into a shared helper method.");
    }

    [Fact]
    public void PrintVerboseComments_WithoutSummaries_PrintsRecommendationAndSuppression()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var xmlComment = new VerboseComment(
            FilePath: "src/Foo.cs",
            StartLine: 1,
            EndLine: 5,
            SpanStart: 0,
            SpanLength: 150,
            Kind: CommentKind.XmlDocumentation,
            OriginalText: "/// <summary>Very long xml doc exceeding threshold characters.</summary>",
            LeadingWhitespace: "");

        var regularComment = new VerboseComment(
            FilePath: "src/Bar.cs",
            StartLine: 10,
            EndLine: 12,
            SpanStart: 200,
            SpanLength: 130,
            Kind: CommentKind.SingleLine,
            OriginalText: "// Very long regular comment exceeding threshold characters.",
            LeadingWhitespace: "");

        formatter.PrintVerboseComments(writer, [xmlComment, regularComment], new Dictionary<string, string>(), ".");

        var output = writer.ToString();
        output.Should().Contain("Recommendation: Condense verbose comment to be concise (max 120 columns)");
        output.Should().Contain("To suppress: Add condense=\"no\" to XML doc tag (e.g. <summary condense=\"no\">).");
        output.Should().Contain("To suppress: Add '// codecondenser:ignore' or '// nocondense' on preceding line or inline.");
    }

    [Fact]
    public void PrintAiActionableDuplicationFeedback_IncludesSuppressionInstruction()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var methods = new List<MethodCloneInfo>
        {
            new("src/Svc1.cs", "M1", 10, 20, 8, "hashA", "void M1()"),
            new("src/Svc2.cs", "M2", 10, 20, 8, "hashA", "void M2()")
        };
        var group = new CodeCloneGroup("hashA", methods);

        formatter.PrintAiActionableDuplicationFeedback(writer, [group], ".", tolerance: 0);

        var output = writer.ToString();
        output.Should().Contain("To suppress: Add '[SuppressMessage(\"CodeCondenser\", \"Duplication\", Justification = \"<why>\")]' or '// codecondenser:ignore-duplicate' on the method.");
    }

    [Fact]
    public void PrintAiActionableIncrementalFeedback_CommentsOnly_PrintsActionableBlockWithSuppression()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var comment = new VerboseComment(
            FilePath: "src/Service.cs",
            StartLine: 15,
            EndLine: 18,
            SpanStart: 50,
            SpanLength: 140,
            Kind: CommentKind.XmlDocumentation,
            OriginalText: "/// <summary>Exceeding text here</summary>",
            LeadingWhitespace: "");

        formatter.PrintAiActionableIncrementalFeedback(
            writer,
            cloneGroups: [],
            subMethodGroups: [],
            verboseComments: [comment],
            baseDirectory: ".",
            duplicationTolerance: 0,
            hasDuplicationViolation: false,
            hasCommentViolation: true);

        var output = writer.ToString();
        output.Should().Contain("[AI AGENT ACTION REQUIRED] Verbose Comments Policy Violation (Exit Code: 2)");
        output.Should().Contain("Incremental analysis detected 1 new verbose comment(s) exceeding length thresholds.");
        output.Should().Contain("src/Service.cs:15-18");
        output.Should().Contain("Actionable Instructions for AI Agent:");
        output.Should().Contain("Condense: Shorten or rewrite the comment text concisely");
        output.Should().Contain("XML doc comments: Add 'condense=\"no\"' attribute to XML tag");
        output.Should().Contain("Regular comments: Add '// codecondenser:ignore' or '// nocondense'");
    }

    [Fact]
    public void PrintAiActionableIncrementalFeedback_BothDuplicationAndComments_PrintsUnifiedActionableBlockOnce()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var methods = new List<MethodCloneInfo>
        {
            new("src/S1.cs", "M1", 1, 10, 5, "hash1", "void M1()"),
            new("src/S2.cs", "M2", 1, 10, 5, "hash1", "void M2()")
        };
        var group = new CodeCloneGroup("hash1", methods);

        var comment = new VerboseComment(
            FilePath: "src/S3.cs",
            StartLine: 20,
            EndLine: 22,
            SpanStart: 100,
            SpanLength: 130,
            Kind: CommentKind.SingleLine,
            OriginalText: "// Very long text exceeding threshold",
            LeadingWhitespace: "");

        formatter.PrintAiActionableIncrementalFeedback(
            writer,
            cloneGroups: [group],
            subMethodGroups: [],
            verboseComments: [comment],
            baseDirectory: ".",
            duplicationTolerance: 0,
            hasDuplicationViolation: true,
            hasCommentViolation: true);

        var output = writer.ToString();
        // Displays unified header only once
        output.Should().Contain("[AI AGENT ACTION REQUIRED] Incremental Policy Violations (Exit Code: 2)");
        output.Should().Contain("Incremental analysis detected 1 new duplication cluster(s) and 1 new verbose comment(s).");
        output.Should().Contain("Code Duplication Clusters:");
        output.Should().Contain("Verbose Comments:");
        output.Should().Contain("Actionable Instructions for AI Agent:");
        output.Should().Contain("Code Duplication:");
        output.Should().Contain("To suppress: Add '[SuppressMessage(\"CodeCondenser\", \"Duplication\", Justification = \"<why>\")]' or '// codecondenser:ignore-duplicate'");
        output.Should().Contain("Verbose Comments:");
        output.Should().Contain("To suppress: Add 'condense=\"no\"' attribute (XML doc) or '// codecondenser:ignore'");
    }

    [Fact]
    public void PrintMeasurementResult_OutputsFormattedTable()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var result = new CodeMeasurementResult
        {
            Production = new CategoryMeasurement { FilesCount = 10, CodeTokens = 15000, CommentTokens = 3000, TotalTokens = 18000 },
            Tests = new CategoryMeasurement { FilesCount = 5, CodeTokens = 8000, CommentTokens = 500, TotalTokens = 8500 },
            Total = new CategoryMeasurement { FilesCount = 15, CodeTokens = 23000, CommentTokens = 3500, TotalTokens = 26500 },
            Files =
            [
                new FileMeasurement("src/App.cs", FileCategory.Production, 15000, 3000, 18000),
                new FileMeasurement("tests/AppTests.cs", FileCategory.Test, 8000, 500, 8500)
            ],
            SkippedGeneratedFilesCount = 2
        };

        formatter.PrintMeasurementResult(writer, result, "C:\\MyProject", "qwen");

        var output = writer.ToString();
        output.Should().Contain("Estimator: qwen");
        output.Should().Contain("Category");
        output.Should().Contain("Code Tokens");
        output.Should().Contain("Comment Tokens");
        output.Should().Contain("Total");
        output.Should().Contain("Production");
        output.Should().Contain("15,000");
        output.Should().Contain("3,000");
        output.Should().Contain("18,000");
        output.Should().Contain("Tests");
        output.Should().Contain("8,000");
        output.Should().Contain("500");
        output.Should().Contain("8,500");
        output.Should().Contain("23,000");
        output.Should().Contain("3,500");
        output.Should().Contain("26,500");
    }

    [Fact]
    public void PrintRepeatedStringGroups_OutputsFormattedSnippetAndUsage()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var longStr = "SELECT CustomerId, OrderId, TotalAmount FROM Orders WHERE Status = 'Pending'";
        var group = new RepeatedStringGroup
        {
            Value = longStr,
            Occurrences =
            [
                new()
                {
                    FilePath = "src/Services/OrderService.cs",
                    StartLine = 15,
                    EndLine = 15,
                    StartColumn = 20,
                    EndColumn = 20 + longStr.Length,
                    StringValue = longStr,
                    EnclosingMember = "GetPendingOrders",
                    Category = FileCategory.Production
                },
                new()
                {
                    FilePath = "src/Repositories/OrderRepo.cs",
                    StartLine = 45,
                    EndLine = 45,
                    StartColumn = 25,
                    EndColumn = 25 + longStr.Length,
                    StringValue = longStr,
                    EnclosingMember = "QueryOrders",
                    Category = FileCategory.Production
                }
            ],
            Category = CloneCategory.Production,
            SuggestedConstantName = "PendingOrdersQuery",
            SuggestedClassName = "Constants",
            TargetNamespace = "ApplicationNamespace"
        };

        formatter.PrintRepeatedStringGroups(writer, [group], ".");

        var output = writer.ToString();
        output.Should().Contain("Found 1 Repeated Inline String Cluster(s)");
        output.Should().Contain($"[PRODUCTION REPEATED STRING #1] ({longStr.Length} chars, 2 occurrences");
        output.Should().Contain("OrderService.cs:15:20 in GetPendingOrders");
        output.Should().Contain("OrderRepo.cs:45:25 in QueryOrders");
        output.Should().Contain("Suggested Constant: Constants.PendingOrdersQuery");
        output.Should().Contain("Minimal Declaration:");
        output.Should().Contain("static class Constants");
        output.Should().Contain("internal const string PendingOrdersQuery = \"...\";");
    }

    [Fact]
    public void PrintRepeatedStringGroups_ExistingConstant_OutputsCleanRecommendation()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var str = "https://api.example.com/v1/resource";
        var constOcc = new RepeatedStringOccurrence
        {
            FilePath = "src/Common/Routes.cs",
            StartLine = 12,
            EndLine = 12,
            StartColumn = 25,
            EndColumn = 25 + str.Length,
            StringValue = str,
            IsConstDeclaration = true,
            ConstantName = "ResourceRoute",
            ConstantTypeName = "Routes"
        };
        var inlineOcc = new RepeatedStringOccurrence
        {
            FilePath = "src/Services/Client.cs",
            StartLine = 30,
            EndLine = 30,
            StartColumn = 18,
            EndColumn = 18 + str.Length,
            StringValue = str,
            EnclosingMember = "FetchResource",
            Category = FileCategory.Production
        };

        var group = new RepeatedStringGroup
        {
            Value = str,
            Occurrences = [constOcc, inlineOcc],
            ExistingConstantDeclarations = [constOcc],
            Category = CloneCategory.Production,
            SuggestedConstantName = "ResourceRoute",
            SuggestedClassName = "Routes",
            TargetNamespace = "Common"
        };

        formatter.PrintRepeatedStringGroups(writer, [group], ".");

        var output = writer.ToString();
        output.Should().Contain("[PRODUCTION EXISTING CONSTANT #1]");
        output.Should().Contain("1 inline occurrence(s)");
        output.Should().Contain("Existing Constant: Routes.ResourceRoute in src/Common/Routes.cs:12:25");
        output.Should().Contain("Inline Occurrences (1):");
        output.Should().Contain("Client.cs:30:18 in FetchResource");
        output.Should().Contain("Recommendation: Use existing constant 'Routes.ResourceRoute' instead of inline value.");
        output.Should().NotContain("Minimal Declaration:");
    }

    [Fact]
    public void PrintRepeatedStringGroups_PrivateConstantWithExternalOccurrence_IncludesPromotionNote()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var str = "https://internal.service.local/api/v1/auth";
        var constOcc = new RepeatedStringOccurrence
        {
            FilePath = "src/Internal/AuthModule.cs",
            StartLine = 10,
            EndLine = 10,
            StartColumn = 20,
            EndColumn = 20 + str.Length,
            StringValue = str,
            IsConstDeclaration = true,
            IsPrivateConstant = true,
            ConstantName = "AuthUrl",
            ConstantTypeName = "AuthModule"
        };
        var inlineOcc = new RepeatedStringOccurrence
        {
            FilePath = "src/Consumers/ApiConsumer.cs",
            StartLine = 45,
            EndLine = 45,
            StartColumn = 12,
            EndColumn = 12 + str.Length,
            StringValue = str
        };

        var group = new RepeatedStringGroup
        {
            Value = str,
            Occurrences = [constOcc, inlineOcc],
            ExistingConstantDeclarations = [constOcc],
            Category = CloneCategory.Production,
            SuggestedConstantName = "AuthUrl",
            SuggestedClassName = "AuthModule"
        };

        formatter.PrintRepeatedStringGroups(writer, [group], ".");

        var output = writer.ToString();
        output.Should().Contain("Recommendation: Use existing constant 'AuthModule.AuthUrl' instead of inline value (promote to internal/public if needed).");
    }

    [Fact]
    public void PrintAiActionableIncrementalFeedback_MultiViolationWithExistingConstant_PrintsExistingConstantInfoAndUpdatedInstructions()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var str = "https://internal.service.local/api/v1/data";
        var constOcc = new RepeatedStringOccurrence
        {
            FilePath = "src/Config.cs",
            StartLine = 5,
            EndLine = 5,
            StartColumn = 10,
            EndColumn = 10 + str.Length,
            StringValue = str,
            IsConstDeclaration = true,
            IsPrivateConstant = false,
            ConstantName = "DataEndpoint",
            ConstantTypeName = "Config"
        };
        var inlineOcc = new RepeatedStringOccurrence
        {
            FilePath = "src/Services/DataService.cs",
            StartLine = 22,
            EndLine = 22,
            StartColumn = 14,
            EndColumn = 14 + str.Length,
            StringValue = str
        };

        var strGroup = new RepeatedStringGroup
        {
            Value = str,
            Occurrences = [constOcc, inlineOcc],
            ExistingConstantDeclarations = [constOcc],
            Category = CloneCategory.Production,
            SuggestedConstantName = "DataEndpoint",
            SuggestedClassName = "Config"
        };

        var cloneMethods = new List<MethodCloneInfo>
        {
            new("src/ServiceA.cs", "DoSomething", 1, 10, 8, "hashA", "void DoSomething()"),
            new("src/ServiceB.cs", "DoSomething", 1, 10, 8, "hashA", "void DoSomething()")
        };
        var cloneGroup = new CodeCloneGroup("hashA", cloneMethods, CloneCategory.Production, 8);

        formatter.PrintAiActionableIncrementalFeedback(
            writer,
            cloneGroups: [cloneGroup],
            subMethodGroups: [],
            verboseComments: [],
            baseDirectory: ".",
            duplicationTolerance: 0,
            hasDuplicationViolation: true,
            repeatedStrings: [strGroup],
            repeatedStringTolerance: 0,
            hasRepeatedStringViolation: true);

        var output = writer.ToString();
        output.Should().Contain("[AI AGENT ACTION REQUIRED] Incremental Policy Violations (Exit Code: 2)");
        output.Should().Contain("1 inline occurrence(s)");
        output.Should().Contain("Existing Constant: Config.DataEndpoint");
        output.Should().Contain("Recommendation: Use existing constant 'Config.DataEndpoint' instead of inline value.");
        output.Should().Contain("Replace inline strings with existing constants when available, or extract duplicate strings into a shared constant");
    }

    [Fact]
    public void PrintTypeFileIssues_OutputsFormattedSplitAndRenameDetails()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var primary = new ExtractedTypeInfo { TypeName = "PrimaryService", Kind = "class", LinesOfCode = 30, StartLine = 1, EndLine = 35, TargetFileName = "PrimaryService.cs", IsPrimary = true };
        var auxiliary = new ExtractedTypeInfo { TypeName = "AuxService", Kind = "class", LinesOfCode = 20, StartLine = 36, EndLine = 55, TargetFileName = "AuxService.cs" };

        var issue = new TypeFileAnalysis
        {
            FilePath = "src/OldService.cs",
            Action = TypeFileAction.SplitAndRename,
            SuggestedFileName = "PrimaryService.cs",
            Types = [primary, auxiliary]
        };

        formatter.PrintTypeFileIssues(writer, [issue], ".");

        var output = writer.ToString();
        output.Should().Contain("Found 1 File(s) with Multiple Types or Mismatched Names:");
        output.Should().Contain("[SPLIT & RENAME FILE #1] src/OldService.cs → PrimaryService.cs:");
        output.Should().Contain("Primary Type: PrimaryService (renames file to PrimaryService.cs)");
        output.Should().Contain("• AuxService (class, 20 LOC) → AuxService.cs");
    }

    [Fact]
    public void PrintAiActionableIncrementalFeedback_WithSplitAndRename_OutputsDistinctLabel()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var primary = new ExtractedTypeInfo { TypeName = "PrimaryService", Kind = "class", LinesOfCode = 30, StartLine = 1, EndLine = 35, TargetFileName = "PrimaryService.cs", IsPrimary = true };
        var auxiliary = new ExtractedTypeInfo { TypeName = "AuxService", Kind = "class", LinesOfCode = 20, StartLine = 36, EndLine = 55, TargetFileName = "AuxService.cs" };

        var issue = new TypeFileAnalysis
        {
            FilePath = "src/OldService.cs",
            Action = TypeFileAction.SplitAndRename,
            SuggestedFileName = "PrimaryService.cs",
            Types = [primary, auxiliary]
        };

        formatter.PrintAiActionableIncrementalFeedback(
            writer,
            cloneGroups: [],
            subMethodGroups: [],
            verboseComments: [],
            baseDirectory: ".",
            duplicationTolerance: 0,
            hasDuplicationViolation: false,
            typeIssues: [issue],
            typeTolerance: 0,
            hasTypeViolation: true);

        var output = writer.ToString();
        output.Should().Contain("[AI AGENT ACTION REQUIRED] Single Type Per File Policy Violation (Exit Code: 2)");
        output.Should().Contain("Issue #1 [Split & Rename]: src/OldService.cs → PrimaryService.cs (Contains 2 types)");
        output.Should().Contain("Primary Type: PrimaryService (renames file to PrimaryService.cs)");
        output.Should().Contain("• AuxService (class, 20 LOC) → AuxService.cs");
    }

    [Fact]
    public void PrintLinesOfCodeViolations_OutputsFormattedViolations()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var violation1 = new LinesOfCodeViolation
        {
            FilePath = "src/Service.cs",
            Identifier = "Service.LongMethod()",
            Kind = LinesOfCodeKind.Method,
            LinesOfCode = 180,
            Threshold = 150,
            StartLine = 10,
            EndLine = 200,
            Category = FileCategory.Production
        };
        var violation2 = new LinesOfCodeViolation
        {
            FilePath = "src/LargeClass.cs",
            Identifier = "LargeClass",
            Kind = LinesOfCodeKind.Class,
            LinesOfCode = 1600,
            Threshold = 1500,
            StartLine = 1,
            EndLine = 1750,
            Category = FileCategory.Production
        };

        formatter.PrintLinesOfCodeViolations(writer, [violation1, violation2], ".");

        var output = writer.ToString();
        output.Should().Contain("Found 2 Declaration(s) Exceeding Lines of Code Thresholds:");
        output.Should().Contain("Classes Exceeding Threshold (1):");
        output.Should().Contain("Class #1 [1600 LOC > threshold 1500]:");
        output.Should().Contain("src/LargeClass.cs:1-1750");
        output.Should().Contain("LargeClass");
        output.Should().Contain("Methods Exceeding Threshold (1):");
        output.Should().Contain("Method #1 [180 LOC > threshold 150]:");
        output.Should().Contain("src/Service.cs:10-200");
        output.Should().Contain("Service.LongMethod()");
    }

    [Fact]
    public void PrintAiActionableIncrementalFeedback_WithLinesOfCodeViolation_OutputsDistinctActionableInstructions()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var violation = new LinesOfCodeViolation
        {
            FilePath = "src/Service.cs",
            Identifier = "Service.BigMethod()",
            Kind = LinesOfCodeKind.Method,
            LinesOfCode = 175,
            Threshold = 150,
            StartLine = 10,
            EndLine = 195,
            Category = FileCategory.Production
        };

        formatter.PrintAiActionableIncrementalFeedback(
            writer,
            cloneGroups: [],
            subMethodGroups: [],
            verboseComments: [],
            baseDirectory: ".",
            duplicationTolerance: 0,
            hasDuplicationViolation: false,
            linesOfCodeViolations: [violation],
            linesOfCodeTolerance: 0,
            hasLinesOfCodeViolation: true);

        var output = writer.ToString();
        output.Should().Contain("[AI AGENT ACTION REQUIRED] Lines of Code Limits Policy Violation (Exit Code: 2)");
        output.Should().Contain("Violation #1 [Method, 175 LOC > threshold 150]:");
        output.Should().Contain("src/Service.cs:10-195 in Service.BigMethod()");
        output.Should().Contain("Methods: Refactor large methods into smaller helpers using Extract Method pattern.");
        output.Should().Contain("[SuppressMessage(\"CodeCondenser\", \"MethodLength\")]");
    }

    [Fact]
    public void PrintPredicateCloneGroups_WithExistingHelper_PrintsHelperCallSitesAndReplacement()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var helper = new PredicateHelperInfo
        {
            HelperName = "IsAutonomousTask",
            DeclaringTypeName = "TaskHelper",
            IsStatic = true
        };

        var occ = new PredicateOccurrence
        {
            FilePath = "src/Consumer.cs",
            StartLine = 15,
            EndLine = 15,
            StartColumn = 10,
            EndColumn = 60,
            ExpressionText = "taskId.HasValue && taskId.Value > 0",
            NormalizedSignature = "$0.HasValue && $0.Value > CONST",
            NormalizedHash = 12345,
            ArgumentExpressions = ["taskId"]
        };

        var group = new PredicateCloneGroup
        {
            NormalizedSignature = "$0.HasValue && $0.Value > CONST",
            Occurrences = [occ],
            Category = CloneCategory.Production,
            ExistingHelper = helper,
            SuggestedHelperName = "IsAutonomousTask"
        };

        formatter.PrintPredicateCloneGroups(writer, [group], ".");
        var output = writer.ToString();

        output.Should().Contain("Found 1 Predicate / Boolean Condition Clone Cluster(s):");
        output.Should().Contain("Matches 'IsAutonomousTask'");
        output.Should().Contain("TaskHelper.static IsAutonomousTask");
        output.Should().Contain("src/Consumer.cs:15:10");
        output.Should().Contain("IsAutonomousTask(taskId)");
    }

    [Fact]
    public void PrintAiActionableIncrementalFeedback_WithPredicateViolation_OutputsDistinctActionableInstructions()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var occ1 = new PredicateOccurrence
        {
            FilePath = "src/A.cs",
            StartLine = 10,
            EndLine = 10,
            StartColumn = 15,
            EndColumn = 60,
            ExpressionText = "a > 0 && b < 100",
            NormalizedSignature = "$0 > CONST && $1 < CONST",
            NormalizedHash = 55555,
            ArgumentExpressions = ["a", "b"]
        };

        var group = new PredicateCloneGroup
        {
            NormalizedSignature = "$0 > CONST && $1 < CONST",
            Occurrences = [occ1],
            Category = CloneCategory.Production,
            SuggestedHelperName = "IsValidRange",
            SuggestedClassName = "PredicateHelpers"
        };

        formatter.PrintAiActionableIncrementalFeedback(
            writer,
            cloneGroups: [],
            subMethodGroups: [],
            verboseComments: [],
            baseDirectory: ".",
            duplicationTolerance: 0,
            hasDuplicationViolation: false,
            predicateGroups: [group],
            predicateTolerance: 0,
            hasPredicateViolation: true);

        var output = writer.ToString();
        output.Should().Contain("[AI AGENT ACTION REQUIRED] Predicate Clone Policy Violation (Exit Code: 2)");
        output.Should().Contain("Incremental analysis detected 1 new predicate clone cluster(s)");
        output.Should().Contain("src/A.cs:10:15");
        output.Should().Contain("codecondenser:ignore-predicate");
    }

    [Fact]
    public void PrintAiActionableIncrementalFeedback_WithPredicateViolationAndToleratedLoc_DisplaysPredicateBanner()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var occ = new PredicateOccurrence
        {
            FilePath = "src/A.cs",
            StartLine = 10,
            EndLine = 10,
            StartColumn = 15,
            EndColumn = 60,
            ExpressionText = "a > 0 && b < 100",
            NormalizedSignature = "$0 > CONST && $1 < CONST",
            NormalizedHash = 55555,
            ArgumentExpressions = ["a", "b"]
        };

        var group = new PredicateCloneGroup
        {
            NormalizedSignature = "$0 > CONST && $1 < CONST",
            Occurrences = [occ],
            Category = CloneCategory.Production,
            SuggestedHelperName = "IsValidRange",
            SuggestedClassName = "PredicateHelpers"
        };

        var locViolation = new LinesOfCodeViolation
        {
            FilePath = "src/LongMethod.cs",
            Identifier = "Process",
            Kind = LinesOfCodeKind.Method,
            LinesOfCode = 160,
            Threshold = 150,
            StartLine = 1,
            EndLine = 180,
            Category = FileCategory.Production
        };

        // LOC count is 1, but tolerance is 5, so hasLinesOfCodeViolation is false.
        formatter.PrintAiActionableIncrementalFeedback(
            writer,
            cloneGroups: [],
            subMethodGroups: [],
            verboseComments: [],
            baseDirectory: ".",
            duplicationTolerance: 0,
            hasDuplicationViolation: false,
            linesOfCodeViolations: [locViolation],
            linesOfCodeTolerance: 5,
            hasLinesOfCodeViolation: false,
            predicateGroups: [group],
            predicateTolerance: 0,
            hasPredicateViolation: true);

        var output = writer.ToString();
        output.Should().Contain("[AI AGENT ACTION REQUIRED] Predicate Clone Policy Violation (Exit Code: 2)");
        output.Should().Contain("Incremental analysis detected 1 new predicate clone cluster(s)");
    }

    [Fact]
    public void PrintSummary_WhenCommentsNotAnalyzed_DoesNotPrintVerboseCommentsOrCommentGain()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        formatter.PrintSummary(
            writer,
            filesAnalyzed: 3,
            methodsScanned: 5,
            cloneGroupsCount: 0,
            verboseCommentsCount: null,
            mode: "Dry-Run");

        var output = writer.ToString();
        output.Should().NotContain("Verbose Comments:");
        output.Should().NotContain("Comment Gain:");
        output.Should().Contain("Clone Clusters:    0");
    }

    [Fact]
    public void PrintSummary_WhenDuplicationNotAnalyzed_DoesNotPrintCloneClustersOrMethodsScanned()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        formatter.PrintSummary(
            writer,
            filesAnalyzed: 3,
            methodsScanned: null,
            cloneGroupsCount: null,
            verboseCommentsCount: 2,
            mode: "Dry-Run");

        var output = writer.ToString();
        output.Should().NotContain("Methods Scanned:");
        output.Should().NotContain("Clone Clusters:");
        output.Should().NotContain("Production Clones:");
        output.Should().NotContain("Test Clones:");
        output.Should().NotContain("Clone Gain:");
        output.Should().Contain("Verbose Comments:  2");
    }

    [Fact]
    public void PrintVerboseComments_WhenLimited_PrintsHeaderWithShowingTopCount()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var comment = new VerboseComment("A.cs", 1, 5, 0, 150, CommentKind.SingleLine, new string('X', 150), "");
        formatter.PrintVerboseComments(writer, [comment], new Dictionary<string, string>(), ".", totalCount: 10);

        var output = writer.ToString();
        output.Should().Contain("Found 10 verbose comment(s) exceeding threshold (Showing top 1 with largest benefit):");
    }

    [Fact]
    public void PrintCloneGroups_WhenLimited_PrintsHeaderWithShowingTopCount()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        var method = new MethodCloneInfo("A.cs", "M1", 1, 10, 10, "h1", "void M1()");
        var group = new CodeCloneGroup("h1", [method, method], CloneCategory.Production, ImpactScore: 20);
        formatter.PrintCloneGroups(writer, [group], ".", totalCount: 15, totalProductionCount: 15);

        var output = writer.ToString();
        output.Should().Contain("Found 15 Production Duplication Cluster(s) (Showing top 1 with largest benefit):");
    }

    [Fact]
    public void PrintCloneGroups_WhenLimitedToZero_DoesNotSayNoStructuralDuplicationDetected()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        formatter.PrintCloneGroups(writer, [], ".", totalCount: 5);

        var output = writer.ToString();
        output.Should().NotContain("No structural code duplication detected.");
    }

    [Fact]
    public void PrintVerboseComments_WhenLimitedToZero_DoesNotSayNoVerboseCommentsDetected()
    {
        var formatter = new ConsoleFormatter(enableColor: false);
        using var writer = new StringWriter();

        formatter.PrintVerboseComments(writer, [], new Dictionary<string, string>(), ".", totalCount: 5);

        var output = writer.ToString();
        output.Should().NotContain("No verbose comments detected.");
    }
}
