using System.Text.Json;
using AwesomeAssertions;

namespace Pmad.CodeCondenser.Cli.Tests;

public class JsonSchemaValidationTests
{
    [Fact]
    public void SchemaFile_ExistsAndIsValidJson()
    {
        // Find repository root by walking up from current test execution directory
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? schemaPath = null;

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "schemas", "codecondenser-report.json");
            if (File.Exists(candidate))
            {
                schemaPath = candidate;
                break;
            }
            dir = dir.Parent;
        }

        schemaPath.Should().NotBeNull("schemas/codecondenser-report.json must exist in repository");

        var content = File.ReadAllText(schemaPath!);
        var doc = JsonDocument.Parse(content);

        doc.RootElement.GetProperty("$schema").GetString().Should().Contain("json-schema.org");
        doc.RootElement.GetProperty("title").GetString().Should().Be("CodeCondenserReport");
        doc.RootElement.GetProperty("properties").TryGetProperty("summary", out _).Should().BeTrue();
        doc.RootElement.GetProperty("properties").TryGetProperty("cloneClusters", out _).Should().BeTrue();
        doc.RootElement.GetProperty("properties").TryGetProperty("subMethodClusters", out _).Should().BeTrue();
        doc.RootElement.GetProperty("properties").TryGetProperty("verboseComments", out _).Should().BeTrue();
    }
}
