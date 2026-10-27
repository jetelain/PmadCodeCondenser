using System.Reflection;
using AwesomeAssertions;
using Pmad.CodeCondenser.Core.Comments;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Infrastructure.Storage;

namespace Pmad.CodeCondenser.Cli.Tests;

public class CliIntegrationTests : IDisposable
{
    private readonly string _testDirectory;

    public CliIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "CliIntegrationTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_HelpOption_OutputsHelpAndExitsZero()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(["--help"], output, error);

        exitCode.Should().Be(0);
        error.ToString().Should().BeEmpty();
        output.ToString().Should().Contain("codecondenser");
        output.ToString().Should().Contain("analyze");
        output.ToString().Should().Contain("apply");
        output.ToString().Should().Contain("report");
    }

    [Fact]
    public async Task RunAsync_VersionOption_OutputsVersionAndExitsZero()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(["--version"], output, error);

        exitCode.Should().Be(0);
        error.ToString().Should().BeEmpty();

        var assembly = typeof(Program).Assembly;
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = assembly.GetName().Version?.ToString(3) ?? "0.1.0-preview";
        var expectedVersion = infoVersion?.Split('+')[0] ?? version;

        output.ToString().Trim().Should().Be(expectedVersion);
    }

    [Fact]
    public async Task RunAsync_VersionOption_ShortAlias_OutputsVersionAndExitsZero()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(["-v"], output, error);

        exitCode.Should().Be(0);
        error.ToString().Should().BeEmpty();

        var assembly = typeof(Program).Assembly;
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = assembly.GetName().Version?.ToString(3) ?? "0.1.0-preview";
        var expectedVersion = infoVersion?.Split('+')[0] ?? version;

        output.ToString().Trim().Should().Be(expectedVersion);
    }

    [Fact]
    public async Task RunAsync_InvalidOption_OutputsErrorAndExitsOne()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(["analyze", "--unknown-flag"], output, error);

        exitCode.Should().NotBe(0);
        error.ToString().Should().Contain("--unknown-flag");
    }

    [Fact]
    public async Task RunAsync_ExplicitMissingConfigFile_ReturnsErrorAndExitsOne()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var missingConfig = Path.Combine(_testDirectory, "nonexistent-config.json");
        var exitCode = await Program.RunAsync(["analyze", _testDirectory, "--config", missingConfig], output, error);

        exitCode.Should().Be(ExitCodes.Error);
        error.ToString().Should().Contain("Configuration file not found");
        error.ToString().Should().Contain("nonexistent-config.json");
    }

    [Fact]
    public async Task RunAsync_ExplicitMissingGlobalConfigFile_ReturnsErrorAndExitsOne()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var missingGlobal = Path.Combine(_testDirectory, "nonexistent-global.json");
        var exitCode = await Program.RunAsync(["analyze", _testDirectory, "--global-config", missingGlobal], output, error);

        exitCode.Should().Be(ExitCodes.Error);
        error.ToString().Should().Contain("Global configuration path not found");
        error.ToString().Should().Contain("nonexistent-global.json");
    }

    [Fact]
    public async Task RunAsync_ReportCommand_RunsInReportOnlyMode()
    {
        var file = Path.Combine(_testDirectory, "ReportTest.cs");
        await File.WriteAllTextAsync(file, """
        public class ReportTest
        {
            // This is a very long and verbose explanation that easily exceeds the default threshold of one hundred characters in total length
            public void Run() {}
        }
        """);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(["report", _testDirectory, "--no-color"], output, error);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("Found 1 verbose comment(s) exceeding threshold:");
        output.ToString().Should().Contain("Recommendation: Condense verbose comment");
    }

    [Fact]
    public async Task RunAsync_ReportCommand_WithNoComments_DoesNotReportComments()
    {
        var file = Path.Combine(_testDirectory, "ReportNoCommentsTest.cs");
        await File.WriteAllTextAsync(file, """
        public class ReportNoCommentsTest
        {
            // This is a very long and verbose explanation that easily exceeds the default threshold of one hundred characters in total length
            public void Run() {}
        }
        """);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(["report", _testDirectory, "--no-comments", "--no-color"], output, error);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("No verbose comments detected.");
        output.ToString().Should().NotContain("Found 1 verbose comment(s) exceeding threshold:");
    }

    [Fact]
    public async Task RunAsync_ApplyMode_WhenNoFilesModified_PrintsNoFilesRequiredModification()
    {
        var file = Path.Combine(_testDirectory, "CleanService.cs");
        await File.WriteAllTextAsync(file, "public class CleanService {}");

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["apply", _testDirectory, "--no-color"],
            output,
            error);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("No source files required modification.");
        output.ToString().Should().NotContain("Successfully updated comments across modified source file(s).");
    }

    [Fact]
    public async Task RunAsync_SingleFileTarget_FormatsRelativePathCorrectly()
    {
        var file = Path.Combine(_testDirectory, "SingleService.cs");
        await File.WriteAllTextAsync(file, """
        public class SingleService
        {
            // This is an extremely long and verbose explanation that easily exceeds the default threshold of one hundred characters in total length
            public void Run() {}
        }
        """);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var mockSummarizer = new MockSummarizer();
        var engine = new CodeCondenserEngine(
            snapshotStore: new JsonSnapshotStore(),
            commentSummarizer: mockSummarizer);

        var exitCode = await Program.RunAsync(
            ["apply", file, "--no-color"],
            output,
            error,
            customEngine: engine);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("Applied: SingleService.cs");
        output.ToString().Should().NotContain("Applied: .");
    }

    [Fact]
    public async Task RunAsync_DryRunAnalysis_DetectsClonesAndCommentsWithoutModifyingFiles()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "BillingService.cs");
        var file2 = Path.Combine(_testDirectory, "InvoiceService.cs");

        var code1 = """
        namespace MyCompany.Billing;
        public class BillingService
        {
            // This is an extremely long and verbose explanation that easily exceeds the default threshold of one hundred characters in total length
            public void ProcessBilling(int accountId, double amount)
            {
                double tax = amount * 0.2;
                double total = amount + tax;
                if (total > 1000)
                {
                    total -= 50;
                }
                double finalAmount = total;
                Console.WriteLine(finalAmount);
                double fee = finalAmount * 0.05;
                Console.WriteLine(fee);
            }
        }
        """;

        var code2 = """
        namespace MyCompany.Invoicing;
        public class InvoiceService
        {
            public void ProcessInvoice(int clientId, double price)
            {
                double vat = price * 0.2;
                double grandTotal = price + vat;
                if (grandTotal > 1000)
                {
                    grandTotal -= 50;
                }
                double finalPrice = grandTotal;
                Console.WriteLine(finalPrice);
                double commission = finalPrice * 0.05;
                Console.WriteLine(commission);
            }
        }
        """;

        await File.WriteAllTextAsync(file1, code1);
        await File.WriteAllTextAsync(file2, code2);

        var mockSummarizer = new MockSummarizer();
        var engine = new CodeCondenserEngine(
            snapshotStore: new JsonSnapshotStore(),
            commentSummarizer: mockSummarizer);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--no-color"],
            output,
            error,
            customEngine: engine);

        // Assert
        exitCode.Should().Be(0);
        var outputText = output.ToString();

        outputText.Should().Contain("Found 1 Production Duplication Cluster(s)");
        outputText.Should().Contain("Found 1 verbose comment(s) exceeding threshold:");
        outputText.Should().Contain("BillingService.cs");
        outputText.Should().Contain("InvoiceService.cs");
        outputText.Should().Contain("Mode:              Dry-Run");

        // Dry-run guarantee: Source files must NOT be modified
        var textOnDisk = await File.ReadAllTextAsync(file1);
        textOnDisk.Should().Be(code1);
    }

    [Fact]
    public async Task RunAsync_ApplyMode_ModifiesSourceFilesWithSummaries()
    {
        // Arrange
        var file = Path.Combine(_testDirectory, "AccountService.cs");
        var code = """
        namespace MyCompany;
        public class AccountService
        {
            // This is an extremely long and verbose explanation that easily exceeds the default threshold of one hundred characters in total length
            public void Run()
            {
                Console.WriteLine("Account");
            }
        }
        """;

        await File.WriteAllTextAsync(file, code);

        var mockSummarizer = new MockSummarizer();
        var engine = new CodeCondenserEngine(
            snapshotStore: new JsonSnapshotStore(),
            commentSummarizer: mockSummarizer);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(
            ["apply", _testDirectory, "--no-color"],
            output,
            error,
            customEngine: engine);

        // Assert
        exitCode.Should().Be(0);
        output.ToString().Should().Contain("Applied: AccountService.cs");

        var textOnDisk = await File.ReadAllTextAsync(file);
        textOnDisk.Should().NotContain("This is an extremely long and verbose explanation");
        textOnDisk.Should().Contain("// [Summarized] Condensed comment text");

        // Incremental run immediately after apply should see 0 changed files because snapshot baseline was updated
        using var outInc = new StringWriter();
        var exitInc = await Program.RunAsync(
            ["analyze", _testDirectory, "--incremental", "--no-color"],
            outInc,
            new StringWriter(),
            customEngine: engine);

        exitInc.Should().Be(0);
        outInc.ToString().Should().Contain("Files Analyzed:    0");
    }

    [Fact]
    public async Task RunAsync_SnapshotCommand_CreatesSnapshotOnDisk()
    {
        // Arrange
        var file = Path.Combine(_testDirectory, "Order.cs");
        await File.WriteAllTextAsync(file, """
        namespace MyCompany;
        public class Order
        {
            public void TestMethod(int a, int b)
            {
                int c = a + b;
                int d = c * 2;
                int e = d - 1;
                if (e > 5) e = 5;
                int f = e + 2;
                Console.WriteLine(f);
                int g = f * 3;
                Console.WriteLine(g);
            }
        }
        """);

        var engine = new CodeCondenserEngine(snapshotStore: new JsonSnapshotStore());

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(
            ["snapshot", _testDirectory, "--no-color"],
            output,
            error,
            customEngine: engine);

        // Assert
        exitCode.Should().Be(0);
        output.ToString().Should().Contain("Snapshot baseline updated:");

        var snapshotFile = Path.Combine(_testDirectory, ".codecondenser", "snapshot.json");
        File.Exists(snapshotFile).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_IncrementalMode_LeveragesSnapshotBaseline()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "A.cs");
        var file2 = Path.Combine(_testDirectory, "B.cs");
        await File.WriteAllTextAsync(file1, "public class A { public void M() {} }");
        await File.WriteAllTextAsync(file2, "public class B { public void M() {} }");

        var store = new JsonSnapshotStore();
        var engine = new CodeCondenserEngine(snapshotStore: store);

        // First run: create snapshot baseline
        using var out1 = new StringWriter();
        var exit1 = await Program.RunAsync(
            ["snapshot", _testDirectory, "--no-color"],
            out1,
            new StringWriter(),
            customEngine: engine);
        exit1.Should().Be(0);

        // Second run: incremental with no changes
        using var out2 = new StringWriter();
        var exit2 = await Program.RunAsync(
            ["analyze", _testDirectory, "--incremental", "--no-color"],
            out2,
            new StringWriter(),
            customEngine: engine);

        exit2.Should().Be(0);
        out2.ToString().Should().Contain("Files Analyzed:    0");
    }

    [Fact]
    public async Task RunAsync_ThresholdOverrides_FilterOutUnmatchedItems()
    {
        // Arrange
        var file = Path.Combine(_testDirectory, "ThresholdTest.cs");
        await File.WriteAllTextAsync(file, """
        namespace MyCompany;
        public class ThresholdTest
        {
            // This is a moderately verbose comment that is over 100 characters long but under 200 characters in length.
            public void SmallMethod()
            {
                int a = 1;
                int b = 2;
                int c = a + b;
                Console.WriteLine(c);
            }
        }
        """);

        var engine = new CodeCondenserEngine(snapshotStore: new JsonSnapshotStore());

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Threshold comment raised to 300
        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--threshold-comments", "300", "--threshold-statements", "15", "--no-color"],
            output,
            error,
            customEngine: engine);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("No verbose comments detected.");
        output.ToString().Should().Contain("No structural code duplication detected.");
    }

    [Fact]
    public async Task RunAsync_ContextSizeOverride_AppliesToSettings()
    {
        CodeCondenserSettings? capturedSettings = null;
        var summarizer = new SettingsCapturingSummarizer(s => capturedSettings = s);
        var engine = new CodeCondenserEngine(
            snapshotStore: new JsonSnapshotStore(),
            commentSummarizer: summarizer);

        var file = Path.Combine(_testDirectory, "ContextSizeTest.cs");
        await File.WriteAllTextAsync(file, """
        namespace MyCompany;
        public class ContextSizeTest
        {
            // This is a very long comment that exceeds the default threshold of one hundred characters to trigger summarizer invocation.
            public void SmallMethod() { }
        }
        """);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["apply", _testDirectory, "--context-size", "16384", "--no-color"],
            output,
            error,
            customEngine: engine);

        exitCode.Should().Be(0);
        capturedSettings.Should().NotBeNull();
        capturedSettings!.Ollama.ContextSize.Should().Be(16384);
    }

    [Fact]
    public async Task RunAsync_IncrementalMode_WhenAdditionalDuplicationDetected_ReturnsExitCode2AndActionableFeedback()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "OriginalService.cs");
        var cloneCode = """
        namespace MyCompany;
        public class OriginalService
        {
            public void Compute(int a, int b)
            {
                int x = a + b;
                int y = x * 2;
                int z = y - 5;
                if (z > 10) z = 10;
                int result = z + 1;
                Console.WriteLine(result);
                int finalVal = result * 3;
                Console.WriteLine(finalVal);
            }
        }
        """;
        await File.WriteAllTextAsync(file1, cloneCode);

        var engine = new CodeCondenserEngine(snapshotStore: new JsonSnapshotStore());

        // Step 1: establish baseline snapshot
        using var outSnapshot = new StringWriter();
        var exitSnapshot = await Program.RunAsync(
            ["snapshot", _testDirectory, "--no-color"],
            outSnapshot,
            new StringWriter(),
            customEngine: engine);
        exitSnapshot.Should().Be(0);

        // Step 2: introduce a new file with duplicated method logic
        var file2 = Path.Combine(_testDirectory, "DuplicatedService.cs");
        var duplicateCode = """
        namespace MyCompany;
        public class DuplicatedService
        {
            public void Calculate(int p1, int p2)
            {
                int val1 = p1 + p2;
                int val2 = val1 * 2;
                int val3 = val2 - 5;
                if (val3 > 10) val3 = 10;
                int res = val3 + 1;
                Console.WriteLine(res);
                int outputVal = res * 3;
                Console.WriteLine(outputVal);
            }
        }
        """;
        await File.WriteAllTextAsync(file2, duplicateCode);

        // Step 3: Run incremental analysis
        using var outInc = new StringWriter();
        using var errInc = new StringWriter();
        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--incremental", "--no-color"],
            outInc,
            errInc,
            customEngine: engine);

        // Assert: Exits with code 2 and outputs actionable feedback for an AI agent
        exitCode.Should().Be(2);
        var outputText = outInc.ToString();
        outputText.Should().Contain("[AI AGENT ACTION REQUIRED]");
        outputText.Should().Contain("Code Duplication Policy Violation (Exit Code: 2)");
        outputText.Should().Contain("Incremental analysis detected 1 new code duplication cluster(s) exceeding tolerance margin (allowed: 0).");
        outputText.Should().Contain("DuplicatedService.cs");
        outputText.Should().Contain("OriginalService.cs");
        outputText.Should().Contain("Actionable Instructions for AI Agent:");
        outputText.Should().Contain("Extract the identical structural logic into a shared helper method");
    }

    [Fact]
    public async Task RunAsync_IncrementalMode_WhenDuplicationWithinTolerance_ReturnsExitCode0()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "BaseSvc.cs");
        var cloneCode = """
        namespace MyCompany;
        public class BaseSvc
        {
            public void Execute(int a, int b)
            {
                int x = a + b;
                int y = x * 2;
                int z = y - 5;
                if (z > 10) z = 10;
                int result = z + 1;
                Console.WriteLine(result);
                int finalVal = result * 3;
                Console.WriteLine(finalVal);
            }
        }
        """;
        await File.WriteAllTextAsync(file1, cloneCode);

        var engine = new CodeCondenserEngine(snapshotStore: new JsonSnapshotStore());

        // Step 1: establish baseline snapshot
        await Program.RunAsync(
            ["snapshot", _testDirectory, "--no-color"],
            new StringWriter(),
            new StringWriter(),
            customEngine: engine);

        // Step 2: introduce a clone
        var file2 = Path.Combine(_testDirectory, "CloneSvc.cs");
        await File.WriteAllTextAsync(file2, cloneCode.Replace("BaseSvc", "CloneSvc"));

        // Step 3: Run incremental with tolerance = 1
        using var outInc = new StringWriter();
        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--incremental", "--duplication-tolerance", "1", "--no-color"],
            outInc,
            new StringWriter(),
            customEngine: engine);

        // Assert: Allowed by tolerance, exits with 0
        exitCode.Should().Be(0);
        outInc.ToString().Should().NotContain("[AI AGENT ACTION REQUIRED]");
    }

    [Fact]
    public async Task RunAsync_DisableDuplicationDetection_DoesNotReportClones()
    {
        var file1 = Path.Combine(_testDirectory, "Svc1.cs");
        var file2 = Path.Combine(_testDirectory, "Svc2.cs");
        var code = """
        namespace MyCompany;
        public class Svc
        {
            public void M(int a, int b)
            {
                int x = a + b;
                int y = x * 2;
                int z = y - 5;
                if (z > 10) z = 10;
                int r = z + 1;
                Console.WriteLine(r);
                int f = r * 3;
                Console.WriteLine(f);
            }
        }
        """;
        await File.WriteAllTextAsync(file1, code);
        await File.WriteAllTextAsync(file2, code.Replace("Svc", "Svc2"));

        var engine = new CodeCondenserEngine(snapshotStore: new JsonSnapshotStore());

        using var output = new StringWriter();
        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--no-duplication", "--no-color"],
            output,
            new StringWriter(),
            customEngine: engine);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("No structural code duplication detected.");
        output.ToString().Should().NotContain("Found 1 code duplication cluster(s):");
    }

    [Fact]
    public async Task RunAsync_DisableCommentSummarization_DoesNotReportComments()
    {
        var file = Path.Combine(_testDirectory, "CommentSvc.cs");
        await File.WriteAllTextAsync(file, """
        namespace MyCompany;
        public class CommentSvc
        {
            // This is an extremely long and verbose explanation that easily exceeds the default threshold of one hundred characters in total length
            public void M() {}
        }
        """);

        var engine = new CodeCondenserEngine(snapshotStore: new JsonSnapshotStore());

        using var output = new StringWriter();
        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--no-comments", "--no-color"],
            output,
            new StringWriter(),
            customEngine: engine);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("No verbose comments detected.");
        output.ToString().Should().NotContain("Found 1 verbose comment(s) exceeding threshold:");
    }

    [Fact]
    public async Task RunAsync_DefaultWithoutLlm_RunsInReportMode_AndOutputsRecommendationsAndSuppressionTips()
    {
        var file = Path.Combine(_testDirectory, "ReportModeTest.cs");
        await File.WriteAllTextAsync(file, """
        namespace MyCompany;
        public class ReportModeTest
        {
            // This is an extremely long and verbose explanation that easily exceeds the default threshold of one hundred characters in total length
            public void M() {}
        }
        """);

        // Engine with no LLM summarizer
        var engine = new CodeCondenserEngine(snapshotStore: new JsonSnapshotStore());

        using var output = new StringWriter();
        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--no-color"],
            output,
            new StringWriter(),
            customEngine: engine);

        exitCode.Should().Be(0);
        var outputText = output.ToString();
        outputText.Should().Contain("Found 1 verbose comment(s) exceeding threshold:");
        outputText.Should().Contain("Recommendation: Condense verbose comment to be concise (max 120 columns)");
        outputText.Should().Contain("To suppress: Add '// codecondenser:ignore' or '// nocondense' on preceding line or inline.");
        outputText.Should().NotContain("+ Condensed:");
    }

    [Fact]
    public async Task RunAsync_IncrementalMode_WhenNewVerboseCommentDetected_OutputsActionableFeedbackOnlyOnceAtTheEnd()
    {
        // 1. Establish baseline
        var file1 = Path.Combine(_testDirectory, "Initial.cs");
        await File.WriteAllTextAsync(file1, """
        namespace MyCompany;
        public class Initial
        {
            public void Run() {}
        }
        """);

        var engine = new CodeCondenserEngine(snapshotStore: new JsonSnapshotStore());
        await Program.RunAsync(
            ["snapshot", _testDirectory, "--no-color"],
            new StringWriter(),
            new StringWriter(),
            customEngine: engine);

        // 2. Introduce a new file with a verbose comment
        var file2 = Path.Combine(_testDirectory, "NewWithComment.cs");
        await File.WriteAllTextAsync(file2, """
        namespace MyCompany;
        public class NewWithComment
        {
            // This is an extremely long and verbose explanation that easily exceeds the default threshold of one hundred characters in total length
            public void RunNew() {}
        }
        """);

        // 3. Run incremental
        using var outInc = new StringWriter();
        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--incremental", "--no-color"],
            outInc,
            new StringWriter(),
            customEngine: engine);

        exitCode.Should().Be(0);
        var outputText = outInc.ToString();
        outputText.Should().Contain("[AI AGENT ACTION REQUIRED] Verbose Comments Detected");
        outputText.Should().Contain("Incremental analysis detected 1 new verbose comment(s) exceeding length thresholds.");
        outputText.Should().Contain("Actionable Instructions for AI Agent:");
        outputText.Should().Contain("XML doc comments: Add 'condense=\"no\"' attribute to XML tag");
        outputText.Should().Contain("Regular comments: Add '// codecondenser:ignore' or '// nocondense'");

        // Actionable feedback should be displayed only once at the end
        System.Text.RegularExpressions.Regex.Matches(outputText, @"\[AI AGENT ACTION REQUIRED\]").Count.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_IncrementalMode_WhenCommentToleranceZero_ReturnsExitCode2()
    {
        // 1. Establish baseline
        var file1 = Path.Combine(_testDirectory, "Base.cs");
        await File.WriteAllTextAsync(file1, "public class Base { public void M() {} }");

        var engine = new CodeCondenserEngine(snapshotStore: new JsonSnapshotStore());
        await Program.RunAsync(
            ["snapshot", _testDirectory, "--no-color"],
            new StringWriter(),
            new StringWriter(),
            customEngine: engine);

        // 2. Introduce new file with verbose comment
        var file2 = Path.Combine(_testDirectory, "NewComment.cs");
        await File.WriteAllTextAsync(file2, """
        public class NewComment
        {
            // This is an extremely long and verbose explanation that easily exceeds the default threshold of one hundred characters in total length
            public void M() {}
        }
        """);

        // 3. Incremental run with --comment-tolerance 0
        using var outInc = new StringWriter();
        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--incremental", "-ct", "0", "--no-color"],
            outInc,
            new StringWriter(),
            customEngine: engine);

        exitCode.Should().Be(2);
        var outputText = outInc.ToString();
        outputText.Should().Contain("[AI AGENT ACTION REQUIRED] Verbose Comments Policy Violation (Exit Code: 2)");
        outputText.Should().Contain("To suppress:");
        System.Text.RegularExpressions.Regex.Matches(outputText, @"\[AI AGENT ACTION REQUIRED\]").Count.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_IncrementalMode_WhenBothDuplicationAndComments_OutputsUnifiedActionableFeedbackOnceAtTheEnd()
    {
        // 1. Establish baseline
        var file1 = Path.Combine(_testDirectory, "Orig.cs");
        await File.WriteAllTextAsync(file1, """
        public class Orig
        {
            public void Compute(int a, int b)
            {
                int x = a + b;
                int y = x * 2;
                int z = y - 5;
                if (z > 10) z = 10;
                int r = z + 1;
                Console.WriteLine(r);
                int f = r * 3;
                Console.WriteLine(f);
            }
        }
        """);

        var engine = new CodeCondenserEngine(snapshotStore: new JsonSnapshotStore());
        await Program.RunAsync(
            ["snapshot", _testDirectory, "--no-color"],
            new StringWriter(),
            new StringWriter(),
            customEngine: engine);

        // 2. Introduce clone method AND verbose comment
        var file2 = Path.Combine(_testDirectory, "NewFeature.cs");
        await File.WriteAllTextAsync(file2, """
        public class NewFeature
        {
            // This is an extremely long and verbose explanation that easily exceeds the default threshold of one hundred characters in total length
            public void Calculate(int a, int b)
            {
                int x = a + b;
                int y = x * 2;
                int z = y - 5;
                if (z > 10) z = 10;
                int r = z + 1;
                Console.WriteLine(r);
                int f = r * 3;
                Console.WriteLine(f);
            }
        }
        """);

        // 3. Incremental run
        using var outInc = new StringWriter();
        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--incremental", "--no-color"],
            outInc,
            new StringWriter(),
            customEngine: engine);

        // Exit code 2 due to duplication policy violation
        exitCode.Should().Be(2);
        var outputText = outInc.ToString();

        // Single actionable header for both violations
        System.Text.RegularExpressions.Regex.Matches(outputText, @"\[AI AGENT ACTION REQUIRED\]").Count.Should().Be(1);
        outputText.Should().Contain("[AI AGENT ACTION REQUIRED] Incremental Policy Violations (Exit Code: 2)");
        outputText.Should().Contain("Code Duplication Clusters:");
        outputText.Should().Contain("Verbose Comments:");
        outputText.Should().Contain("Actionable Instructions for AI Agent:");
        outputText.Should().Contain("Code Duplication:");
        outputText.Should().Contain("To suppress: Add '[SuppressMessage(\"CodeCondenser\", \"Duplication\", Justification = \"<why>\")]' or '// codecondenser:ignore-duplicate'");
        outputText.Should().Contain("Verbose Comments:");
        outputText.Should().Contain("To suppress: Add 'condense=\"no\"' attribute (XML doc) or '// codecondenser:ignore'");
    }

    [Fact]
    public async Task RunAsync_WithGlobalConfig_InheritsLlmSettingsAndDefaultsToReport()
    {
        var globalConfigFile = Path.Combine(_testDirectory, "global-codecondenser.json");
        await File.WriteAllTextAsync(globalConfigFile, """
        {
            "ollama": {
                "endpoint": "http://custom-ollama:11434",
                "model": "gemma4:e4b"
            }
        }
        """);

        var sourceFile = Path.Combine(_testDirectory, "Verbose.cs");
        await File.WriteAllTextAsync(sourceFile, """
        public class Verbose
        {
            // This is a very verbose comment that exceeds the default threshold of one hundred characters to trigger comment extraction
            public void Test() {}
        }
        """);

        bool summarizerCalled = false;
        var summarizer = new SettingsCapturingSummarizer(s =>
        {
            summarizerCalled = true;
        });

        var engine = new CodeCondenserEngine(commentSummarizer: summarizer);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--global-config", globalConfigFile, "--no-color"],
            output,
            error,
            customEngine: engine);

        exitCode.Should().Be(0);
        // In default mode, global LLM config must NOT trigger summarizer; it must run in Report mode
        summarizerCalled.Should().BeFalse();
        output.ToString().Should().Contain("Found 1 verbose comment(s) exceeding threshold");
        output.ToString().Should().Contain("Recommendation: Condense verbose comment");
    }

    [Fact]
    public async Task RunAsync_WithGlobalConfigAndSummarySummarize_UsesGlobalLlmSettings()
    {
        var globalConfigFile = Path.Combine(_testDirectory, "global-codecondenser2.json");
        await File.WriteAllTextAsync(globalConfigFile, """
        {
            "ollama": {
                "endpoint": "http://custom-ollama:11434",
                "model": "gemma4:e4b"
            }
        }
        """);

        var sourceFile = Path.Combine(_testDirectory, "Verbose2.cs");
        await File.WriteAllTextAsync(sourceFile, """
        public class Verbose2
        {
            // This is a very verbose comment that exceeds the default threshold of one hundred characters to trigger comment extraction
            public void Test() {}
        }
        """);

        CodeCondenserSettings? capturedSettings = null;
        var summarizer = new SettingsCapturingSummarizer(s =>
        {
            capturedSettings = s;
        });

        var engine = new CodeCondenserEngine(commentSummarizer: summarizer);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--global-config", globalConfigFile, "--summary", "summarize", "--no-color"],
            output,
            error,
            customEngine: engine);

        exitCode.Should().Be(0);
        capturedSettings.Should().NotBeNull();
        capturedSettings!.Ollama.Endpoint.Should().Be("http://custom-ollama:11434");
        capturedSettings.Ollama.Model.Should().Be("gemma4:e4b");
    }

    [Fact]
    public async Task RunAsync_WithGlobalAndProjectConfig_ProjectOverridesGlobal()
    {
        var globalConfigFile = Path.Combine(_testDirectory, "global-codecondenser3.json");
        await File.WriteAllTextAsync(globalConfigFile, """
        {
            "maxParallelLlmRequests": 8,
            "ollama": {
                "endpoint": "http://custom-ollama:11434",
                "model": "global-model"
            }
        }
        """);

        var projectConfigFile = Path.Combine(_testDirectory, "codecondenser.json");
        await File.WriteAllTextAsync(projectConfigFile, """
        {
            "ollama": {
                "model": "project-model"
            }
        }
        """);

        var sourceFile = Path.Combine(_testDirectory, "Verbose3.cs");
        await File.WriteAllTextAsync(sourceFile, """
        public class Verbose3
        {
            // This is a very verbose comment that exceeds the default threshold of one hundred characters to trigger comment extraction
            public void Test() {}
        }
        """);

        CodeCondenserSettings? capturedSettings = null;
        var summarizer = new SettingsCapturingSummarizer(s =>
        {
            capturedSettings = s;
        });

        var engine = new CodeCondenserEngine(commentSummarizer: summarizer);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--global-config", globalConfigFile, "--no-color"],
            output,
            error,
            customEngine: engine);

        exitCode.Should().Be(0);
        capturedSettings.Should().NotBeNull();
        // Endpoint inherited from global config
        capturedSettings!.Ollama.Endpoint.Should().Be("http://custom-ollama:11434");
        // Model overridden by project config
        capturedSettings.Ollama.Model.Should().Be("project-model");
        capturedSettings.MaxParallelLlmRequests.Should().Be(8);
    }

    private sealed class SettingsCapturingSummarizer : ICommentSummarizer
    {
        private readonly Action<CodeCondenserSettings> _onSummarize;
        public SettingsCapturingSummarizer(Action<CodeCondenserSettings> onSummarize) => _onSummarize = onSummarize;

        public Task<IReadOnlyDictionary<string, string>> SummarizeAsync(
            IReadOnlyList<VerboseComment> comments,
            IReadOnlyDictionary<string, string> existingCache,
            CodeCondenserSettings settings,
            CancellationToken cancellationToken = default)
        {
            _onSummarize(settings);
            var results = new Dictionary<string, string>(existingCache);
            foreach (var comment in comments)
            {
                results[comment.ContentHash] = "// Condensed comment";
            }
            return Task.FromResult<IReadOnlyDictionary<string, string>>(results);
        }
    }

    private sealed class MockSummarizer : ICommentSummarizer
    {
        public Task<IReadOnlyDictionary<string, string>> SummarizeAsync(
            IReadOnlyList<VerboseComment> comments,
            IReadOnlyDictionary<string, string> existingCache,
            CodeCondenserSettings settings,
            CancellationToken cancellationToken = default)
        {
            var results = new Dictionary<string, string>(existingCache);
            foreach (var comment in comments)
            {
                if (!results.ContainsKey(comment.ContentHash))
                {
                    results[comment.ContentHash] = "// [Summarized] Condensed comment text";
                }
            }

            return Task.FromResult<IReadOnlyDictionary<string, string>>(results);
        }
    }
}
