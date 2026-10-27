using AwesomeAssertions;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Filtering;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Tests.Filtering;

public class PathFilterTests : IDisposable
{
    private readonly string _tempDirectory;

    public PathFilterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PathFilterTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void BaseDirectory_ResolvesToRepositoryRoot_WhenMarkerPresentInParent()
    {
        var repoRoot = Path.Combine(_tempDirectory, "MyRepo");
        var srcDir = Path.Combine(repoRoot, "src", "SubProject");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(repoRoot, "MyRepo.sln"), "");

        var filter = new PathFilter(new CodeCondenserSettings(), srcDir);

        filter.BaseDirectory.Should().Be(Path.GetFullPath(repoRoot));
    }

    [Fact]
    public void ClassifyFile_CategorizesProductionTestAndGeneratedFiles()
    {
        var settings = new CodeCondenserSettings();
        var filter = new PathFilter(settings, _tempDirectory);

        var designerFile = Path.Combine(_tempDirectory, "Form1.Designer.cs");
        File.WriteAllText(designerFile, "class Form1 {}");

        var testFile = Path.Combine(_tempDirectory, "OrderTests.cs");
        File.WriteAllText(testFile, "class OrderTests {}");

        var prodFile = Path.Combine(_tempDirectory, "OrderService.cs");
        File.WriteAllText(prodFile, "class OrderService {}");

        filter.ClassifyFile("Form1.Designer.cs", designerFile).Should().Be(FileCategory.Generated);
        filter.ClassifyFile("OrderTests.cs", testFile).Should().Be(FileCategory.Test);
        filter.ClassifyFile("OrderService.cs", prodFile).Should().Be(FileCategory.Production);
    }

    [Fact]
    public void ShouldAnalyzeFile_ExcludesGeneratedFiles_ByDefault()
    {
        var settings = new CodeCondenserSettings();
        var filter = new PathFilter(settings, _tempDirectory);

        var genFile = Path.Combine(_tempDirectory, "20261019_Init.Designer.cs");
        File.WriteAllText(genFile, "class Init {}");

        var shouldAnalyze = filter.ShouldAnalyzeFile(
            "20261019_Init.Designer.cs",
            genFile,
            out var category,
            out var isSkippedGenerated);

        shouldAnalyze.Should().BeFalse();
        category.Should().Be(FileCategory.Generated);
        isSkippedGenerated.Should().BeTrue();
    }

    [Fact]
    public void ShouldAnalyzeFile_IncludesGeneratedFiles_WhenExcludeGeneratedCodeIsFalse()
    {
        var settings = new CodeCondenserSettings
        {
            ExcludeGeneratedCode = false
        };
        var filter = new PathFilter(settings, _tempDirectory);

        var genFile = Path.Combine(_tempDirectory, "20261019_Init.Designer.cs");
        File.WriteAllText(genFile, "class Init {}");

        var shouldAnalyze = filter.ShouldAnalyzeFile(
            "20261019_Init.Designer.cs",
            genFile,
            out var category,
            out var isSkippedGenerated);

        shouldAnalyze.Should().BeTrue();
        isSkippedGenerated.Should().BeFalse();
    }

    [Fact]
    public void ShouldAnalyzeFile_AppliesIncludeAndExcludePatterns()
    {
        var settings = new CodeCondenserSettings
        {
            Include = ["src/**"],
            Exclude = ["**/Ignored/**"]
        };
        var filter = new PathFilter(settings, _tempDirectory);

        // Under src, not ignored -> included
        filter.ShouldAnalyzeFile("src/Services/Order.cs", Path.Combine(_tempDirectory, "src/Services/Order.cs"), out _, out _)
            .Should().BeTrue();

        // Under src, but matching exclude -> excluded
        filter.ShouldAnalyzeFile("src/Ignored/Order.cs", Path.Combine(_tempDirectory, "src/Ignored/Order.cs"), out _, out _)
            .Should().BeFalse();

        // Not under src -> excluded
        filter.ShouldAnalyzeFile("samples/Demo.cs", Path.Combine(_tempDirectory, "samples/Demo.cs"), out _, out _)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldAnalyzeFile_ExcludesTests_WhenExcludeTestsIsTrue()
    {
        var settings = new CodeCondenserSettings
        {
            ExcludeTests = true
        };
        var filter = new PathFilter(settings, _tempDirectory);

        var testsSubDir = Path.Combine(_tempDirectory, "tests");
        var srcSubDir = Path.Combine(_tempDirectory, "src");
        Directory.CreateDirectory(testsSubDir);
        Directory.CreateDirectory(srcSubDir);

        var testPath = Path.Combine(testsSubDir, "UnitTests.cs");
        File.WriteAllText(testPath, "class UnitTests {}");

        var prodPath = Path.Combine(srcSubDir, "Service.cs");
        File.WriteAllText(prodPath, "class Service {}");

        filter.ShouldAnalyzeFile("tests/UnitTests.cs", testPath, out var testCat, out _)
            .Should().BeFalse();
        testCat.Should().Be(FileCategory.Test);

        filter.ShouldAnalyzeFile("src/Service.cs", prodPath, out var prodCat, out _)
            .Should().BeTrue();
        prodCat.Should().Be(FileCategory.Production);
    }

    [Fact]
    public void ShouldPruneDirectory_PrunesDefaultBuildAndToolingFolders()
    {
        var settings = new CodeCondenserSettings { UseDefaultExcludes = true };
        var filter = new PathFilter(settings, _tempDirectory);

        filter.ShouldPruneDirectory(Path.Combine(_tempDirectory, "bin")).Should().BeTrue();
        filter.ShouldPruneDirectory(Path.Combine(_tempDirectory, "obj")).Should().BeTrue();
        filter.ShouldPruneDirectory(Path.Combine(_tempDirectory, ".git")).Should().BeTrue();
        filter.ShouldPruneDirectory(Path.Combine(_tempDirectory, "node_modules")).Should().BeTrue();
        filter.ShouldPruneDirectory(Path.Combine(_tempDirectory, "src")).Should().BeFalse();
    }

    [Fact]
    public void ShouldPruneDirectory_PrunesUserExcludedDirectories()
    {
        var settings = new CodeCondenserSettings
        {
            Exclude = ["**/Legacy/**", "vendor/**"]
        };
        var filter = new PathFilter(settings, _tempDirectory);

        filter.ShouldPruneDirectory(Path.Combine(_tempDirectory, "vendor")).Should().BeTrue();
        filter.ShouldPruneDirectory(Path.Combine(_tempDirectory, "src", "Legacy")).Should().BeTrue();
        filter.ShouldPruneDirectory(Path.Combine(_tempDirectory, "src", "Modern")).Should().BeFalse();
    }

    [Fact]
    public void ShouldPruneDirectory_PrunesTopLevelOutsideIncludePrefix()
    {
        var settings = new CodeCondenserSettings
        {
            Include = ["src/**"]
        };
        var filter = new PathFilter(settings, _tempDirectory);

        filter.ShouldPruneDirectory(Path.Combine(_tempDirectory, "src")).Should().BeFalse();
        filter.ShouldPruneDirectory(Path.Combine(_tempDirectory, "docs")).Should().BeTrue();
        filter.ShouldPruneDirectory(Path.Combine(_tempDirectory, "benchmarks")).Should().BeTrue();
    }

    [Fact]
    public void ShouldPruneDirectory_DoesNotPruneWhenPatternStartsWithDotSlashOrBackslash()
    {
        var settingsDotSlash = new CodeCondenserSettings
        {
            Include = ["./src/**"]
        };
        var filterDotSlash = new PathFilter(settingsDotSlash, _tempDirectory);

        filterDotSlash.ShouldPruneDirectory(Path.Combine(_tempDirectory, "src")).Should().BeFalse();
        filterDotSlash.ShouldPruneDirectory(Path.Combine(_tempDirectory, "docs")).Should().BeTrue();

        var settingsBackslash = new CodeCondenserSettings
        {
            Include = [".\\src\\**"]
        };
        var filterBackslash = new PathFilter(settingsBackslash, _tempDirectory);

        filterBackslash.ShouldPruneDirectory(Path.Combine(_tempDirectory, "src")).Should().BeFalse();
        filterBackslash.ShouldPruneDirectory(Path.Combine(_tempDirectory, "docs")).Should().BeTrue();
    }

    [Fact]
    public void ShouldAnalyzeFile_AppliesPatternsWithDotSlashAndBackslashes()
    {
        var settings = new CodeCondenserSettings
        {
            Include = ["./src/**"],
            Exclude = [".\\src\\Ignored\\**"]
        };
        var filter = new PathFilter(settings, _tempDirectory);

        // Under src, not ignored -> included
        filter.ShouldAnalyzeFile("src/Services/Order.cs", Path.Combine(_tempDirectory, "src/Services/Order.cs"), out _, out _)
            .Should().BeTrue();

        // Under src, but matching exclude -> excluded
        filter.ShouldAnalyzeFile("src/Ignored/Order.cs", Path.Combine(_tempDirectory, "src/Ignored/Order.cs"), out _, out _)
            .Should().BeFalse();

        // Not under src -> excluded
        filter.ShouldAnalyzeFile("samples/Demo.cs", Path.Combine(_tempDirectory, "samples/Demo.cs"), out _, out _)
            .Should().BeFalse();
    }
}
