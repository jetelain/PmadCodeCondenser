using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pmad.CodeCondenser.Core.Configuration;

/// <summary>
/// Helper for loading and deserializing <see cref="CodeCondenserSettings"/> from JSON configuration files (e.g. codecondenser.json).
/// Supports hierarchical inheritance from global user profile configurations (~/.codecondenser/codecondenser.json or config.json).
/// </summary>
public static class CodeCondenserConfigurationLoader
{
    public const string DefaultConfigFileName = "codecondenser.json";

    public const string DefaultIgnoreFileName = ".codecondenserignore";

    public const string DefaultConfigDirectoryName = ".codecondenser";

    public const string UserConfigFileName = "config.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Loads <see cref="CodeCondenserSettings"/> from a specified configuration file or directory,
    /// merges any settings from the global user profile configuration file (~/.codecondenser/codecondenser.json or config.json),
    /// and automatically merges any rules found in a standalone .codecondenserignore file.
    /// </summary>
    /// <param name="targetPathOrConfigFile">Path to the project configuration file or the directory to search in.</param>
    /// <param name="globalConfigFileOrDir">Optional explicit path to the global user profile configuration file or directory.</param>
    /// <param name="disableGlobalConfig">When true, disables searching and loading of the global user profile configuration.</param>
    /// <returns>The loaded settings or a new default instance if not found.</returns>
    public static CodeCondenserSettings Load(
        string? targetPathOrConfigFile = null,
        string? globalConfigFileOrDir = null,
        bool disableGlobalConfig = false)
    {
        string? resolvedGlobalPath = null;
        if (!disableGlobalConfig)
        {
            resolvedGlobalPath = !string.IsNullOrWhiteSpace(globalConfigFileOrDir)
                ? (File.Exists(globalConfigFileOrDir) ? Path.GetFullPath(globalConfigFileOrDir) : ResolveUserProfileConfigFile(globalConfigFileOrDir))
                : ResolveUserProfileConfigFile();
        }

        var resolvedProjectPath = ResolveConfigFile(targetPathOrConfigFile);

        if (resolvedGlobalPath != null && resolvedProjectPath != null &&
            string.Equals(resolvedGlobalPath, resolvedProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            resolvedGlobalPath = null;
        }

        string? globalJson = null;
        if (resolvedGlobalPath != null && File.Exists(resolvedGlobalPath))
        {
            try
            {
                globalJson = File.ReadAllText(resolvedGlobalPath);
            }
            catch
            {
                globalJson = null;
            }
        }

        string? projectJson = null;
        if (resolvedProjectPath != null && File.Exists(resolvedProjectPath))
        {
            try
            {
                projectJson = File.ReadAllText(resolvedProjectPath);
            }
            catch
            {
                projectJson = null;
            }
        }

        CodeCondenserSettings settings;
        if (globalJson != null && projectJson != null)
        {
            var mergedJson = MergeJson(globalJson, projectJson);
            settings = LoadFromString(mergedJson);

            bool projectHasLlm = HasLlmConfiguration(projectJson);
            bool globalHasLlm = HasLlmConfiguration(globalJson);

            if (projectHasLlm)
            {
                settings.IsLlmConfigured = true;
                settings.IsLlmConfiguredGlobally = false;
            }
            else if (globalHasLlm)
            {
                settings.IsLlmConfigured = true;
                settings.IsLlmConfiguredGlobally = true;
            }
        }
        else if (projectJson != null)
        {
            settings = LoadFromString(projectJson);
            if (HasLlmConfiguration(projectJson))
            {
                settings.IsLlmConfigured = true;
                settings.IsLlmConfiguredGlobally = false;
            }
        }
        else if (globalJson != null)
        {
            settings = LoadFromString(globalJson);
            if (HasLlmConfiguration(globalJson))
            {
                settings.IsLlmConfigured = true;
                settings.IsLlmConfiguredGlobally = true;
            }
        }
        else
        {
            settings = new CodeCondenserSettings();
        }

        MergeIgnoreFile(settings, resolvedProjectPath ?? targetPathOrConfigFile);

        return settings;
    }

    /// <summary>
    /// Deserializes <see cref="CodeCondenserSettings"/> from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string.</param>
    /// <returns>The deserialized settings instance or default if empty.</returns>
    public static CodeCondenserSettings LoadFromString(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CodeCondenserSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<CodeCondenserSettings>(json, SerializerOptions);
            if (settings != null)
            {
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
                if (HasLlmConfiguration(doc.RootElement))
                {
                    settings.IsLlmConfigured = true;
                }
            }

            return settings ?? new CodeCondenserSettings();
        }
        catch (JsonException)
        {
            return new CodeCondenserSettings();
        }
    }

