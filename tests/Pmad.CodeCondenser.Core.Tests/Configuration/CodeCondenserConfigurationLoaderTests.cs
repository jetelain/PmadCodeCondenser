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
        var settings = CodeCondenserConfigurationLoader.Load(
            Path.Combine(_tempDirectory, "nonexistent.json"),
            disableGlobalConfig: true);

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

    [Fact]
    public void LoadFromString_ParsesFeatureFlagsAndDuplicationTolerance()
    {
        var json = """
        {
            "enableCommentSummarization": false,
            "enableDuplicationDetection": false,
            "duplicationTolerance": 3,
            "duplicationExitCode": 42
        }
        """;

        var settings = CodeCondenserConfigurationLoader.LoadFromString(json);

        settings.EnableCommentSummarization.Should().BeFalse();
        settings.EnableDuplicationDetection.Should().BeFalse();
        settings.DuplicationTolerance.Should().Be(3);
        settings.DuplicationExitCode.Should().Be(42);
    }

    [Fact]
    public void LoadFromString_ParsesFeatureFlagAliases()
    {
        var json = """
        {
            "enableComments": false,
            "enableCodeDuplication": false,
            "incrementalDuplicationTolerance": 5
        }
        """;

        var settings = CodeCondenserConfigurationLoader.LoadFromString(json);

        settings.EnableCommentSummarization.Should().BeFalse();
        settings.EnableDuplicationDetection.Should().BeFalse();
        settings.DuplicationTolerance.Should().Be(5);
    }

    [Fact]
    public void LoadFromString_ParsesPathFilteringAndTestIsolationSettings()
    {
        var json = """
        {
            "include": ["src/**", "lib/**"],
            "exclude": ["**/Migrations/**", "**/bin/**"],
            "useDefaultExcludes": false,
            "excludeTests": true,
            "enforceTestDuplication": true,
            "testPaths": ["specs/**", "**/*Spec.cs"],
            "excludeGeneratedCode": false,
            "generatedCodePatterns": ["**/*.designer.cs"]
        }
        """;

        var settings = CodeCondenserConfigurationLoader.LoadFromString(json);

        settings.Include.Should().ContainInOrder("src/**", "lib/**");
        settings.Exclude.Should().ContainInOrder("**/Migrations/**", "**/bin/**");
        settings.UseDefaultExcludes.Should().BeFalse();
        settings.ExcludeTests.Should().BeTrue();
        settings.EnforceTestDuplication.Should().BeTrue();
        settings.TestPaths.Should().ContainInOrder("specs/**", "**/*Spec.cs");
        settings.ExcludeGeneratedCode.Should().BeFalse();
        settings.GeneratedCodePatterns.Should().ContainInOrder("**/*.designer.cs");
    }

    [Fact]
    public void Load_MergesCodeCondenserIgnoreFile()
    {
        var ignorePath = Path.Combine(_tempDirectory, CodeCondenserConfigurationLoader.DefaultIgnoreFileName);
        File.WriteAllText(ignorePath, """
        # Comment line to ignore
        
        **/Generated/**
        # Another comment
        src/Legacy/**
        """);

        var configPath = Path.Combine(_tempDirectory, CodeCondenserConfigurationLoader.DefaultConfigFileName);
        File.WriteAllText(configPath, """
        {
            "exclude": ["**/CustomExclude/**"]
        }
        """);

        var settings = CodeCondenserConfigurationLoader.Load(_tempDirectory);

        settings.Exclude.Should().Contain("**/CustomExclude/**");
        settings.Exclude.Should().Contain("**/Generated/**");
        settings.Exclude.Should().Contain("src/Legacy/**");
        settings.Exclude.Should().NotContain(x => x.StartsWith("#"));
    }

    [Fact]
    public void LoadFromString_ParsesSummaryModeCorrectly()
    {
        var json = """
        {
            "summaryMode": "Report"
        }
        """;
        var settings = CodeCondenserConfigurationLoader.LoadFromString(json);
        settings.SummaryMode.Should().Be(SummaryMode.Report);
        settings.GetEffectiveSummaryMode().Should().Be(SummaryMode.Report);
    }

    [Fact]
    public void LoadFromString_ParsesSummaryAliasCorrectly()
    {
        var json = """
        {
            "summary": "Disabled"
        }
        """;
        var settings = CodeCondenserConfigurationLoader.LoadFromString(json);
        settings.SummaryMode.Should().Be(SummaryMode.Disabled);
        settings.EnableCommentSummarization.Should().BeFalse();
        settings.GetEffectiveSummaryMode().Should().Be(SummaryMode.Disabled);
    }

    [Fact]
    public void LoadFromString_DetectsLlmConfiguration()
    {
        var jsonWithOllama = """
        {
            "ollama": { "model": "qwen3.5:4b" }
        }
        """;
        var settingsWithOllama = CodeCondenserConfigurationLoader.LoadFromString(jsonWithOllama);
        settingsWithOllama.IsLlmConfigured.Should().BeTrue();
        settingsWithOllama.GetEffectiveSummaryMode().Should().Be(SummaryMode.Summarize);

        var jsonWithoutLlm = """
        {
            "duplicationStatementThreshold": 12
        }
        """;
        var settingsWithoutLlm = CodeCondenserConfigurationLoader.LoadFromString(jsonWithoutLlm);
        settingsWithoutLlm.IsLlmConfigured.Should().BeFalse();
        settingsWithoutLlm.GetEffectiveSummaryMode().Should().Be(SummaryMode.Report);
    }

    [Fact]
    public void ResolveUserProfileConfigFile_FindsDefaultCodeCondenserJson()
    {
        var userHome = Path.Combine(_tempDirectory, "UserHome1");
        var configDir = Path.Combine(userHome, ".codecondenser");
        Directory.CreateDirectory(configDir);
        var configFile = Path.Combine(configDir, "codecondenser.json");
        File.WriteAllText(configFile, "{}");

        var resolved = CodeCondenserConfigurationLoader.ResolveUserProfileConfigFile(userHome);
        resolved.Should().NotBeNull();
        resolved.Should().Be(Path.GetFullPath(configFile));
    }

    [Fact]
    public void ResolveUserProfileConfigFile_FindsConfigJson()
    {
        var userHome = Path.Combine(_tempDirectory, "UserHome2");
        var configDir = Path.Combine(userHome, ".codecondenser");
        Directory.CreateDirectory(configDir);
        var configFile = Path.Combine(configDir, "config.json");
        File.WriteAllText(configFile, "{}");

        var resolved = CodeCondenserConfigurationLoader.ResolveUserProfileConfigFile(userHome);
        resolved.Should().NotBeNull();
        resolved.Should().Be(Path.GetFullPath(configFile));
    }

    [Fact]
    public void ResolveUserProfileConfigFile_FindsDotCodeCondenserJson()
    {
        var userHome = Path.Combine(_tempDirectory, "UserHome3");
        Directory.CreateDirectory(userHome);
        var configFile = Path.Combine(userHome, ".codecondenser.json");
        File.WriteAllText(configFile, "{}");

        var resolved = CodeCondenserConfigurationLoader.ResolveUserProfileConfigFile(userHome);
        resolved.Should().NotBeNull();
        resolved.Should().Be(Path.GetFullPath(configFile));
    }

    [Fact]
    public void ResolveUserProfileConfigFile_ReturnsNull_WhenDirectoryEmpty()
    {
        var userHome = Path.Combine(_tempDirectory, "EmptyUserHome");
        Directory.CreateDirectory(userHome);

        var resolved = CodeCondenserConfigurationLoader.ResolveUserProfileConfigFile(userHome);
        resolved.Should().BeNull();
    }

    [Fact]
    public void ResolveUserProfileConfigFile_FindsAppDataCodeCondenserJson()
    {
        var appData = Path.Combine(_tempDirectory, "AppData1");
        var configDir = Path.Combine(appData, "CodeCondenser");
        Directory.CreateDirectory(configDir);
        var configFile = Path.Combine(configDir, "codecondenser.json");
        File.WriteAllText(configFile, "{}");

        var resolved = CodeCondenserConfigurationLoader.ResolveUserProfileConfigFile(appData);
        resolved.Should().NotBeNull();
        resolved.Should().Be(Path.GetFullPath(configFile));
    }

    [Fact]
    public void ResolveUserProfileConfigFile_FindsAppDataConfigJson()
    {
        var appData = Path.Combine(_tempDirectory, "AppData2");
        var configDir = Path.Combine(appData, "CodeCondenser");
        Directory.CreateDirectory(configDir);
        var configFile = Path.Combine(configDir, "config.json");
        File.WriteAllText(configFile, "{}");

        var resolved = CodeCondenserConfigurationLoader.ResolveUserProfileConfigFile(appData);
        resolved.Should().NotBeNull();
        resolved.Should().Be(Path.GetFullPath(configFile));
    }

    [Fact]
    public void ResolveUserProfileConfigFile_FindsPmadCodeCondenserJson()
    {
        var appData = Path.Combine(_tempDirectory, "AppData3");
        var configDir = Path.Combine(appData, "pmad-codecondenser");
        Directory.CreateDirectory(configDir);
        var configFile = Path.Combine(configDir, "codecondenser.json");
        File.WriteAllText(configFile, "{}");

        var resolved = CodeCondenserConfigurationLoader.ResolveUserProfileConfigFile(appData);
        resolved.Should().NotBeNull();
        resolved.Should().Be(Path.GetFullPath(configFile));
    }

    [Fact]
    public void ResolveUserProfileConfigFile_PrefersAppDataOverUserProfile_InCustomDir()
    {
        var customDir = Path.Combine(_tempDirectory, "CustomCombinedDir");
        var appDataConfigDir = Path.Combine(customDir, "CodeCondenser");
        var userProfileConfigDir = Path.Combine(customDir, ".codecondenser");
        Directory.CreateDirectory(appDataConfigDir);
        Directory.CreateDirectory(userProfileConfigDir);

        var appDataConfig = Path.Combine(appDataConfigDir, "codecondenser.json");
        var userProfileConfig = Path.Combine(userProfileConfigDir, "codecondenser.json");
        File.WriteAllText(appDataConfig, "{\"maxParallelLlmRequests\": 9}");
        File.WriteAllText(userProfileConfig, "{\"maxParallelLlmRequests\": 1}");

        var resolved = CodeCondenserConfigurationLoader.ResolveUserProfileConfigFile(customDir);
        resolved.Should().Be(Path.GetFullPath(appDataConfig));
    }

    [Fact]
    public void ResolveUserProfileConfigFile_UsesAppDataEnvironmentVariable()
    {
        var appDataDir = Path.Combine(_tempDirectory, "EnvAppData");
        var configDir = Path.Combine(appDataDir, "CodeCondenser");
        Directory.CreateDirectory(configDir);
        var configFile = Path.Combine(configDir, "codecondenser.json");
        File.WriteAllText(configFile, "{}");

        var originalEnv = Environment.GetEnvironmentVariable("CODECONDENSER_APPDATA");
        try
        {
            Environment.SetEnvironmentVariable("CODECONDENSER_APPDATA", appDataDir);
            var resolved = CodeCondenserConfigurationLoader.ResolveUserProfileConfigFile();
            resolved.Should().NotBeNull();
            resolved.Should().Be(Path.GetFullPath(configFile));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODECONDENSER_APPDATA", originalEnv);
        }
    }

    [Fact]
    public void GetDefaultGlobalConfigFilePath_ReturnsExpectedPlatformPath()
    {
        var path = CodeCondenserConfigurationLoader.GetDefaultGlobalConfigFilePath();
        path.Should().NotBeNullOrWhiteSpace();

        if (OperatingSystem.IsWindows())
        {
            path.Should().Contain("CodeCondenser");
            path.Should().EndWith("codecondenser.json");
        }
        else
        {
            path.Should().Contain(".codecondenser");
            path.Should().EndWith("codecondenser.json");
        }
    }

    [Fact]
    public void MergeJson_MergesNestedOllamaPropertiesCorrectly()
    {
        var globalJson = """
        {
            "maxParallelLlmRequests": 8,
            "ollama": {
                "endpoint": "http://user-server:11434",
                "model": "gemma4:e4b",
                "contextSize": 32768
            },
            "commentLengthThreshold": 120
        }
        """;

        var projectJson = """
        {
            "duplicationStatementThreshold": 12,
            "ollama": {
                "model": "deepseek-coder:6.7b"
            }
        }
        """;

        var mergedJson = CodeCondenserConfigurationLoader.MergeJson(globalJson, projectJson);
        var settings = CodeCondenserConfigurationLoader.LoadFromString(mergedJson);

        settings.MaxParallelLlmRequests.Should().Be(8);
        settings.CommentLengthThreshold.Should().Be(120);
        settings.DuplicationStatementThreshold.Should().Be(12);
        settings.Ollama.Endpoint.Should().Be("http://user-server:11434");
        settings.Ollama.Model.Should().Be("deepseek-coder:6.7b");
        settings.Ollama.ContextSize.Should().Be(32768);
    }

    [Fact]
    public void MergeJson_HandlesCaseInsensitivity()
    {
        var globalJson = """
        {
            "Ollama": {
                "Endpoint": "http://global:11434",
                "Model": "global-model"
            }
        }
        """;

        var projectJson = """
        {
            "ollama": {
                "model": "project-model"
            }
        }
        """;

        var mergedJson = CodeCondenserConfigurationLoader.MergeJson(globalJson, projectJson);
        var settings = CodeCondenserConfigurationLoader.LoadFromString(mergedJson);

        settings.Ollama.Endpoint.Should().Be("http://global:11434");
        settings.Ollama.Model.Should().Be("project-model");
    }

    [Fact]
    public void Load_WithGlobalConfigOnly_SetsIsLlmConfiguredGlobally_AndDefaultsToReport()
    {
        var userHome = Path.Combine(_tempDirectory, "UserHome4");
        var configDir = Path.Combine(userHome, ".codecondenser");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "codecondenser.json"), """
        {
            "ollama": {
                "endpoint": "http://my-ollama:11434",
                "model": "gemma4:e4b"
            }
        }
        """);

        var projectDir = Path.Combine(_tempDirectory, "EmptyProjectDir");
        Directory.CreateDirectory(projectDir);

        var settings = CodeCondenserConfigurationLoader.Load(
            targetPathOrConfigFile: projectDir,
            globalConfigFileOrDir: userHome);

        settings.IsLlmConfigured.Should().BeTrue();
        settings.IsLlmConfiguredGlobally.Should().BeTrue();
        settings.Ollama.Endpoint.Should().Be("http://my-ollama:11434");
        settings.Ollama.Model.Should().Be("gemma4:e4b");
        // Effective summary mode must stay Report when LLM is only configured in user profile
        settings.GetEffectiveSummaryMode().Should().Be(SummaryMode.Report);
    }

    [Fact]
    public void Load_WithGlobalConfigExplicitSummary_PreservesSummaryMode()
    {
        var userHome = Path.Combine(_tempDirectory, "UserHome5");
        var configDir = Path.Combine(userHome, ".codecondenser");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "codecondenser.json"), """
        {
            "summaryMode": "Summarize",
            "ollama": {
                "endpoint": "http://my-ollama:11434",
                "model": "gemma4:e4b"
            }
        }
        """);

        var projectDir = Path.Combine(_tempDirectory, "EmptyProjectDir2");
        Directory.CreateDirectory(projectDir);

        var settings = CodeCondenserConfigurationLoader.Load(
            targetPathOrConfigFile: projectDir,
            globalConfigFileOrDir: userHome);

        settings.SummaryMode.Should().Be(SummaryMode.Summarize);
        settings.GetEffectiveSummaryMode().Should().Be(SummaryMode.Summarize);
    }

    [Fact]
    public void Load_WithGlobalAndProjectConfig_ProjectOverridesGlobal_SetsIsLlmConfiguredLocally()
    {
        var userHome = Path.Combine(_tempDirectory, "UserHome6");
        var configDir = Path.Combine(userHome, ".codecondenser");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "codecondenser.json"), """
        {
            "maxParallelLlmRequests": 8,
            "ollama": {
                "endpoint": "http://user-server:11434",
                "model": "global-model"
            }
        }
        """);

        var projectDir = Path.Combine(_tempDirectory, "ProjectDirWithConfig");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "codecondenser.json"), """
        {
            "duplicationStatementThreshold": 14,
            "ollama": {
                "model": "project-model"
            }
        }
        """);

        var settings = CodeCondenserConfigurationLoader.Load(
            targetPathOrConfigFile: projectDir,
            globalConfigFileOrDir: userHome);

        settings.MaxParallelLlmRequests.Should().Be(8);
        settings.DuplicationStatementThreshold.Should().Be(14);
        settings.Ollama.Endpoint.Should().Be("http://user-server:11434");
        settings.Ollama.Model.Should().Be("project-model");
        settings.IsLlmConfigured.Should().BeTrue();
        settings.IsLlmConfiguredGlobally.Should().BeFalse();
        // Because project explicitly configured LLM model, effective mode is Summarize
        settings.GetEffectiveSummaryMode().Should().Be(SummaryMode.Summarize);
    }

    [Fact]
    public void Load_WithDisableGlobalConfig_IgnoresGlobalConfig()
    {
        var userHome = Path.Combine(_tempDirectory, "UserHome7");
        var configDir = Path.Combine(userHome, ".codecondenser");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "codecondenser.json"), """
        {
            "ollama": {
                "model": "custom-model"
            }
        }
        """);

        var projectDir = Path.Combine(_tempDirectory, "EmptyProjectDir3");
        Directory.CreateDirectory(projectDir);

        var settings = CodeCondenserConfigurationLoader.Load(
            targetPathOrConfigFile: projectDir,
            globalConfigFileOrDir: userHome,
            disableGlobalConfig: true);

        settings.IsLlmConfigured.Should().BeFalse();
        settings.Ollama.Model.Should().Be("gemma4:e4b"); // default
    }

    [Fact]
    public void ResolveConfigFile_TraversesParentDirectories_UntilConfigFound()
    {
        var repoRoot = Path.Combine(_tempDirectory, "RepoRoot");
        Directory.CreateDirectory(repoRoot);
        var configFile = Path.Combine(repoRoot, CodeCondenserConfigurationLoader.DefaultConfigFileName);
        File.WriteAllText(configFile, "{}");

        var deepNestedDir = Path.Combine(repoRoot, "src", "ProjectA", "SubModule");
        Directory.CreateDirectory(deepNestedDir);

        var resolved = CodeCondenserConfigurationLoader.ResolveConfigFile(deepNestedDir);

        resolved.Should().NotBeNull();
        resolved.Should().Be(Path.GetFullPath(configFile));
    }

    [Fact]
    public void ResolveConfigFile_DoesNotTreatUserProfileDotCodeCondenserAsProjectConfig()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userProfile)) return;

        var tempSub = Path.Combine(Path.GetTempPath(), "ResolveConfigTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempSub);
        try
        {
            var resolved = CodeCondenserConfigurationLoader.ResolveConfigFile(tempSub);
            if (resolved != null)
            {
                resolved.Should().NotContain(Path.Combine(userProfile, ".codecondenser"));
            }
        }
        finally
        {
            if (Directory.Exists(tempSub)) Directory.Delete(tempSub, true);
        }
    }
}
