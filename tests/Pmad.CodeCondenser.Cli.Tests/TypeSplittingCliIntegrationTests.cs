using System.Text.Json;
using AwesomeAssertions;
using Pmad.CodeCondenser.Cli.Formatting.Json;

namespace Pmad.CodeCondenser.Cli.Tests;

public class TypeSplittingCliIntegrationTests : IDisposable
{
    private readonly string _testDirectory;

    public TypeSplittingCliIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "TypeSplittingCli_" + Guid.NewGuid().ToString("N"));
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
    public async Task Report_FormatJson_DetectsMultiTypeFile()
    {
        var file = Path.Combine(_testDirectory, "Order.cs");
        await File.WriteAllTextAsync(file, """
        namespace Shop;

        public class Order
        {
            public int Id { get; set; }
        }

        public class OrderItem
        {
            public int ItemId { get; set; }
        }
        """);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["report", _testDirectory, "--format", "json", "--no-color"],
            output,
            error);

        exitCode.Should().Be(0);
        var json = output.ToString();
        var report = JsonSerializer.Deserialize<CodeCondenserJsonReport>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        report.Should().NotBeNull();
        report!.Summary.TypeFileIssuesCount.Should().Be(1);
        report.TypeFileIssues.Should().HaveCount(1);
        report.TypeFileIssues[0].Action.Should().Be("Split");
        report.TypeFileIssues[0].TypesToExtract.Should().ContainSingle(t => t.TypeName == "OrderItem");
    }

    [Fact]
    public async Task Analyze_NoTypeSplit_IgnoresMultiTypeFile()
    {
        var file = Path.Combine(_testDirectory, "Services.cs");
        await File.WriteAllTextAsync(file, """
        namespace App;

        public class ServiceA
        {
            public void Run() {}
        }

        public class ServiceB
        {
            public void Exec() {}
        }
        """);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--no-type-split", "--no-color"],
            output,
            error);

        exitCode.Should().Be(0);
        output.ToString().Should().NotContain("Single Type Per File");
    }

    [Fact]
    public async Task Analyze_DefaultTolerance_IgnoresOneLineDelegate()
    {
        var file = Path.Combine(_testDirectory, "Handler.cs");
        await File.WriteAllTextAsync(file, """
        namespace App;

        public delegate void SimpleCallback(int code);

        public class Handler
        {
            public void Execute(SimpleCallback cb) => cb(42);
        }
        """);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--no-color"],
            output,
            error);

        exitCode.Should().Be(0);
        output.ToString().Should().NotContain("Single Type Per File Violations");
    }

    [Fact]
    public async Task Apply_SplitsFileAndPreservesUsingsAndNamespace()
    {
        var file = Path.Combine(_testDirectory, "Order.cs");
        await File.WriteAllTextAsync(file, """
        using System;

        namespace Shop;

        public class Order
        {
            public int Id { get; set; }
        }

        public class OrderItem
        {
            public int ItemId { get; set; }
        }
        """);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["apply", _testDirectory, "--no-color"],
            output,
            error);

        exitCode.Should().Be(0);

        var primaryFile = Path.Combine(_testDirectory, "Order.cs");
        var extractedFile = Path.Combine(_testDirectory, "OrderItem.cs");

        File.Exists(primaryFile).Should().BeTrue();
        File.Exists(extractedFile).Should().BeTrue();

        var primaryContent = await File.ReadAllTextAsync(primaryFile);
        var extractedContent = await File.ReadAllTextAsync(extractedFile);

        primaryContent.Should().Contain("class Order");
        primaryContent.Should().NotContain("class OrderItem");
        primaryContent.Should().Contain("using System;");
        primaryContent.Should().Contain("namespace Shop;");

        extractedContent.Should().Contain("class OrderItem");
        extractedContent.Should().NotContain("class Order\r\n");
        extractedContent.Should().NotContain("class Order\n");
        extractedContent.Should().Contain("using System;");
        extractedContent.Should().Contain("namespace Shop;");
    }

    [Fact]
    public async Task Apply_RenamesFileWhenPrimaryTypeDoesNotMatchFileName()
    {
        var file = Path.Combine(_testDirectory, "OldName.cs");
        await File.WriteAllTextAsync(file, """
        namespace App;

        public class NewService
        {
            public void Run() {}
        }
        """);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["apply", _testDirectory, "--no-color"],
            output,
            error);

        exitCode.Should().Be(0);

        var oldFile = Path.Combine(_testDirectory, "OldName.cs");
        var renamedFile = Path.Combine(_testDirectory, "NewService.cs");

        File.Exists(oldFile).Should().BeFalse();
        File.Exists(renamedFile).Should().BeTrue();

        var content = await File.ReadAllTextAsync(renamedFile);
        content.Should().Contain("class NewService");
    }

    [Fact]
    public async Task Analyze_IgnoredTypeViaCommentMarker_IsSuppressed()
    {
        var file = Path.Combine(_testDirectory, "Suppressed.cs");
        await File.WriteAllTextAsync(file, """
        namespace App;

        public class Suppressed
        {
            public void Run() {}
        }

        // codecondenser:ignore-type-split
        public class ExtraClass
        {
            public void Do() {}
            public void Extra() {}
        }
        """);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["analyze", _testDirectory, "--no-color"],
            output,
            error);

        exitCode.Should().Be(0);
        output.ToString().Should().NotContain("Single Type Per File Violations");
    }

    [Fact]
    public async Task Apply_WhenDestinationFileAlreadyExists_DoesNotOverwriteExistingFile()
    {
        var existingFile = Path.Combine(_testDirectory, "NewService.cs");
        await File.WriteAllTextAsync(existingFile, "// Important existing file content");

        var sourceFile = Path.Combine(_testDirectory, "OldName.cs");
        await File.WriteAllTextAsync(sourceFile, """
        namespace App;

        public class NewService
        {
            public void Run() {}
        }
        """);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["apply", _testDirectory, "--no-color"],
            output,
            error);

        exitCode.Should().Be(0);

        // Existing file must be preserved
        var existingContent = await File.ReadAllTextAsync(existingFile);
        existingContent.Should().Be("// Important existing file content");

        // Source file must also be preserved (not deleted)
        File.Exists(sourceFile).Should().BeTrue();
    }

    [Fact]
    public async Task Apply_IncrementalMode_DoesNotFailWhenSplitsAreApplied()
    {
        var primaryFile = Path.Combine(_testDirectory, "Multi.cs");
        await File.WriteAllTextAsync(primaryFile, """
        namespace App;

        public class FirstService
        {
            public void Run() { }
        }

        public class SecondService
        {
            public void Help() { }
        }
        """);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(
            ["apply", _testDirectory, "--incremental", "-tt", "0", "--no-color"],
            output,
            error);

        exitCode.Should().Be(0);
        File.Exists(Path.Combine(_testDirectory, "SecondService.cs")).Should().BeTrue();
    }
}
