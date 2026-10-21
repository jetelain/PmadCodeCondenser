using FluentAssertions;
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
}
