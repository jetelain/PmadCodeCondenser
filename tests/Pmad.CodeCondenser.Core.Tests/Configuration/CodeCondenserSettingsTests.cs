using FluentAssertions;
using Pmad.CodeCondenser.Core.Configuration;

namespace Pmad.CodeCondenser.Core.Tests.Configuration;

public class CodeCondenserSettingsTests
{
    [Fact]
    public void Defaults_ShouldMatchDocumentedValues()
    {
        var settings = new CodeCondenserSettings();

        settings.CommentLengthThreshold.Should().Be(100);
        settings.XmlSummaryLengthThreshold.Should().BeNull();
        settings.XmlRemarksLengthThreshold.Should().BeNull();
        settings.XmlParamLengthThreshold.Should().BeNull();
        settings.GetEffectiveXmlSummaryThreshold().Should().Be(100);
        settings.GetEffectiveXmlRemarksThreshold().Should().Be(100);
        settings.GetEffectiveXmlParamThreshold().Should().Be(50);
        settings.DuplicationStatementThreshold.Should().Be(8);
        settings.IncludeXmlDocumentation.Should().BeTrue();
        settings.MaxParallelLlmRequests.Should().Be(4);
        settings.Provider.Should().Be("Ollama");
        settings.Ollama.Should().NotBeNull();
        settings.Ollama.Endpoint.Should().Be("http://localhost:11434");
        settings.Ollama.Model.Should().Be("llama3.2:3b");
        settings.SnapshotFilePath.Should().Be(".codecondenser/snapshot.json");
    }

    [Fact]
    public void EffectiveXmlThresholds_WhenCustomValuesSet_ReturnsCustomValues()
    {
        var settings = new CodeCondenserSettings
        {
            CommentLengthThreshold = 120,
            XmlSummaryLengthThreshold = 80,
            XmlRemarksLengthThreshold = 60,
            XmlParamLengthThreshold = 30
        };

        settings.GetEffectiveXmlSummaryThreshold().Should().Be(80);
        settings.GetEffectiveXmlRemarksThreshold().Should().Be(60);
        settings.GetEffectiveXmlParamThreshold().Should().Be(30);
    }

    [Fact]
    public void EffectiveXmlRemarksThreshold_FallsBackToSummaryThreshold_WhenRemarksNotSet()
    {
        var settings = new CodeCondenserSettings
        {
            CommentLengthThreshold = 100,
            XmlSummaryLengthThreshold = 75
        };

        settings.GetEffectiveXmlRemarksThreshold().Should().Be(75);
    }

    [Fact]
    public void XmlSummaryRemarksLengthThreshold_AliasWorksBothWays()
    {
        var settings = new CodeCondenserSettings();
        settings.XmlSummaryRemarksLengthThreshold = 95;

        settings.XmlSummaryLengthThreshold.Should().Be(95);
        settings.XmlSummaryRemarksLengthThreshold.Should().Be(95);
        settings.GetEffectiveXmlSummaryThreshold().Should().Be(95);
        settings.GetEffectiveXmlRemarksThreshold().Should().Be(95);
    }
}
