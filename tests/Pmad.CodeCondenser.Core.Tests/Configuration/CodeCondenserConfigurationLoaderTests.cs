using FluentAssertions;
using Pmad.CodeCondenser.Core.Configuration;

namespace Pmad.CodeCondenser.Core.Tests.Configuration;

public class CodeCondenserConfigurationLoaderTests : IDisposable
{
    private readonly string _tempDirectory;

    public CodeCondenserConfigurationLoaderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "CodeCondenserConfigTests_" + Guid.NewGuid().ToString("N"));
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
    public void Load_ReturnsDefaults_WhenFileDoesNotExist()
    {
        var settings = CodeCondenserConfigurationLoader.Load(Path.Combine(_tempDirectory, "nonexistent.json"));

        settings.Should().NotBeNull();
        settings.CommentLengthThreshold.Should().Be(100);
        settings.DuplicationStatementThreshold.Should().Be(8);
        settings.IncludeXmlDocumentation.Should().BeTrue();
        settings.MaxParallelLlmRequests.Should().Be(4);
        settings.Provider.Should().Be("Ollama");
        settings.Ollama.Endpoint.Should().Be("http://localhost:11434");
        settings.Ollama.Model.Should().Be("gemma4:e4b");
        settings.SnapshotFilePath.Should().Be(".codecondenser/snapshot.json");
    }

    [Fact]
    public void LoadFromString_ParsesValidJsonCorrectly()
    {
        var json = """
        {
            "commentLengthThreshold": 50,
            "duplicationStatementThreshold": 5,
            "includeXmlDocumentation": false,
            "maxParallelLlmRequests": 8,
            "provider": "CustomLlm",
            "ollama": {
                "endpoint": "http://remote:11434",
                "model": "deepseek-coder:6.7b",
                "contextSize": 16384
            },
            "snapshotFilePath": "custom-snapshot.json"
        }
        """;

        var settings = CodeCondenserConfigurationLoader.LoadFromString(json);

        settings.CommentLengthThreshold.Should().Be(50);
        settings.DuplicationStatementThreshold.Should().Be(5);
        settings.IncludeXmlDocumentation.Should().BeFalse();
        settings.MaxParallelLlmRequests.Should().Be(8);
        settings.Provider.Should().Be("CustomLlm");
        settings.Ollama.Endpoint.Should().Be("http://remote:11434");
        settings.Ollama.Model.Should().Be("deepseek-coder:6.7b");
        settings.Ollama.ContextSize.Should().Be(16384);
        settings.SnapshotFilePath.Should().Be("custom-snapshot.json");
    }

    [Fact]
    public void LoadFromString_ParsesXmlThresholdsCorrectly()
    {
        var json = """
        {
            "commentLengthThreshold": 90,
            "xmlSummaryLengthThreshold": 70,
            "xmlParamLengthThreshold": 35
        }
        """;

        var settings = CodeCondenserConfigurationLoader.LoadFromString(json);

        settings.CommentLengthThreshold.Should().Be(90);
        settings.XmlSummaryLengthThreshold.Should().Be(70);
        settings.XmlParamLengthThreshold.Should().Be(35);
        settings.GetEffectiveXmlSummaryThreshold().Should().Be(70);
        settings.GetEffectiveXmlRemarksThreshold().Should().Be(70);
        settings.GetEffectiveXmlParamThreshold().Should().Be(35);
    }

    [Fact]
    public void LoadFromString_ParsesXmlSummaryRemarksAliasCorrectly()
    {
        var json = """
        {
            "xmlSummaryRemarksLengthThreshold": 85
        }
        """;

        var settings = CodeCondenserConfigurationLoader.LoadFromString(json);

        settings.XmlSummaryLengthThreshold.Should().Be(85);
        settings.GetEffectiveXmlSummaryThreshold().Should().Be(85);
        settings.GetEffectiveXmlRemarksThreshold().Should().Be(85);
    }

    [Fact]
    public void Load_FindsDefaultFileInTargetDirectory()
    {
        var configPath = Path.Combine(_tempDirectory, CodeCondenserConfigurationLoader.DefaultConfigFileName);
        File.WriteAllText(configPath, """
        {
            "commentLengthThreshold": 120
        }
        """);

        var settings = CodeCondenserConfigurationLoader.Load(_tempDirectory);

        settings.CommentLengthThreshold.Should().Be(120);
        settings.DuplicationStatementThreshold.Should().Be(8); // Default retained
    }

    [Fact]
    public void LoadFromString_ReturnsDefault_WhenJsonIsInvalid()
    {
        var settings = CodeCondenserConfigurationLoader.LoadFromString("not-valid-json");

        settings.Should().NotBeNull();
        settings.CommentLengthThreshold.Should().Be(100);
    }

    [Fact]
    public void Load_WhenTargetPathIsSourceFile_FindsConfigFileInParentDirectory()
    {
        var configPath = Path.Combine(_tempDirectory, CodeCondenserConfigurationLoader.DefaultConfigFileName);
        File.WriteAllText(configPath, """
        {
            "commentLengthThreshold": 77
        }
        """);

        var sourceFile = Path.Combine(_tempDirectory, "MyService.cs");
        File.WriteAllText(sourceFile, "public class MyService {}");

        var settings = CodeCondenserConfigurationLoader.Load(sourceFile);

        settings.CommentLengthThreshold.Should().Be(77);
    }
}