    /// <summary>
    /// Merges two JSON strings hierarchically. Properties in <paramref name="overrideJson"/> override properties in <paramref name="baseJson"/>.
    /// Nested JSON objects are merged recursively.
    /// </summary>
    public static string MergeJson(string baseJson, string overrideJson)
    {
        if (string.IsNullOrWhiteSpace(baseJson))
        {
            return overrideJson ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(overrideJson))
        {
            return baseJson;
        }

        try
        {
            var docOptions = new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var baseNode = JsonNode.Parse(baseJson, null, docOptions);
            var overrideNode = JsonNode.Parse(overrideJson, null, docOptions);

            if (baseNode is JsonObject baseObj && overrideNode is JsonObject overrideObj)
            {
                MergeJsonObjects(baseObj, overrideObj);
                return baseObj.ToJsonString();
            }

            return overrideJson;
        }
        catch
        {
            return overrideJson;
        }
    }

    private static void MergeJsonObjects(JsonObject target, JsonObject source)
    {
        foreach (var property in source.ToList())
        {
            var key = property.Key;
            var sourceVal = property.Value;

            var existingKey = target.Select(p => p.Key)
                                    .FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

            if (existingKey != null)
            {
                if (sourceVal is JsonObject sourceChildObj && target[existingKey] is JsonObject targetChildObj)
                {
                    MergeJsonObjects(targetChildObj, sourceChildObj);
                    continue;
                }

                target.Remove(existingKey);
            }

            target[key] = sourceVal?.DeepClone();
        }
    }

    /// <summary>
    /// Checks whether the JSON document explicitly configures an LLM provider, model, endpoint, or ollama section.
    /// </summary>
    public static bool HasLlmConfiguration(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            return HasLlmConfiguration(doc.RootElement);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether the JSON element explicitly configures an LLM provider, model, endpoint, or ollama section.
    /// </summary>
    public static bool HasLlmConfiguration(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var prop in root.EnumerateObject())
        {
            if (string.Equals(prop.Name, "ollama", StringComparison.OrdinalIgnoreCase))
            {
                if (prop.Value.ValueKind == JsonValueKind.Object && prop.Value.EnumerateObject().Any())
                {
                    return true;
                }
            }
            else if (string.Equals(prop.Name, "model", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(prop.Name, "endpoint", StringComparison.OrdinalIgnoreCase))
            {
                if (prop.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(prop.Value.GetString()))
                {
                    return true;
                }
            }
            else if (string.Equals(prop.Name, "provider", StringComparison.OrdinalIgnoreCase))
            {
                if (prop.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(prop.Value.GetString()))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Merges patterns from a .codecondenserignore file into the active settings Exclude list.
    /// </summary>
    public static void MergeIgnoreFile(CodeCondenserSettings settings, string? targetPathOrConfigFile)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var ignoreFilePath = ResolveIgnoreFile(targetPathOrConfigFile);
        if (ignoreFilePath == null || !File.Exists(ignoreFilePath))
        {
            return;
        }

        try
        {
            var lines = File.ReadAllLines(ignoreFilePath);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                {
                    continue;
                }

                if (!settings.Exclude.Contains(line, StringComparer.Ordinal))
                {
                    settings.Exclude.Add(line);
                }
            }
        }
        catch (Exception)
        {
            // Ignore unreadable .codecondenserignore files
        }
    }

    /// <summary>
    /// Resolves the absolute path to a .codecondenserignore file if present.
    /// </summary>
    public static string? ResolveIgnoreFile(string? targetPathOrConfigFile)
    {
        string? startDir = null;

        if (!string.IsNullOrWhiteSpace(targetPathOrConfigFile))
        {
            if (File.Exists(targetPathOrConfigFile))
            {
                startDir = Path.GetDirectoryName(targetPathOrConfigFile);
            }
            else if (Directory.Exists(targetPathOrConfigFile))
            {
                startDir = targetPathOrConfigFile;
            }
        }

        startDir ??= Directory.GetCurrentDirectory();

        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, DefaultIgnoreFileName);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }

            // Stop walking up if we reach repository boundary (.git, .sln, .slnx)
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                dir.EnumerateFiles("*.sln").Any() ||
                dir.EnumerateFiles("*.slnx").Any())
            {
                break;
            }

