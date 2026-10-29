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

        // Match <TargetFramework>...</TargetFramework> or minimum version across <TargetFrameworks>...</TargetFrameworks>
        var singleMatch = TargetFrameworkRegex().Match(csprojContent);
        if (singleMatch.Success)
        {
            return ParseTfmVersion(singleMatch.Groups[1].Value);
        }

        var multiMatch = TargetFrameworksRegex().Match(csprojContent);
        if (multiMatch.Success)
        {
            var tfms = multiMatch.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Version? minVersion = null;
            foreach (var tfm in tfms)
            {
                var ver = ParseTfmVersion(tfm);
                if (ver != null)
                {
                    if (minVersion == null || ver < minVersion)
                    {
                        minVersion = ver;
                    }
                }
            }
            return minVersion;
        }

        return null;
    }

    /// <summary>
    /// Parses target framework version from a TFM string (e.g. "net10.0", "net8.0-windows", "net48", "netstandard2.0").
    /// </summary>
    public static Version? ParseTfmVersion(string? tfm)
    {
        if (string.IsNullOrWhiteSpace(tfm))
        {
            return null;
        }

        var text = tfm.Trim();

        // Filter out windows/android suffix e.g. net8.0-windows
        var dashIdx = text.IndexOf('-', StringComparison.Ordinal);
        if (dashIdx >= 0)
        {
            text = text[..dashIdx];
        }

        if (text.StartsWith("netcoreapp", StringComparison.OrdinalIgnoreCase))
        {
            var verText = text[10..];
            return Version.TryParse(verText, out var v) ? v : null;
        }

        if (text.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
        {
            var verText = text[11..];
            return Version.TryParse(verText, out var v) ? v : null;
        }

        if (text.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            text = text[3..];
        }

        // Handle .NET Framework 4.x / 3.x / 2.x without dots, e.g. "48" -> 4.8, "472" -> 4.7.2, "35" -> 3.5
        if (text.Length is 2 or 3 && char.IsDigit(text[0]) && char.IsDigit(text[1]))
        {
            var major = text[0] - '0';
            var minor = text[1] - '0';
            if (major is 2 or 3 or 4)
            {
                if (text.Length == 3 && char.IsDigit(text[2]))
                {
                    var build = text[2] - '0';
                    return new Version(major, minor, build);
                }

                return new Version(major, minor);
            }
        }

        if (Version.TryParse(text, out var ver))
        {
            return ver;
        }

        if (int.TryParse(text, out var modernMajor) && modernMajor >= 5)
        {
            return new Version(modernMajor, 0);
        }

        return null;
    }

    [GeneratedRegex(@"<TargetFramework>\s*([^<\s]+)\s*</TargetFramework>", RegexOptions.IgnoreCase)]
    private static partial Regex TargetFrameworkRegex();

    [GeneratedRegex(@"<TargetFrameworks>\s*([^<]+?)\s*</TargetFrameworks>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TargetFrameworksRegex();
}
