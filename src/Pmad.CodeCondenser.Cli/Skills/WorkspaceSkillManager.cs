using System.Reflection;
using System.Text;
using Pmad.CodeCondenser.Core.Filtering;

namespace Pmad.CodeCondenser.Cli.Skills;

/// <summary>
/// Status of an individual skill file operation.
/// </summary>
public enum SkillFileStatus
{
    Created,
    Updated,
    UpToDate,
    Removed
}

/// <summary>
/// Detailed result for a specific skill file operation.
/// </summary>
public sealed record SkillFileResult(string RelativePath, string FullPath, SkillFileStatus Status);

/// <summary>
/// Aggregated result of installing or updating workspace skills.
/// </summary>
public sealed record SkillInstallResult(
    string TargetDirectory,
    IReadOnlyList<SkillFileResult> Files,
    int CreatedCount,
    int UpdatedCount,
    int UpToDateCount,
    int RemovedCount)
{
    public bool IsAllUpToDate => CreatedCount == 0 && UpdatedCount == 0 && RemovedCount == 0;
}

/// <summary>
/// Configuration options for the skills installer.
/// </summary>
public sealed class SkillInstallOptions
{
    public bool DryRun { get; set; }

    public bool Force { get; set; }

    public bool Clean { get; set; }

    public string? SkillName { get; set; }
}

/// <summary>
/// Manages the discovery and deployment of bundled AI agent skills into target workspaces.
/// </summary>
public static class WorkspaceSkillManager
{
    public const string ResourcePrefix = "skills/";
    public const string DefaultSkillsSubdirectory = ".agents/skills";

