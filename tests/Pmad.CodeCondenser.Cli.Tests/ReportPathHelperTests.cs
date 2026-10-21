using FluentAssertions;
using Pmad.CodeCondenser.Cli.Formatting;

namespace Pmad.CodeCondenser.Cli.Tests;

public class ReportPathHelperTests
{
    [Theory]
    [InlineData("c:/repo/src/Services/OrderService.cs", "c:/repo", "src/Services/OrderService.cs")]
    [InlineData("c:\\repo\\src\\Services\\OrderService.cs", "c:\\repo", "src/Services/OrderService.cs")]
    [InlineData("c:\\repo\\src\\Services\\OrderService.cs", "c:/repo/", "src/Services/OrderService.cs")]
    [InlineData("/home/runner/work/repo/src/App.cs", "/home/runner/work/repo", "src/App.cs")]
    [InlineData("/home/runner/work/repo/src/App.cs", "/home/runner/work/repo/", "src/App.cs")]
    [InlineData("src/Services/OrderService.cs", "c:/repo", "src/Services/OrderService.cs")]
    [InlineData("./src/Services/OrderService.cs", "c:/repo", "src/Services/OrderService.cs")]
    [InlineData("/src/Services/OrderService.cs", "", "src/Services/OrderService.cs")]
    [InlineData("C:/REPO/SRC/FILE.CS", "c:/repo", "SRC/FILE.CS")]
    public void NormalizeRelativePath_ValidPaths_ReturnsNormalizedRelativePath(string path, string baseDir, string expected)
    {
        var result = ReportPathHelper.NormalizeRelativePath(path, baseDir);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "c:/repo")]
    [InlineData("", "c:/repo")]
    [InlineData("   ", "c:/repo")]
    public void NormalizeRelativePath_EmptyOrNullPath_ReturnsEmptyString(string? path, string baseDir)
    {
        var result = ReportPathHelper.NormalizeRelativePath(path!, baseDir);
        result.Should().BeEmpty();
    }

    [Fact]
    public void NormalizeRelativePath_PathEqualsBaseDirectory_ReturnsEmptyString()
    {
        var result = ReportPathHelper.NormalizeRelativePath("c:/repo", "c:/repo");
        result.Should().BeEmpty();
    }
}
