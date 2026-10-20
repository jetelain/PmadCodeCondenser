using System.Text.Json;

namespace Pmad.CodeCondenser.Core.Configuration;

/// <summary>
/// Helper for loading and deserializing <see cref="CodeCondenserSettings"/> from JSON configuration files (e.g. codecondenser.json).
/// </summary>
public static class CodeCondenserConfigurationLoader
{
    public const string DefaultConfigFileName = "codecondenser.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Loads <see cref="CodeCondenserSettings"/> from a specified configuration file or directory.
    /// If no path is provided or the file is not found, returns a default settings instance.
    /// </summary>
    /// <param name="targetPathOrConfigFile">Path to the configuration file or the directory to search in.</param>
    /// <returns>The loaded settings or a new default instance if not found.</returns>
    public static CodeCondenserSettings Load(string? targetPathOrConfigFile = null)
    {
        var resolvedPath = ResolveConfigFile(targetPathOrConfigFile);
        if (resolvedPath == null || !File.Exists(resolvedPath))
        {
            return new CodeCondenserSettings();
        }

        try
        {
            var json = File.ReadAllText(resolvedPath);
            return LoadFromString(json);
        }
        catch (Exception)
        {
            // If the configuration file is unreadable or invalid JSON, return defaults
            return new CodeCondenserSettings();
        }
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
