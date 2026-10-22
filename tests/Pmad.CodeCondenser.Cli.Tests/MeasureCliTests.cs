using System.Text.Json;
using FluentAssertions;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Cli.Tests;

public sealed class MeasureCliTests : IDisposable
{
    private readonly string _testDirectory;

    public MeasureCliTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "MeasureCliTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);

        // Create sample production file
        var prodDir = Path.Combine(_testDirectory, "src");
        Directory.CreateDirectory(prodDir);
        File.WriteAllText(Path.Combine(prodDir, "Calculator.cs"), @"
namespace SampleApp;

/// <summary>
/// A simple calculator class for math operations.
/// </summary>
public class Calculator
{
    // Adds two numbers together
    public int Add(int a, int b) => a + b;
}
");

        // Create sample test file
        var testDir = Path.Combine(_testDirectory, "tests");
        Directory.CreateDirectory(testDir);
        File.WriteAllText(Path.Combine(testDir, "CalculatorTests.cs"), @"
namespace SampleApp.Tests;

public class CalculatorTests
{
    // Test the add method
    public void TestAdd()
    {
        var calc = new Calculator();
        var result = calc.Add(1, 2);
    }
}
");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures
            }
        }
    }

    [Fact]
    public async Task RunAsync_MeasureCommand_DisplaysTableOutput()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(["measure", _testDirectory, "--no-color"], output, error);

        exitCode.Should().Be(0);
        var stdout = output.ToString();
        stdout.Should().Contain("Category");
        stdout.Should().Contain("Production");
        stdout.Should().Contain("Tests");
        stdout.Should().Contain("Total");
        stdout.Should().Contain("Estimator: default");
    }

    [Theory]
    [InlineData("qwen")]
    [InlineData("gemma")]
    public async Task RunAsync_MeasureCommand_WithEstimatorOption_AppliesEstimator(string estimator)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(["measure", _testDirectory, "--estimator", estimator, "--no-color"], output, error);

        exitCode.Should().Be(0);
        var stdout = output.ToString();
        stdout.Should().Contain($"Estimator: {estimator}");
    }

    [Fact]
    public async Task RunAsync_MeasureCommand_JsonFormat_OutputsValidJson()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(["measure", _testDirectory, "--format", "json"], output, error);

        exitCode.Should().Be(0);
        var json = output.ToString().Trim();
        var measurement = JsonSerializer.Deserialize<CodeMeasurementResult>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        measurement.Should().NotBeNull();
        measurement!.Production.FilesCount.Should().Be(1);
        measurement.Production.TotalTokens.Should().BeGreaterThan(0);
        measurement.Production.CommentTokens.Should().BeGreaterThan(0);
        measurement.Production.CodeTokens.Should().BeGreaterThan(0);

        measurement.Tests.FilesCount.Should().Be(1);
        measurement.Tests.TotalTokens.Should().BeGreaterThan(0);
        measurement.Tests.CommentTokens.Should().BeGreaterThan(0);
        measurement.Tests.CodeTokens.Should().BeGreaterThan(0);

        measurement.Total.FilesCount.Should().Be(2);
        measurement.Total.TotalTokens.Should().Be(measurement.Production.TotalTokens + measurement.Tests.TotalTokens);
        measurement.Files.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunAsync_MeasureCommand_WithOutputOption_WritesReportToFile()
    {
        var outputFile = Path.Combine(_testDirectory, "output", "tokens.json");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(["measure", _testDirectory, "--format", "json", "-o", outputFile], output, error);

        exitCode.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        var content = await File.ReadAllTextAsync(outputFile);
        content.Should().Contain("\"production\"");
        content.Should().Contain("\"tests\"");
        content.Should().Contain("\"total\"");
    }

    [Fact]
    public async Task RunAsync_MeasureCommand_WithExcludeTests_ExcludesTestFiles()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(["measure", _testDirectory, "--exclude-tests", "--format", "json"], output, error);

        exitCode.Should().Be(0);
        var json = output.ToString().Trim();
        var measurement = JsonSerializer.Deserialize<CodeMeasurementResult>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        measurement.Should().NotBeNull();
        measurement!.Production.FilesCount.Should().Be(1);
        measurement.Tests.FilesCount.Should().Be(0);
        measurement.Total.FilesCount.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_MeasureCommand_WithSarifFormat_ReturnsError()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(["measure", _testDirectory, "--format", "sarif"], output, error);

        exitCode.Should().Be(ExitCodes.Error);
        error.ToString().Should().Contain("SARIF report format is only supported for code analysis");
    }
}
