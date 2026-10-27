using AwesomeAssertions;
using Pmad.CodeCondenser.Core.Clones;
using Pmad.CodeCondenser.Core.Engine;
using Pmad.CodeCondenser.Infrastructure.Storage;

namespace Pmad.CodeCondenser.Cli.Tests;

public class PathFilteringCliIntegrationTests : IDisposable
{
    private readonly string _testDirectory;

    public PathFilteringCliIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "PathFilterCli_" + Guid.NewGuid().ToString("N"));
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
    public async Task RunAsync_SkipsGeneratedDesignerAndMigrationFiles_ByDefault()
    {
        var migrationsDir = Path.Combine(_testDirectory, "Migrations");
        Directory.CreateDirectory(migrationsDir);

        // Identical duplicate code in generated migration designer and model snapshot
        var duplicateMethodBody = """
        int a = 1; int b = 2; int c = 3; int d = 4;
        int e = 5; int f = 6; int g = 7; int h = 8;
        """;

        var file1 = Path.Combine(migrationsDir, "20261019_InitialCreate.Designer.cs");
        await File.WriteAllTextAsync(file1, $$"""
        namespace App.Migrations;
        public class InitialCreate
        {
            public void BuildTargetModel()
            {
                {{duplicateMethodBody}}
            }
        }
        """);

        var file2 = Path.Combine(migrationsDir, "AppDbContextModelSnapshot.cs");
        await File.WriteAllTextAsync(file2, $$"""
        namespace App.Migrations;
        public class AppDbContextModelSnapshot
        {
            public void BuildModel()
            {
                {{duplicateMethodBody}}
            }
        }
        """);

        var engine = new CodeCondenserEngine(
            cloneDetector: new CloneDetector(),
            snapshotStore: new JsonSnapshotStore());

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act - Default behavior excludes generated code
        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--no-color", "--no-comments"],
            output,
            error,
            customEngine: engine);

        exitCode.Should().Be(0);
        var outputText = output.ToString();
        outputText.Should().Contain("No structural code duplication detected.");
        outputText.Should().Contain("Skipped 2 auto-generated file(s).");
    }

    [Fact]
    public async Task RunAsync_IncludesGeneratedFiles_WhenIncludeGeneratedSpecified()
    {
        var migrationsDir = Path.Combine(_testDirectory, "Migrations");
        Directory.CreateDirectory(migrationsDir);

        var duplicateMethodBody = """
        int a = 1; int b = 2; int c = 3; int d = 4;
        int e = 5; int f = 6; int g = 7; int h = 8;
        """;

        var file1 = Path.Combine(migrationsDir, "InitialCreate.Designer.cs");
        await File.WriteAllTextAsync(file1, $$"""
        namespace App.Migrations;
        public class InitialCreate
        {
            public void BuildTargetModel()
            {
                {{duplicateMethodBody}}
            }
        }
        """);

        var file2 = Path.Combine(migrationsDir, "AppModelSnapshot.cs");
        await File.WriteAllTextAsync(file2, $$"""
        namespace App.Migrations;
        public class AppModelSnapshot
        {
            public void BuildModel()
            {
                {{duplicateMethodBody}}
            }
        }
        """);

        var engine = new CodeCondenserEngine(
            cloneDetector: new CloneDetector(),
            snapshotStore: new JsonSnapshotStore());

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act - Explicitly include generated code
        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--include-generated", "--no-color", "--no-comments"],
            output,
            error,
            customEngine: engine);

        exitCode.Should().Be(0);
        var outputText = output.ToString();
        outputText.Should().Contain("Production Duplication Cluster");
        outputText.Should().NotContain("Skipped");
    }

    [Fact]
    public async Task RunAsync_TestDuplication_DoesNotFailCiGate_ByDefault()
    {
        var testsDir = Path.Combine(_testDirectory, "tests");
        Directory.CreateDirectory(testsDir);

        var duplicateMethodBody = """
        int a = 1; int b = 2; int c = 3; int d = 4;
        int e = 5; int f = 6; int g = 7; int h = 8;
        """;

        var testFile1 = Path.Combine(testsDir, "OrderTests.cs");
        await File.WriteAllTextAsync(testFile1, $$"""
        namespace App.Tests;
        public class OrderTests
        {
            public void TestMethodA()
            {
                {{duplicateMethodBody}}
            }
        }
        """);

        var testFile2 = Path.Combine(testsDir, "InvoiceTests.cs");
        await File.WriteAllTextAsync(testFile2, $$"""
        namespace App.Tests;
        public class InvoiceTests
        {
            public void TestMethodB()
            {
                {{duplicateMethodBody}}
            }
        }
        """);

        var store = new JsonSnapshotStore();
        var engine = new CodeCondenserEngine(snapshotStore: store);

        // Act: Run incremental mode with zero tolerance. Test duplication should NOT trigger exit code 2 by default
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--incremental", "--duplication-tolerance", "0", "--no-color", "--no-comments"],
            output,
            error,
            customEngine: engine);

        exitCode.Should().Be(0);
        var outputText = output.ToString();
        outputText.Should().Contain("Found 1 Test Duplication Cluster(s) (Review for DAMP vs DRY):");
        outputText.Should().Contain("Test Clones:       1");
    }

    [Fact]
    public async Task RunAsync_TestDuplication_FailsCiGate_WhenEnforceTestDuplicationSet()
    {
        var testsDir = Path.Combine(_testDirectory, "tests");
        Directory.CreateDirectory(testsDir);

        var duplicateMethodBody = """
        int a = 1; int b = 2; int c = 3; int d = 4;
        int e = 5; int f = 6; int g = 7; int h = 8;
        """;

        var testFile1 = Path.Combine(testsDir, "OrderTests.cs");
        await File.WriteAllTextAsync(testFile1, $$"""
        namespace App.Tests;
        public class OrderTests
        {
            public void TestMethodA()
            {
                {{duplicateMethodBody}}
            }
        }
        """);

        var testFile2 = Path.Combine(testsDir, "InvoiceTests.cs");
        await File.WriteAllTextAsync(testFile2, $$"""
        namespace App.Tests;
        public class InvoiceTests
        {
            public void TestMethodB()
            {
                {{duplicateMethodBody}}
            }
        }
        """);

        var store = new JsonSnapshotStore();
        var engine = new CodeCondenserEngine(snapshotStore: store);

        // Act: Run incremental mode with --enforce-test-duplication and zero tolerance. Should fail with exit code 2
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--incremental", "--duplication-tolerance", "0", "--enforce-test-duplication", "--no-color", "--no-comments"],
            output,
            error,
            customEngine: engine);

        exitCode.Should().Be(2);
        var outputText = output.ToString();
        outputText.Should().Contain("[AI AGENT ACTION REQUIRED]");
    }

    [Fact]
    public async Task RunAsync_ExcludeTestsFlag_SkipsTestFilesCompletely()
    {
        var testsDir = Path.Combine(_testDirectory, "tests");
        Directory.CreateDirectory(testsDir);

        var srcDir = Path.Combine(_testDirectory, "src");
        Directory.CreateDirectory(srcDir);

        await File.WriteAllTextAsync(Path.Combine(testsDir, "OrderTests.cs"), "public class OrderTests {}");
        await File.WriteAllTextAsync(Path.Combine(srcDir, "OrderService.cs"), "public class OrderService {}");

        var engine = new CodeCondenserEngine();

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--exclude-tests", "--no-color", "--no-comments"],
            output,
            error,
            customEngine: engine);

        exitCode.Should().Be(0);
        var outputText = output.ToString();
        outputText.Should().Contain("Files Analyzed:    1 (Production: 1)");
    }

    [Fact]
    public async Task RunAsync_NoColorEnvironmentVariable_SuppressesAnsiEscapeSequences()
    {
        var srcDir = Path.Combine(_testDirectory, "src");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(Path.Combine(srcDir, "Sample.cs"), "public class Sample {}");

        var prevNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        try
        {
            Environment.SetEnvironmentVariable("NO_COLOR", "1");

            var engine = new CodeCondenserEngine();
            using var output = new StringWriter();
            using var error = new StringWriter();

            // Run WITHOUT --no-color flag; NO_COLOR env var should suppress ANSI
            var exitCode = await Program.RunAsync(
                ["analyze", _testDirectory, "--no-comments"],
                output,
                error,
                customEngine: engine);

            exitCode.Should().Be(0);
            output.ToString().Should().NotContain("\u001b[");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_COLOR", prevNoColor);
        }
    }
}