    /// <summary>
    /// Resolves the destination directory for workspace skills based on the target path or explicit override.
    /// </summary>
    public static string ResolveTargetSkillsDirectory(string targetPath, string? explicitSkillsDir = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitSkillsDir))
        {
            return Path.GetFullPath(explicitSkillsDir);
        }

        var normalizedTarget = (targetPath ?? ".").Trim();
        var fullTarget = Path.GetFullPath(normalizedTarget);

        var trimmed = fullTarget.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmed.EndsWith(Path.Combine(".agents", "skills"), StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".agents/skills", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".agents\\skills", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var baseDir = PathFilter.ResolveBaseDirectory(normalizedTarget);
        return Path.Combine(baseDir, ".agents", "skills");
    }

    /// <summary>
    /// Gets the standard global skills directories: ~/.gemini/config/skills and ~/.agents/skills.
    /// </summary>
    public static IReadOnlyList<string> GetGlobalSkillsDirectories(string? userProfilePath = null)
    {
        var userProfile = !string.IsNullOrWhiteSpace(userProfilePath)
            ? userProfilePath
            : Environment.GetEnvironmentVariable("CODECONDENSER_USER_PROFILE") is { Length: > 0 } envProfile
                ? envProfile
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrWhiteSpace(userProfile))
        {
            return [];
        }

        var dirs = new List<string>
        {
            Path.Combine(userProfile, ".gemini", "config", "skills"),
            Path.Combine(userProfile, ".agents", "skills")
        };

        return dirs
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Resolves the destination directories for skills based on target path, explicit override, or global flag.
    /// </summary>
    public static IReadOnlyList<string> ResolveTargetSkillsDirectories(
        string targetPath,
        string? explicitSkillsDir = null,
        bool isGlobal = false,
        string? userProfilePath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitSkillsDir))
        {
            return [Path.GetFullPath(explicitSkillsDir)];
        }

        if (isGlobal)
        {
            return GetGlobalSkillsDirectories(userProfilePath);
        }

        return [ResolveTargetSkillsDirectory(targetPath)];
    }

    /// <summary>
    /// Installs or updates bundled skills across multiple target skill directories.
    /// </summary>
    public static IReadOnlyList<SkillInstallResult> InstallSkillsToDirectories(
        IEnumerable<string> targetDirectories,
        SkillInstallOptions? options = null,
        Assembly? resourceAssembly = null)
    {
        ArgumentNullException.ThrowIfNull(targetDirectories);
        var results = new List<SkillInstallResult>();
        foreach (var dir in targetDirectories)
        {
            results.Add(InstallSkills(dir, options, resourceAssembly));
        }
        return results;
    }

    /// <summary>
    /// Gets the list of distinct skill names available in the bundled assembly resources.
    /// </summary>
    public static IReadOnlyList<string> GetAvailableSkillNames(Assembly? assembly = null)
    {
        assembly ??= typeof(WorkspaceSkillManager).Assembly;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var resName in assembly.GetManifestResourceNames())
        {
            var normalized = resName.Replace('\\', '/');
            if (normalized.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var rel = normalized[ResourcePrefix.Length..];
                var slashIndex = rel.IndexOf('/');
                if (slashIndex > 0)
                {
                    names.Add(rel[..slashIndex]);
                }
            }
        }

        return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Installs or updates bundled skills in the target skills directory.
    /// </summary>
    public static SkillInstallResult InstallSkills(
        string targetSkillsDirectory,
        SkillInstallOptions? options = null,
        Assembly? resourceAssembly = null)
    {
        ArgumentNullException.ThrowIfNull(targetSkillsDirectory);
        options ??= new SkillInstallOptions();
        resourceAssembly ??= typeof(WorkspaceSkillManager).Assembly;

        var fullTargetDir = Path.GetFullPath(targetSkillsDirectory);
        var resourceEntries = DiscoverSkillResources(resourceAssembly, options.SkillName);

        var fileResults = new List<SkillFileResult>();
        var bundledTargetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int createdCount = 0;
        int updatedCount = 0;
        int upToDateCount = 0;
        int removedCount = 0;

        foreach (var (relPath, originalResName) in resourceEntries)
        {
            var targetFilePath = Path.Combine(fullTargetDir, relPath.Replace('/', Path.DirectorySeparatorChar));
            bundledTargetPaths.Add(targetFilePath);

            byte[] resourceBytes;
            using (var stream = resourceAssembly.GetManifestResourceStream(originalResName))
            {
                if (stream == null)
                {
                    continue;
                }

                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                resourceBytes = ms.ToArray();
            }

            if (!File.Exists(targetFilePath))
            {
                if (!options.DryRun)
                {
                    var fileDir = Path.GetDirectoryName(targetFilePath);
                    if (!string.IsNullOrEmpty(fileDir))
                    {
                        Directory.CreateDirectory(fileDir);
                    }
                    File.WriteAllBytes(targetFilePath, resourceBytes);
                }

                createdCount++;
                fileResults.Add(new SkillFileResult(relPath, targetFilePath, SkillFileStatus.Created));
            }
            else
            {
                var diskBytes = File.ReadAllBytes(targetFilePath);
                var isIdentical = !options.Force && (diskBytes.AsSpan().SequenceEqual(resourceBytes) || AreEquivalent(diskBytes, resourceBytes));

                if (isIdentical)
                {
                    upToDateCount++;
                    fileResults.Add(new SkillFileResult(relPath, targetFilePath, SkillFileStatus.UpToDate));
                }
                else
                {
                    if (!options.DryRun)
                    {
                        File.WriteAllBytes(targetFilePath, resourceBytes);
                    }

                    updatedCount++;
                    fileResults.Add(new SkillFileResult(relPath, targetFilePath, SkillFileStatus.Updated));
                }
            }
        }

        // Clean obsolete files if requested
        if (options.Clean && Directory.Exists(fullTargetDir))
        {
            var skillsToClean = !string.IsNullOrWhiteSpace(options.SkillName)
                ? new[] { options.SkillName }
                : GetAvailableSkillNames(resourceAssembly);

            foreach (var skill in skillsToClean)
            {
                var skillDir = Path.Combine(fullTargetDir, skill);
                if (Directory.Exists(skillDir))
                {
                    foreach (var existingFile in Directory.EnumerateFiles(skillDir, "*", SearchOption.AllDirectories))
                    {
                        if (!bundledTargetPaths.Contains(existingFile))
                        {
                            var rel = Path.GetRelativePath(fullTargetDir, existingFile).Replace('\\', '/');
                            if (!options.DryRun)
                            {
                                File.Delete(existingFile);
                            }

                            removedCount++;
                            fileResults.Add(new SkillFileResult(rel, existingFile, SkillFileStatus.Removed));
                        }
                    }
                }
            }
        }

        return new SkillInstallResult(
            fullTargetDir,
            fileResults,
            createdCount,
            updatedCount,
            upToDateCount,
            removedCount);
    }

    private static List<(string RelPath, string OriginalResName)> DiscoverSkillResources(
        Assembly assembly,
        string? skillNameFilter)
    {
        var entries = new List<(string RelPath, string OriginalResName)>();

        foreach (var resName in assembly.GetManifestResourceNames())
        {
            var normalized = resName.Replace('\\', '/');
            if (normalized.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var rel = normalized[ResourcePrefix.Length..];
                if (!string.IsNullOrWhiteSpace(skillNameFilter))
                {
                    var prefix = skillNameFilter.Trim().TrimEnd('/') + "/";
                    if (!rel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                entries.Add((rel, resName));
            }
        }

        return entries.OrderBy(e => e.RelPath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool AreEquivalent(byte[] a, byte[] b)
    {
        var textA = NormalizeContent(a);
        var textB = NormalizeContent(b);
        return string.Equals(textA, textB, StringComparison.Ordinal);
    }

    private static string NormalizeContent(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text[1..];
        }
        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }
}
