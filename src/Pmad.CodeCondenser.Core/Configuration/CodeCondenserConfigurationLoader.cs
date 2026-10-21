using System.Text.Json;

namespace Pmad.CodeCondenser.Core.Configuration;

/// <summary>
/// Helper for loading and deserializing <see cref="CodeCondenserSettings"/> from JSON configuration files (e.g. codecondenser.json).
/// </summary>
public static class CodeCondenserConfigurationLoader
{
    public const string DefaultConfigFileName = "codecondenser.json";

    public const string DefaultIgnoreFileName = ".codecondenserignore";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Loads <see cref="CodeCondenserSettings"/> from a specified configuration file or directory,
    /// and automatically merges any rules found in a standalone .codecondenserignore file.
    /// </summary>
    /// <param name="targetPathOrConfigFile">Path to the configuration file or the directory to search in.</param>
    /// <returns>The loaded settings or a new default instance if not found.</returns>
    public static CodeCondenserSettings Load(string? targetPathOrConfigFile = null)
    {
        var resolvedPath = ResolveConfigFile(targetPathOrConfigFile);
        CodeCondenserSettings settings;

        if (resolvedPath == null || !File.Exists(resolvedPath))
        {
            settings = new CodeCondenserSettings();
        }
        else
        {
            try
            {
                var json = File.ReadAllText(resolvedPath);
                settings = LoadFromString(json);
            }
            catch (Exception)
            {
                // If the configuration file is unreadable or invalid JSON, return defaults
                settings = new CodeCondenserSettings();
            }
        }

        MergeIgnoreFile(settings, resolvedPath ?? targetPathOrConfigFile);

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
            return settings ?? new CodeCondenserSettings();
        }
        catch (JsonException)
        {
            return new CodeCondenserSettings();
        }
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
}
