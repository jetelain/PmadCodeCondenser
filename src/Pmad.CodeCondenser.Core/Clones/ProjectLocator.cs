using System.Collections.Concurrent;

namespace Pmad.CodeCondenser.Core.Clones;

/// <summary>
/// Resolves project boundaries by searching upwards for the nearest enclosing *.csproj file,
/// with intermediate ancestor path memoization in a concurrent dictionary.
/// </summary>
public sealed class ProjectLocator : IProjectLocator
{
    private readonly ConcurrentDictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the internal cache mapping intermediate directory paths to their resolved enclosing .csproj paths (or null if none found).
    /// </summary>
    public IReadOnlyDictionary<string, string?> Cache => _cache;

    /// <inheritdoc />
    public string LocateProjectId(string filePath, string? rootDirectory = null)
    {
        var csproj = LocateProjectFilePath(filePath, rootDirectory);
        return !string.IsNullOrEmpty(csproj) ? Path.GetFileNameWithoutExtension(csproj) : "Default";
    }

    /// <inheritdoc />
    public string? LocateProjectFilePath(string filePath, string? rootDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        string? currentDir;
        try
        {
            var fullPath = Path.IsPathRooted(filePath) || string.IsNullOrWhiteSpace(rootDirectory)
                ? Path.GetFullPath(filePath)
                : Path.GetFullPath(Path.Combine(rootDirectory, filePath));
            currentDir = Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath);
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrEmpty(currentDir))
        {
            return null;
        }

        string? normalizedRoot = null;
        if (!string.IsNullOrWhiteSpace(rootDirectory))
        {
            try
            {
                normalizedRoot = NormalizeDirectory(Path.GetFullPath(rootDirectory));
            }
            catch
            {
                // Fallback if root path is invalid
            }
        }

        var visitedDirs = new List<string>();
        string? foundCsproj = null;

        while (!string.IsNullOrEmpty(currentDir))
        {
            var normalizedCurrent = NormalizeDirectory(currentDir);

            if (_cache.TryGetValue(normalizedCurrent, out var cachedCsproj))
            {
                foundCsproj = cachedCsproj;
                break;
            }

            visitedDirs.Add(normalizedCurrent);

            // Check if current directory contains any *.csproj
            try
            {
                if (Directory.Exists(normalizedCurrent))
                {
                    var csprojFiles = Directory.GetFiles(normalizedCurrent, "*.csproj");
                    if (csprojFiles.Length > 0)
                    {
                        Array.Sort(csprojFiles, StringComparer.OrdinalIgnoreCase);
                        foundCsproj = Path.GetFullPath(csprojFiles[0]);
                        break;
                    }
                }
            }
            catch
            {
                // Access denied or IO error
            }

            if (normalizedRoot != null && string.Equals(normalizedCurrent, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                // Reached workspace root boundary without finding a .csproj
                break;
            }

            var parent = Path.GetDirectoryName(normalizedCurrent);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, normalizedCurrent, StringComparison.OrdinalIgnoreCase))
            {
                break; // Reached filesystem root
            }

            currentDir = parent;
        }

        // Comprehensive path memoization: record all intermediate directories visited in this walk
        foreach (var dir in visitedDirs)
        {
            _cache.TryAdd(dir, foundCsproj);
        }

        return foundCsproj;
    }

    private static string NormalizeDirectory(string path)
    {
        var normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalized;
    }
}
