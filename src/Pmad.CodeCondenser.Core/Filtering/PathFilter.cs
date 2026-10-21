using Microsoft.Extensions.FileSystemGlobbing;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Filtering;

/// <summary>
/// Service responsible for resolving base repository directories, evaluating glob patterns for include/exclude/test/generated paths,
/// and pruning directory trees early during discovery.
/// </summary>
public sealed class PathFilter
{
    private static readonly HashSet<string> DefaultIgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", ".codecondenser", "node_modules", "testresults"
    };

    private readonly CodeCondenserSettings _settings;
    private readonly Matcher? _includeMatcher;
    private readonly Matcher? _excludeMatcher;
    private readonly Matcher? _testMatcher;
    private readonly Matcher? _generatedMatcher;
    private readonly HashSet<string>? _rootIncludePrefixes;

    public string BaseDirectory { get; }

    public PathFilter(CodeCondenserSettings settings, string targetPath)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        BaseDirectory = ResolveBaseDirectory(targetPath);

        if (_settings.Include.Count > 0)
        {
            _includeMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            foreach (var pattern in _settings.Include)
            {
                var norm = NormalizePattern(pattern);
                if (!string.IsNullOrWhiteSpace(norm))
                {
                    _includeMatcher.AddInclude(norm);
                }
            }
            _rootIncludePrefixes = ExtractRootIncludePrefixes(_settings.Include);
        }

        if (_settings.Exclude.Count > 0)
        {
            _excludeMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            foreach (var pattern in _settings.Exclude)
            {
                var norm = NormalizePattern(pattern);
                if (!string.IsNullOrWhiteSpace(norm))
                {
                    _excludeMatcher.AddInclude(norm);
                }
            }
        }

        if (_settings.TestPaths.Count > 0)
        {
            _testMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            foreach (var pattern in _settings.TestPaths)
            {
                var norm = NormalizePattern(pattern);
                if (!string.IsNullOrWhiteSpace(norm))
                {
                    _testMatcher.AddInclude(norm);
                }
            }
        }

        if (_settings.ExcludeGeneratedCode && _settings.GeneratedCodePatterns.Count > 0)
        {
            _generatedMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            foreach (var pattern in _settings.GeneratedCodePatterns)
            {
                var norm = NormalizePattern(pattern);
                if (!string.IsNullOrWhiteSpace(norm))
                {
                    _generatedMatcher.AddInclude(norm);
                }
            }
        }
    }

    /// <summary>
    /// Normalizes a path relative to the repository base directory using forward slashes.
    /// </summary>
    public string NormalizeRelativePath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return string.Empty;
        }

        try
        {
            var rel = Path.GetRelativePath(BaseDirectory, fullPath);
            rel = rel.Replace('\\', '/');
            if (rel.StartsWith("./", StringComparison.Ordinal))
            {
                rel = rel[2..];
            }
            return rel;
        }
        catch
        {
            return fullPath.Replace('\\', '/');
        }
    }

    /// <summary>
    /// Determines whether a directory should be pruned from traversal before descending into its children.
    /// </summary>
    /// <param name="currentDirectoryFullPath">Full path to the directory.</param>
    /// <param name="isTargetRoot">True if this directory is the root target being analyzed.</param>
    public bool ShouldPruneDirectory(string currentDirectoryFullPath, bool isTargetRoot = false)
    {
        if (isTargetRoot)
        {
            return false;
        }

        var dirInfo = new DirectoryInfo(currentDirectoryFullPath);
        var dirName = dirInfo.Name;

        // 1. Default directory excludes
        if (_settings.UseDefaultExcludes)
        {
            if (DefaultIgnoredDirectoryNames.Contains(dirName) || (dirName.StartsWith('.') && dirName.Length > 1))
            {
                return true;
            }
        }

        var relDir = NormalizeRelativePath(currentDirectoryFullPath);
        if (string.IsNullOrEmpty(relDir) || relDir == ".")
        {
            return false;
        }

        // 2. User-defined exclude patterns checked against directory
        if (_excludeMatcher != null)
        {
            if (_excludeMatcher.Match(relDir).HasMatches ||
                _excludeMatcher.Match(relDir + "/").HasMatches ||
                _excludeMatcher.Match(relDir + "/*").HasMatches ||
                _excludeMatcher.Match(relDir + "/**").HasMatches)
            {
                return true;
            }
        }

        // 3. Root prefix include pruning (when traversal is at or near repo base)
        if (_rootIncludePrefixes != null && _rootIncludePrefixes.Count > 0)
        {
            var segments = relDir.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 1 && !_rootIncludePrefixes.Contains(segments[0]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a file is identified as auto-generated based on configured patterns or leading header comments.
    /// </summary>
    public bool IsGenerated(string relativePath, string fullPath)
    {
        if (!_settings.ExcludeGeneratedCode)
        {
            return false;
        }

        if (_generatedMatcher != null && _generatedMatcher.Match(relativePath).HasMatches)
        {
            return true;
        }

        return GeneratedCodeDetector.HasAutoGeneratedHeader(fullPath);
    }

    /// <summary>
    /// Checks if a file matches test path patterns.
    /// </summary>
    public bool IsTest(string relativePath)
    {
        if (_testMatcher == null)
        {
            return false;
        }

        return _testMatcher.Match(relativePath).HasMatches;
    }

    /// <summary>
    /// Classifies a file into Production, Test, or Generated.
    /// </summary>
    public FileCategory ClassifyFile(string relativePath, string fullPath)
    {
        if (IsGenerated(relativePath, fullPath))
        {
            return FileCategory.Generated;
        }

        if (IsTest(relativePath))
        {
            return FileCategory.Test;
        }

        return FileCategory.Production;
    }

    /// <summary>
    /// Evaluates whether a file should be included in analysis or skipped.
    /// </summary>
    /// <param name="relativePath">Normalized relative path from BaseDirectory.</param>
    /// <param name="fullPath">Absolute file path.</param>
    /// <param name="category">The resulting file category.</param>
    /// <param name="isSkippedGenerated">True if the file was skipped specifically because it is generated code.</param>
    /// <returns>True if the file is eligible for analysis; false if it should be skipped.</returns>
    public bool ShouldAnalyzeFile(
        string relativePath,
        string fullPath,
        out FileCategory category,
        out bool isSkippedGenerated)
    {
        isSkippedGenerated = false;

        // 1. Check generated code
        if (IsGenerated(relativePath, fullPath))
        {
            category = FileCategory.Generated;
            isSkippedGenerated = true;
            return false;
        }

        // 2. Classify as Test or Production
        var isTest = IsTest(relativePath);
        category = isTest ? FileCategory.Test : FileCategory.Production;

        // 3. If ExcludeTests is enabled, skip test files
        if (_settings.ExcludeTests && isTest)
        {
            return false;
        }

        // 4. Check user exclude patterns
        if (_excludeMatcher != null && _excludeMatcher.Match(relativePath).HasMatches)
        {
            return false;
        }

        // 5. Check user include patterns (if any specified)
        if (_includeMatcher != null && !_includeMatcher.Match(relativePath).HasMatches)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves the repository base directory from a target path by walking up parents
    /// looking for .git, .sln, .slnx, or codecondenser.json.
    /// </summary>
    public static string ResolveBaseDirectory(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return Directory.GetCurrentDirectory();
        }

        string startDir;
        if (File.Exists(targetPath))
        {
            startDir = Path.GetDirectoryName(Path.GetFullPath(targetPath)) ?? Directory.GetCurrentDirectory();
        }
        else if (Directory.Exists(targetPath))
        {
            startDir = Path.GetFullPath(targetPath);
        }
        else
        {
            startDir = Directory.GetCurrentDirectory();
        }

        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                File.Exists(Path.Combine(dir.FullName, CodeCondenserConfigurationLoader.DefaultConfigFileName)) ||
                dir.EnumerateFiles("*.sln").Any() ||
                dir.EnumerateFiles("*.slnx").Any())
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return startDir;
    }

    /// <summary>
    /// Normalizes a glob pattern by converting backslashes to forward slashes and stripping leading './' and '/'.
    /// </summary>
    public static string NormalizePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return string.Empty;
        }

        var p = pattern.Trim().Replace('\\', '/');
        while (p.StartsWith("./", StringComparison.Ordinal))
        {
            p = p[2..];
        }

        return p.TrimStart('/');
    }

    private static HashSet<string>? ExtractRootIncludePrefixes(IEnumerable<string> includePatterns)
    {
        var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pattern in includePatterns)
        {
            var clean = NormalizePattern(pattern);
            if (string.IsNullOrWhiteSpace(clean))
            {
                continue;
            }

            if (clean.StartsWith('*'))
            {
                // Recursive or wildcard root, cannot prune prefixes
                return null;
            }

            var firstSlash = clean.IndexOf('/');
            if (firstSlash <= 0)
            {
                return null;
            }

            var rootSegment = clean[..firstSlash];
            if (rootSegment.Contains('*') || rootSegment.Contains('?'))
            {
                return null;
            }

            prefixes.Add(rootSegment);
        }

        return prefixes.Count > 0 ? prefixes : null;
    }
}
