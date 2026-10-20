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
        settings.DuplicationStatementThreshold.Should().Be(8);
        settings.IncludeXmlDocumentation.Should().BeTrue();
        settings.MaxParallelLlmRequests.Should().Be(4);
        settings.Provider.Should().Be("Ollama");
        settings.Ollama.Should().NotBeNull();
        settings.Ollama.Endpoint.Should().Be("http://localhost:11434");
        settings.Ollama.Model.Should().Be("llama3.2:3b");
        settings.SnapshotFilePath.Should().Be(".codecondenser/snapshot.json");
    }
}
