using FluentAssertions;
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
        output.ToString().Should().Contain("pmad-codecondenser");
        output.ToString().Should().Contain("--apply");
        output.ToString().Should().Contain("--incremental");
    }

    [Fact]
    public async Task RunAsync_VersionOption_OutputsVersionAndExitsZero()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(["--version"], output, error);

        exitCode.Should().Be(0);
        error.ToString().Should().BeEmpty();
        output.ToString().Should().Contain("pmad-codecondenser version");
    }

    [Fact]
    public async Task RunAsync_InvalidOption_OutputsErrorAndExitsOne()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(["--unknown-flag"], output, error);

        exitCode.Should().Be(1);
        error.ToString().Should().Contain("Unrecognized option: '--unknown-flag'");
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
            [_testDirectory, "--no-color"],
            output,
            error,
            customEngine: engine);

        // Assert
        exitCode.Should().Be(0);
        var outputText = output.ToString();

        outputText.Should().Contain("Found 1 code duplication cluster(s):");
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
            [_testDirectory, "--apply", "--no-color"],
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
            [_testDirectory, "--incremental", "--no-color"],
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
            [_testDirectory, "--incremental", "--no-color"],
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
            [_testDirectory, "--threshold-comments", "300", "--threshold-statements", "15", "--no-color"],
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
            [_testDirectory, "--apply", "--context-size", "16384", "--no-color"],
            output,
            error,
            customEngine: engine);

        exitCode.Should().Be(0);
        capturedSettings.Should().NotBeNull();
        capturedSettings!.Ollama.ContextSize.Should().Be(16384);
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