            dir = dir.Parent;
        }

        var currentDirCandidate = Path.Combine(Directory.GetCurrentDirectory(), DefaultIgnoreFileName);
        if (File.Exists(currentDirCandidate))
        {
            return Path.GetFullPath(currentDirCandidate);
        }

        return null;
    }

    /// <summary>
    /// Resolves the absolute path to the configuration file if present.
    /// </summary>
    public static string? ResolveConfigFile(string? targetPathOrConfigFile)
    {
        if (!string.IsNullOrWhiteSpace(targetPathOrConfigFile))
        {
            if (File.Exists(targetPathOrConfigFile))
            {
                if (targetPathOrConfigFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFullPath(targetPathOrConfigFile);
                }

                var parentDir = Path.GetDirectoryName(targetPathOrConfigFile);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    var candidateInParent = Path.Combine(parentDir, DefaultConfigFileName);
                    if (File.Exists(candidateInParent))
                    {
                        return Path.GetFullPath(candidateInParent);
                    }
                }
            }

            if (Directory.Exists(targetPathOrConfigFile))
            {
                var candidate = Path.Combine(targetPathOrConfigFile, DefaultConfigFileName);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        var currentDirCandidate = Path.Combine(Directory.GetCurrentDirectory(), DefaultConfigFileName);
        if (File.Exists(currentDirCandidate))
        {
            return Path.GetFullPath(currentDirCandidate);
        }

        return null;
    }

    /// <summary>
    /// Resolves the absolute path to the global user profile configuration file if present.
    /// Checks ~/.codecondenser/codecondenser.json, ~/.codecondenser/config.json, and ~/.codecondenser.json.
    /// </summary>
    /// <param name="userProfileDirectory">Optional user profile root directory override (useful for testing).</param>
    /// <returns>The resolved configuration file path, or null if not found.</returns>
    public static string? ResolveUserProfileConfigFile(string? userProfileDirectory = null)
    {
        if (userProfileDirectory == null)
        {
            var envDisable = Environment.GetEnvironmentVariable("CODECONDENSER_DISABLE_GLOBAL_CONFIG");
            if (string.Equals(envDisable, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(envDisable, "true", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var envGlobalConfig = Environment.GetEnvironmentVariable("CODECONDENSER_GLOBAL_CONFIG") ??
                                  Environment.GetEnvironmentVariable("PMAD_CODECONDENSER_GLOBAL_CONFIG");
            if (!string.IsNullOrWhiteSpace(envGlobalConfig) && File.Exists(envGlobalConfig))
            {
                return Path.GetFullPath(envGlobalConfig);
            }

            var envProfile = Environment.GetEnvironmentVariable("CODECONDENSER_USER_PROFILE");
            if (!string.IsNullOrWhiteSpace(envProfile))
            {
                userProfileDirectory = envProfile;
            }
            else
            {
                userProfileDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
        }

        if (string.IsNullOrWhiteSpace(userProfileDirectory))
        {
            return null;
        }

        if (File.Exists(userProfileDirectory))
        {
            return Path.GetFullPath(userProfileDirectory);
        }

        if (!Directory.Exists(userProfileDirectory))
        {
            return null;
        }

        // 1. ~/.codecondenser/codecondenser.json
        var candidate1 = Path.Combine(userProfileDirectory, DefaultConfigDirectoryName, DefaultConfigFileName);
        if (File.Exists(candidate1))
        {
            return Path.GetFullPath(candidate1);
        }

        // 2. ~/.codecondenser/config.json
        var candidate2 = Path.Combine(userProfileDirectory, DefaultConfigDirectoryName, UserConfigFileName);
        if (File.Exists(candidate2))
        {
            return Path.GetFullPath(candidate2);
        }

        // 3. ~/.codecondenser.json
        var candidate3 = Path.Combine(userProfileDirectory, "." + DefaultConfigFileName);
        if (File.Exists(candidate3))
        {
            return Path.GetFullPath(candidate3);
        }

        return null;
    }
}
