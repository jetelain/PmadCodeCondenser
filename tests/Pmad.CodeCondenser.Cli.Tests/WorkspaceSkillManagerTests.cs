using FluentAssertions;
using Pmad.CodeCondenser.Cli.Skills;

namespace Pmad.CodeCondenser.Cli.Tests;

public sealed class WorkspaceSkillManagerTests : IDisposable
{
    private readonly string _tempDir;

    public WorkspaceSkillManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "pmad_skills_test_" + Guid.NewGuid().ToString("N"));
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
    public void GetAvailableSkillNames_ContainsCodecondenser()
    {
        var skills = WorkspaceSkillManager.GetAvailableSkillNames();

        skills.Should().Contain("codecondenser");
    }

    [Fact]
    public void ResolveTargetSkillsDirectory_ExplicitOverride_ReturnsSpecifiedDirectory()
    {
        var custom = Path.Combine(_tempDir, "custom", "skills");
        var resolved = WorkspaceSkillManager.ResolveTargetSkillsDirectory(_tempDir, custom);

        resolved.Should().Be(Path.GetFullPath(custom));
    }

    [Fact]
    public void ResolveTargetSkillsDirectory_PathAlreadyPointsToAgentsSkills_ReturnsSamePath()
    {
        var target = Path.Combine(_tempDir, ".agents", "skills");
        var resolved = WorkspaceSkillManager.ResolveTargetSkillsDirectory(target);

        resolved.Should().Be(Path.GetFullPath(target));
    }

    [Fact]
    public void ResolveTargetSkillsDirectory_WorkspaceRoot_AppendsAgentsSkills()
    {
        var resolved = WorkspaceSkillManager.ResolveTargetSkillsDirectory(_tempDir);

        resolved.Should().Be(Path.Combine(Path.GetFullPath(_tempDir), ".agents", "skills"));
    }

    [Fact]
    public void InstallSkills_EmptyDirectory_InstallsAllFilesWithCreatedStatus()
    {
        var targetDir = Path.Combine(_tempDir, ".agents", "skills");

        var result = WorkspaceSkillManager.InstallSkills(targetDir);

        result.CreatedCount.Should().BeGreaterThan(0);
        result.UpdatedCount.Should().Be(0);
        result.UpToDateCount.Should().Be(0);
        result.RemovedCount.Should().Be(0);
        result.IsAllUpToDate.Should().BeFalse();

        var skillMd = Path.Combine(targetDir, "codecondenser", "SKILL.md");
        File.Exists(skillMd).Should().BeTrue();
        var content = File.ReadAllText(skillMd);
        content.Should().Contain("Pmad.CodeCondenser");
    }

    [Fact]
    public void InstallSkills_SecondRun_ReportsAllUpToDate()
    {
        var targetDir = Path.Combine(_tempDir, ".agents", "skills");

        var firstResult = WorkspaceSkillManager.InstallSkills(targetDir);
        firstResult.CreatedCount.Should().BeGreaterThan(0);

        var secondResult = WorkspaceSkillManager.InstallSkills(targetDir);

        secondResult.CreatedCount.Should().Be(0);
        secondResult.UpdatedCount.Should().Be(0);
        secondResult.UpToDateCount.Should().Be(firstResult.CreatedCount);
        secondResult.RemovedCount.Should().Be(0);
        secondResult.IsAllUpToDate.Should().BeTrue();
    }

    [Fact]
    public void InstallSkills_ModifiedFile_OverwritesAndReportsUpdated()
    {
        var targetDir = Path.Combine(_tempDir, ".agents", "skills");
        WorkspaceSkillManager.InstallSkills(targetDir);

        var skillMd = Path.Combine(targetDir, "codecondenser", "SKILL.md");
        File.WriteAllText(skillMd, "# User modified content that is different");

        var result = WorkspaceSkillManager.InstallSkills(targetDir);

        result.CreatedCount.Should().Be(0);
        result.UpdatedCount.Should().Be(1);
        result.UpToDateCount.Should().BeGreaterThan(0);
        result.IsAllUpToDate.Should().BeFalse();

        File.ReadAllText(skillMd).Should().Contain("Pmad.CodeCondenser");
    }

    [Fact]
    public void InstallSkills_Force_RewritesAllFiles()
    {
        var targetDir = Path.Combine(_tempDir, ".agents", "skills");
        var firstResult = WorkspaceSkillManager.InstallSkills(targetDir);

        var forceResult = WorkspaceSkillManager.InstallSkills(targetDir, new SkillInstallOptions { Force = true });

        forceResult.CreatedCount.Should().Be(0);
        forceResult.UpdatedCount.Should().Be(firstResult.CreatedCount);
        forceResult.UpToDateCount.Should().Be(0);
        forceResult.IsAllUpToDate.Should().BeFalse();
    }

    [Fact]
    public void InstallSkills_DryRun_DoesNotCreateFilesOnDisk()
    {
        var targetDir = Path.Combine(_tempDir, ".agents", "skills");

        var result = WorkspaceSkillManager.InstallSkills(targetDir, new SkillInstallOptions { DryRun = true });

        result.CreatedCount.Should().BeGreaterThan(0);
        Directory.Exists(targetDir).Should().BeFalse();
    }

    [Fact]
    public void InstallSkills_Clean_RemovesObsoleteFiles()
    {
        var targetDir = Path.Combine(_tempDir, ".agents", "skills");
        WorkspaceSkillManager.InstallSkills(targetDir);

        var obsoleteFile = Path.Combine(targetDir, "codecondenser", "obsolete-guide.md");
        File.WriteAllText(obsoleteFile, "This is an obsolete file.");

        var cleanResult = WorkspaceSkillManager.InstallSkills(targetDir, new SkillInstallOptions { Clean = true });

        cleanResult.RemovedCount.Should().Be(1);
        cleanResult.Files.Should().Contain(f => f.Status == SkillFileStatus.Removed && f.RelativePath.Contains("obsolete-guide.md"));
        File.Exists(obsoleteFile).Should().BeFalse();
    }

    [Fact]
    public void InstallSkills_FilterBySkillName_OnlyDeploysMatchingSkill()
    {
        var targetDir = Path.Combine(_tempDir, ".agents", "skills");

        var result = WorkspaceSkillManager.InstallSkills(targetDir, new SkillInstallOptions { SkillName = "codecondenser" });

        result.CreatedCount.Should().BeGreaterThan(0);
        result.Files.Should().OnlyContain(f => f.RelativePath.StartsWith("codecondenser/"));
    }

    [Fact]
    public void InstallSkills_FilterByNonExistentSkill_InstallsNothing()
    {
        var targetDir = Path.Combine(_tempDir, ".agents", "skills");

        var result = WorkspaceSkillManager.InstallSkills(targetDir, new SkillInstallOptions { SkillName = "non-existent-skill" });

        result.CreatedCount.Should().Be(0);
        result.UpdatedCount.Should().Be(0);
        result.UpToDateCount.Should().Be(0);
        result.Files.Should().BeEmpty();
    }
}
