using System.Text.Json;
using AwesomeAssertions;
using Pmad.CodeCondenser.Cli.Formatting.Json;
using Pmad.CodeCondenser.Cli.Formatting.Sarif;
using Pmad.CodeCondenser.Core.Comments;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Infrastructure.Storage;

namespace Pmad.CodeCondenser.Cli.Tests;

public class CliExportIntegrationTests : IDisposable
{
    private readonly string _testDirectory;

    public CliExportIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "CliExportIntegrationTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task RunAsync_FormatSarifStdout_StreamsValidSarifToStdoutAndBannerToStderr()
    {
        await CreateDuplicateFilesAsync();

        using var output = new StringWriter();
        using var error = new StringWriter();

        var engine = CreateEngine();
        var exitCode = await Program.RunAsync(
            [_testDirectory, "--no-comments", "--format", "sarif"],
            output,
            error,
            customEngine: engine);

        exitCode.Should().Be(0);

        // Stderr must receive banner and diagnostic progress
        error.ToString().Should().Contain("=== Pmad.CodeCondenser ===");
        error.ToString().Should().Contain("Analyzing target:");

        // Stdout must contain ONLY valid SARIF 2.1.0 JSON
        var stdoutText = output.ToString().Trim();
        stdoutText.Should().StartWith("{");
        stdoutText.Should().EndWith("}");

        var sarif = JsonSerializer.Deserialize<SarifLog>(stdoutText, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        sarif.Should().NotBeNull();
        sarif!.Version.Should().Be("2.1.0");
        sarif.Runs.Should().HaveCount(1);
        sarif.Runs[0].Results.Should().HaveCount(2); // Symmetrical results
    }

    [Fact]
    public async Task RunAsync_FormatJsonStdout_StreamsValidJsonToStdoutAndBannerToStderr()
    {
        await CreateDuplicateFilesAsync();

        using var output = new StringWriter();
        using var error = new StringWriter();

        var engine = CreateEngine();
        var exitCode = await Program.RunAsync(
            [_testDirectory, "--no-comments", "--format", "json"],
            output,
            error,
            customEngine: engine);

        exitCode.Should().Be(0);

        error.ToString().Should().Contain("=== Pmad.CodeCondenser ===");

        var stdoutText = output.ToString().Trim();
        stdoutText.Should().StartWith("{");
        stdoutText.Should().EndWith("}");

        var report = JsonSerializer.Deserialize<CodeCondenserJsonReport>(stdoutText, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        report.Should().NotBeNull();
        report!.SchemaVersion.Should().Be("1.0.0");
        report.Summary.CloneClustersCount.Should().Be(1);
        report.CloneClusters.Should().HaveCount(1);
        report.CloneClusters[0].Methods.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunAsync_FormatSarifOutputFile_WritesToFileAndStatusToStdout()
    {
        await CreateDuplicateFilesAsync();
        var outputFile = Path.Combine(_testDirectory, "artifacts", "report.sarif");

        using var output = new StringWriter();
        using var error = new StringWriter();

        var engine = CreateEngine();
        var exitCode = await Program.RunAsync(
            [_testDirectory, "--no-comments", "--format", "sarif", "--output", outputFile],
            output,
            error,
            customEngine: engine);

        exitCode.Should().Be(0);

        // Status is on stdout since output goes to file
        output.ToString().Should().Contain("=== Pmad.CodeCondenser ===");
        output.ToString().Should().Contain("Report written to:");

        File.Exists(outputFile).Should().BeTrue();
        var fileContent = await File.ReadAllTextAsync(outputFile);
        var sarif = JsonSerializer.Deserialize<SarifLog>(fileContent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        sarif.Should().NotBeNull();
        sarif!.Runs[0].Results.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunAsync_FormatJsonOutputFile_WritesToFile()
    {
        await CreateDuplicateFilesAsync();
        var outputFile = Path.Combine(_testDirectory, "artifacts", "report.json");

        using var output = new StringWriter();
        using var error = new StringWriter();

        var engine = CreateEngine();
        var exitCode = await Program.RunAsync(
            [_testDirectory, "--no-comments", "-f", "json", "-o", outputFile],
            output,
            error,
            customEngine: engine);

        exitCode.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();

        var fileContent = await File.ReadAllTextAsync(outputFile);
        var report = JsonSerializer.Deserialize<CodeCondenserJsonReport>(fileContent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        report.Should().NotBeNull();
        report!.Summary.FilesAnalyzed.Should().Be(2);
        report.Summary.ProductionFiles.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_ToleranceFailureWithOutputFile_EmitsReportBeforeFailing()
    {
        await CreateDuplicateFilesAsync();
        var outputFile = Path.Combine(_testDirectory, "gate-failure.sarif");

        using var output = new StringWriter();
        using var error = new StringWriter();

        var engine = CreateEngine();
        var exitCode = await Program.RunAsync(
            [_testDirectory, "--no-comments", "--incremental", "--duplication-tolerance", "0", "--format", "sarif", "--output", outputFile],
            output,
            error,
            customEngine: engine);

        // Should return duplication exit code 2
        exitCode.Should().Be(2);

        // Even though exitCode is 2, the SARIF file must have been generated and be valid
        File.Exists(outputFile).Should().BeTrue();
        var fileContent = await File.ReadAllTextAsync(outputFile);
        var sarif = JsonSerializer.Deserialize<SarifLog>(fileContent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        sarif.Should().NotBeNull();
        sarif!.Runs[0].Results.Should().HaveCount(2);
    }

    private async Task CreateDuplicateFilesAsync()
    {
        var file1 = Path.Combine(_testDirectory, "BillingService.cs");
        var file2 = Path.Combine(_testDirectory, "InvoiceService.cs");

        var code1 = """
        namespace MyCompany.Billing;
        public class BillingService
        {
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
    }

    private static CodeCondenserEngine CreateEngine()
    {
        return new CodeCondenserEngine(
            snapshotStore: new JsonSnapshotStore(),
            summarizedCommentsStore: new JsonSummarizedCommentsStore(),
            commentSummarizer: new NoOpSummarizer(),
            subMethodCacheStore: new JsonSubMethodCacheStore());
    }

    private sealed class NoOpSummarizer : ICommentSummarizer
    {
        public Task<IReadOnlyDictionary<string, string>> SummarizeAsync(
            IReadOnlyList<VerboseComment> comments,
            IReadOnlyDictionary<string, string> existingCache,
            CodeCondenserSettings settings,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(existingCache));
        }
    }
}
