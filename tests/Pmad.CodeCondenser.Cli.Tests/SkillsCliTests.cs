using System.Text.Json;
using FluentAssertions;

namespace Pmad.CodeCondenser.Cli.Tests;

public sealed class SkillsCliTests : IDisposable
{
    private readonly string _tempDir;

    public SkillsCliTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pmad_skills_cli_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures
        }
    }

    [Fact]
    public async Task RunAsync_SkillsCommand_InstallsSkillsSuccessfully()
    {
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();

        var exitCode = await Program.RunAsync(["skills", _tempDir, "--no-color"], outWriter, errWriter);

        exitCode.Should().Be(0);
        errWriter.ToString().Should().BeEmpty();
        var output = outWriter.ToString();
        output.Should().Contain("[Created]");
        output.Should().Contain("codecondenser/SKILL.md");
        output.Should().Contain("Skills successfully synchronized");

        var skillFile = Path.Combine(_tempDir, ".agents", "skills", "codecondenser", "SKILL.md");
        File.Exists(skillFile).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_SkillsCommand_SecondRunReportsAlreadyUpToDate()
    {
        var outWriter1 = new StringWriter();
        var errWriter1 = new StringWriter();
        await Program.RunAsync(["skills", _tempDir, "--no-color"], outWriter1, errWriter1);

        var outWriter2 = new StringWriter();
        var errWriter2 = new StringWriter();
        var exitCode = await Program.RunAsync(["skills", _tempDir, "--no-color"], outWriter2, errWriter2);

        exitCode.Should().Be(0);
        var output = outWriter2.ToString();
        output.Should().Contain("[Up to date]");
        output.Should().Contain("Skills are already up to date");
    }

    [Fact]
    public async Task RunAsync_SkillsJsonFormat_OutputsValidJson()
    {
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();

        var exitCode = await Program.RunAsync(["skills", _tempDir, "--format", "json"], outWriter, errWriter);

        exitCode.Should().Be(0);
        var json = outWriter.ToString();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("created").GetInt32().Should().BeGreaterThan(0);
        root.GetProperty("files").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RunAsync_SkillsJsonToFile_WritesOutputFile()
    {
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();
        var reportPath = Path.Combine(_tempDir, "skills-report.json");

        var exitCode = await Program.RunAsync(["skills", _tempDir, "--format", "json", "-o", reportPath], outWriter, errWriter);

        exitCode.Should().Be(0);
        File.Exists(reportPath).Should().BeTrue();
        var json = File.ReadAllText(reportPath);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("created").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RunAsync_SkillsInvalidSkillName_ReturnsErrorCode()
    {
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();

        var exitCode = await Program.RunAsync(["skills", _tempDir, "--skill", "non-existent"], outWriter, errWriter);

        exitCode.Should().Be(ExitCodes.Error);
        errWriter.ToString().Should().Contain("Skill 'non-existent' not found");
    }

    [Fact]
    public async Task RunAsync_SkillsDryRun_DoesNotCreateFiles()
    {
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();

        var exitCode = await Program.RunAsync(["skills", _tempDir, "--dry-run", "--no-color"], outWriter, errWriter);

        exitCode.Should().Be(0);
        outWriter.ToString().Should().Contain("Dry run mode");
        Directory.Exists(Path.Combine(_tempDir, ".agents", "skills")).Should().BeFalse();
    }
}
