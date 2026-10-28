using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Pmad.CodeCondenser.Core.Clones;

namespace Pmad.CodeCondenser.Core.Patterns;

/// <summary>
/// Resolves target framework versions from project files for TFM-dependent condensation rules.
/// </summary>
public static partial class ProjectTargetFrameworkResolver
{
    private static readonly ConcurrentDictionary<string, Version?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Incurs minimal I/O to extract the primary target framework version from a .csproj file.
    /// </summary>
    public static Version? ResolveTargetFramework(string? csprojFilePath)
    {
        if (string.IsNullOrWhiteSpace(csprojFilePath) || !File.Exists(csprojFilePath))
        {
            return null;
        }

        return Cache.GetOrAdd(Path.GetFullPath(csprojFilePath), path =>
        {
            try
            {
                var content = File.ReadAllText(path);
                return ParseTargetFrameworkFromXml(content);
            }
            catch
            {
                return null;
            }
        });
    }

    /// <summary>
    /// Resolves target framework version for a given source file using the provided project locator.
    /// </summary>
    public static Version? ResolveTargetFrameworkForFile(string filePath, IProjectLocator? projectLocator, string? rootDirectory = null)
    {
        if (projectLocator == null)
        {
            return null;
        }

        var csproj = projectLocator.LocateProjectFilePath(filePath, rootDirectory);
        return ResolveTargetFramework(csproj);
    }

    /// <summary>
    /// Parses target framework version from .csproj XML content.
    /// </summary>
    public static Version? ParseTargetFrameworkFromXml(string csprojContent)
    {
        if (string.IsNullOrWhiteSpace(csprojContent))
        {
            return null;
        }

        // Match <TargetFramework>...</TargetFramework> or first item in <TargetFrameworks>...</TargetFrameworks>
        var singleMatch = TargetFrameworkRegex().Match(csprojContent);
        if (singleMatch.Success)
        {
            return ParseTfmVersion(singleMatch.Groups[1].Value);
        }

        var multiMatch = TargetFrameworksRegex().Match(csprojContent);
        if (multiMatch.Success)
        {
            var tfms = multiMatch.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var tfm in tfms)
            {
                var ver = ParseTfmVersion(tfm);
                if (ver != null)
                {
                    return ver;
                }
            }
        }

        return null;
    }

    private static Version? ParseTfmVersion(string tfm)
    {
        var text = tfm.Trim();
        if (text.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            text = text[3..];
        }

        // Filter out windows/android suffix e.g. net8.0-windows
        var dashIdx = text.IndexOf('-', StringComparison.Ordinal);
        if (dashIdx >= 0)
        {
            text = text[..dashIdx];
        }

        if (Version.TryParse(text, out var ver))
        {
            return ver;
        }

        if (int.TryParse(text, out var major))
        {
            return new Version(major, 0);
        }

        return null;
    }

    [GeneratedRegex(@"<TargetFramework>\s*([^<\s]+)\s*</TargetFramework>", RegexOptions.IgnoreCase)]
    private static partial Regex TargetFrameworkRegex();

    [GeneratedRegex(@"<TargetFrameworks>\s*([^<\s]+)\s*</TargetFrameworks>", RegexOptions.IgnoreCase)]
    private static partial Regex TargetFrameworksRegex();
}
