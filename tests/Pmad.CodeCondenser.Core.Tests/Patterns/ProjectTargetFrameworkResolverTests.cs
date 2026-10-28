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
}
