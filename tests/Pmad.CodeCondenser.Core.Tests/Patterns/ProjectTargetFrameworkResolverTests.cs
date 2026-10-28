using Pmad.CodeCondenser.Core.Patterns;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Patterns;

public class ProjectTargetFrameworkResolverTests
{
    [Fact]
    public void ParseTargetFrameworkFromXml_SingleTfm_ParsesCorrectly()
    {
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """;

        var ver = ProjectTargetFrameworkResolver.ParseTargetFrameworkFromXml(csproj);
        Assert.NotNull(ver);
        Assert.Equal(new Version(10, 0), ver);
    }

    [Fact]
    public void ParseTargetFrameworkFromXml_MultiTfm_ParsesFirst()
    {
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
              </PropertyGroup>
            </Project>
            """;

        var ver = ProjectTargetFrameworkResolver.ParseTargetFrameworkFromXml(csproj);
        Assert.NotNull(ver);
        Assert.Equal(new Version(8, 0), ver);
    }

    [Fact]
    public void ParseTargetFrameworkFromXml_WindowsSuffix_ParsesMajorMinor()
    {
        var csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0-windows</TargetFramework>
              </PropertyGroup>
            </Project>
            """;

        var ver = ProjectTargetFrameworkResolver.ParseTargetFrameworkFromXml(csproj);
        Assert.NotNull(ver);
        Assert.Equal(new Version(8, 0), ver);
    }

    [Theory]
    [InlineData("net48", 4, 8)]
    [InlineData("net472", 4, 7, 2)]
    [InlineData("net461", 4, 6, 1)]
    [InlineData("net45", 4, 5)]
    [InlineData("net40", 4, 0)]
    [InlineData("net35", 3, 5)]
    public void ParseTfmVersion_NetFramework_ParsesCorrectly(string tfm, int major, int minor, int build = -1)
    {
        var ver = ProjectTargetFrameworkResolver.ParseTfmVersion(tfm);
        Assert.NotNull(ver);
        var expected = build >= 0 ? new Version(major, minor, build) : new Version(major, minor);
        Assert.Equal(expected, ver);
    }

    [Theory]
    [InlineData("netstandard2.0", 2, 0)]
    [InlineData("netstandard2.1", 2, 1)]
    [InlineData("netcoreapp3.1", 3, 1)]
    [InlineData("net6.0", 6, 0)]
    [InlineData("net8.0-windows", 8, 0)]
    [InlineData("net10.0", 10, 0)]
    public void ParseTfmVersion_StandardAndModern_ParsesCorrectly(string tfm, int major, int minor)
    {
        var ver = ProjectTargetFrameworkResolver.ParseTfmVersion(tfm);
        Assert.NotNull(ver);
        Assert.Equal(new Version(major, minor), ver);
    }
}
