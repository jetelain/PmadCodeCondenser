using AwesomeAssertions;
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
        settings.Ollama.Model.Should().Be("gemma4:e4b");
        settings.Ollama.ContextSize.Should().Be(24576);
        settings.SnapshotFilePath.Should().Be(".codecondenser/snapshot.json");
        settings.EnableRepeatedStringDetection.Should().BeTrue();
        settings.RepeatedStringLengthThreshold.Should().Be(100);
        settings.ExistingConstantStringLengthThreshold.Should().Be(20);
        settings.RepeatedStringMinOccurrences.Should().Be(2);
        settings.RepeatedStringTolerance.Should().BeNull();
        settings.RepeatedStringExitCode.Should().Be(2);
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

    [Fact]
    public void GetEffectiveSummaryMode_DefaultsToReport_WhenNoLlmConfigured()
    {
        var settings = new CodeCondenserSettings();
        settings.IsLlmConfigured.Should().BeFalse();
        settings.GetEffectiveSummaryMode().Should().Be(SummaryMode.Report);
    }

    [Fact]
    public void GetEffectiveSummaryMode_DefaultsToSummarize_WhenLlmIsConfigured()
    {
        var settings = new CodeCondenserSettings { IsLlmConfigured = true };
        settings.GetEffectiveSummaryMode().Should().Be(SummaryMode.Summarize);
    }

    [Fact]
    public void GetEffectiveSummaryMode_ReturnsDisabled_WhenCommentsDisabled()
    {
        var settings = new CodeCondenserSettings { EnableCommentSummarization = false };
        settings.GetEffectiveSummaryMode().Should().Be(SummaryMode.Disabled);
    }

    [Fact]
    public void SummaryMode_ExplicitOverride_TakesPrecedence()
    {
        var settings = new CodeCondenserSettings
        {
            IsLlmConfigured = true,
            SummaryMode = SummaryMode.Report
        };
        settings.GetEffectiveSummaryMode().Should().Be(SummaryMode.Report);

        settings.SummaryMode = SummaryMode.Disabled;
        settings.EnableCommentSummarization.Should().BeFalse();
        settings.GetEffectiveSummaryMode().Should().Be(SummaryMode.Disabled);

        settings.Summary = SummaryMode.Summarize;
        settings.EnableCommentSummarization.Should().BeTrue();
        settings.GetEffectiveSummaryMode().Should().Be(SummaryMode.Summarize);
    }

    [Fact]
    public void Clone_CopiesSummaryModeAndCommentTolerance()
    {
        var original = new CodeCondenserSettings
        {
            SummaryMode = SummaryMode.Report,
            IsLlmConfigured = true,
            CommentTolerance = 3,
            CommentExitCode = 4
        };

        var cloned = original.Clone();

        cloned.SummaryMode.Should().Be(SummaryMode.Report);
        cloned.IsLlmConfigured.Should().BeTrue();
        cloned.CommentTolerance.Should().Be(3);
        cloned.CommentExitCode.Should().Be(4);
    }

    [Fact]
    public void GetEffectiveSummaryMode_WhenIsLlmConfiguredGlobally_DefaultsToReport()
    {
        var settings = new CodeCondenserSettings
        {
            IsLlmConfigured = true,
            IsLlmConfiguredGlobally = true
        };

        settings.GetEffectiveSummaryMode().Should().Be(SummaryMode.Report);
    }

    [Fact]
    public void Clone_CopiesIsLlmConfiguredGlobally()
    {
        var original = new CodeCondenserSettings
        {
            IsLlmConfigured = true,
            IsLlmConfiguredGlobally = true
        };

        var cloned = original.Clone();
        cloned.IsLlmConfiguredGlobally.Should().BeTrue();
    }

    [Fact]
    public void MaxSummarizationRetries_DefaultsToOne()
    {
        var settings = new CodeCondenserSettings();
        settings.MaxSummarizationRetries.Should().Be(1);
    }

    [Fact]
    public void SummarizationRetries_Alias_SetsAndGetsMaxSummarizationRetries()
    {
        var settings = new CodeCondenserSettings();
        settings.SummarizationRetries = 3;

        settings.MaxSummarizationRetries.Should().Be(3);
        settings.SummarizationRetries.Should().Be(3);
    }

    [Fact]
    public void Clone_CopiesMaxSummarizationRetries()
    {
        var original = new CodeCondenserSettings
        {
            MaxSummarizationRetries = 4
        };

        var cloned = original.Clone();
        cloned.MaxSummarizationRetries.Should().Be(4);
    }

    [Fact]
    public void ExistingConstantLengthThreshold_AliasesWorkCorrectly()
    {
        var settings = new CodeCondenserSettings();
        settings.ExistingConstantLengthThreshold = 25;
        settings.ExistingConstantStringLengthThreshold.Should().Be(25);
        settings.ExistingConstantLengthThreshold.Should().Be(25);

        settings.ThresholdExistingConstantLength = 30;
        settings.ExistingConstantStringLengthThreshold.Should().Be(30);

        settings.ThresholdExistingConstants = 15;
        settings.ExistingConstantStringLengthThreshold.Should().Be(15);
    }

    [Fact]
    public void Clone_CopiesExistingConstantStringLengthThreshold()
    {
        var original = new CodeCondenserSettings
        {
            ExistingConstantStringLengthThreshold = 35
        };

        var cloned = original.Clone();
        cloned.ExistingConstantStringLengthThreshold.Should().Be(35);
    }
}
