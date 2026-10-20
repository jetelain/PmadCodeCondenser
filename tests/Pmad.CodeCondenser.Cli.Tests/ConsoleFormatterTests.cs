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
        output.Should().Contain("Found 1 code duplication cluster(s):");
        output.Should().Contain("[DUPLICATION CLUSTER #1]");
        output.Should().Contain("src/Service1.cs:10-20 in Process (8 statements)");
        output.Should().Contain("src/Service2.cs:15-25 in Execute (8 statements)");
        output.Should().Contain("Recommendation: Extract duplicate structural logic");
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
}
