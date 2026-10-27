using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Snapshots;

/// <summary>
/// Implements incremental diffing logic for workspace files, methods, clone clusters, and comments.
/// </summary>
public sealed class SnapshotDiffer : ISnapshotDiffer
{
    private readonly IFileHasher _fileHasher;

    /// <inheritdoc />
    public event Action<string>? InfoLogged;

    public SnapshotDiffer(IFileHasher? fileHasher = null)
    {
        _fileHasher = fileHasher ?? new FileHasher();
    }

    /// <inheritdoc />
    public FileDiffResult DiffFiles(
        IReadOnlyDictionary<string, string> currentFileHashes,
        AnalysisSnapshot? baseline)
    {
        ArgumentNullException.ThrowIfNull(currentFileHashes);

        if (baseline == null)
        {
            var allAdded = currentFileHashes.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
            return new FileDiffResult(
                AddedFiles: allAdded,
                ModifiedFiles: [],
                UnchangedFiles: [],
                DeletedFiles: []);
        }

        var added = new List<string>();
        var modified = new List<string>();
        var unchanged = new List<string>();
        var deleted = new List<string>();

        foreach (var (path, hash) in currentFileHashes)
        {
            if (baseline.Files.TryGetValue(path, out var info))
            {
                if (string.Equals(info.FileHash, hash, StringComparison.OrdinalIgnoreCase))
                {
                    unchanged.Add(path);
                }
                else
                {
                    modified.Add(path);
                }
            }
            else
            {
                added.Add(path);
            }
        }

        foreach (var path in baseline.Files.Keys)
        {
            if (!currentFileHashes.ContainsKey(path))
            {
                deleted.Add(path);
            }
        }

        added.Sort(StringComparer.OrdinalIgnoreCase);
        modified.Sort(StringComparer.OrdinalIgnoreCase);
        unchanged.Sort(StringComparer.OrdinalIgnoreCase);
        deleted.Sort(StringComparer.OrdinalIgnoreCase);

        return new FileDiffResult(added, modified, unchanged, deleted);
    }

    /// <inheritdoc />
    public async Task<FileDiffResult> DiffFilesAsync(
        string basePath,
        IEnumerable<string> filePaths,
        AnalysisSnapshot? baseline,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentNullException.ThrowIfNull(filePaths);

        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in filePaths)
        {
            var normalized = _fileHasher.NormalizeRelativePath(basePath, file);
            var hash = await _fileHasher.ComputeFileHashAsync(file, cancellationToken).ConfigureAwait(false);
            hashes[normalized] = hash;
        }

        return DiffFiles(hashes, baseline);
    }

    /// <inheritdoc />
    public MethodDiffResult DiffMethods(
        IEnumerable<MethodCloneInfo> currentMethods,
        AnalysisSnapshot? baseline)
    {
        ArgumentNullException.ThrowIfNull(currentMethods);

        var methodsList = currentMethods as IReadOnlyList<MethodCloneInfo> ?? currentMethods.ToList();

        if (baseline == null)
        {
            return new MethodDiffResult(
                NewMethods: methodsList,
                ModifiedMethods: [],
                UnchangedMethods: []);
        }

        if (baseline.NormalizerVersion < AnalysisSnapshot.CurrentNormalizerVersion)
        {
            InfoLogged?.Invoke($"[Info] Normalizer version upgraded (v{baseline.NormalizerVersion} -> v{AnalysisSnapshot.CurrentNormalizerVersion}). Baseline snapshot was created with an older normalizer version.");
        }

        var newMethods = new List<MethodCloneInfo>();
        var modifiedMethods = new List<MethodCloneInfo>();
        var unchangedMethods = new List<MethodCloneInfo>();

        foreach (var method in methodsList)
        {
            var keySignature = MethodKeyHelper.BuildKey(method);
            var keyName = MethodKeyHelper.BuildKey(method.FilePath, method.MethodName);

            if (baseline.MethodHashes.TryGetValue(keySignature, out var oldHash) ||
                baseline.MethodHashes.TryGetValue(keyName, out oldHash))
            {
                if (string.Equals(oldHash, method.NormalizedHash, StringComparison.OrdinalIgnoreCase))
                {
                    unchangedMethods.Add(method);
                }
                else
                {
                    modifiedMethods.Add(method);
                }
            }
            else
            {
                newMethods.Add(method);
            }
        }

        return new MethodDiffResult(newMethods, modifiedMethods, unchangedMethods);
    }

    /// <inheritdoc />
    public IReadOnlyList<CodeCloneGroup> FilterNewCloneGroups(
        IEnumerable<CodeCloneGroup> currentGroups,
        AnalysisSnapshot? baseline,
        FileDiffResult? fileDiff = null)
    {
        ArgumentNullException.ThrowIfNull(currentGroups);

        if (baseline == null)
        {
            return currentGroups.ToList();
        }

        var results = new List<CodeCloneGroup>();

        foreach (var group in currentGroups)
        {
            // A group is new if at least one method clone in the group was introduced or modified since baseline
            if (group.Methods.Any(m => IsMethodNewOrModified(m, baseline, fileDiff)))
            {
                results.Add(group);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public IReadOnlyList<VerboseComment> FilterNewComments(
        IEnumerable<VerboseComment> comments,
        AnalysisSnapshot? baseline)
    {
        ArgumentNullException.ThrowIfNull(comments);

        if (baseline == null)
        {
            return comments.ToList();
        }

        return comments.Where(c => !baseline.CommentCache.ContainsKey(c.ContentHash)).ToList();
    }

    /// <inheritdoc />
    public AnalysisSnapshot CreateOrUpdateSnapshot(
        AnalysisSnapshot? baseline,
        IReadOnlyDictionary<string, string> currentFileHashes,
        IEnumerable<MethodCloneInfo> analyzedMethods,
        IReadOnlyDictionary<string, string> commentCache,
        IEnumerable<string>? typeViolationFiles = null,
        bool isIncremental = false)
    {
        ArgumentNullException.ThrowIfNull(currentFileHashes);
        ArgumentNullException.ThrowIfNull(analyzedMethods);
        ArgumentNullException.ThrowIfNull(commentCache);

        var snapshot = new AnalysisSnapshot
        {
            Version = baseline?.Version ?? 1,
            NormalizerVersion = baseline?.NormalizerVersion ?? AnalysisSnapshot.CurrentNormalizerVersion,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (isIncremental)
        {
            if (typeViolationFiles != null)
            {
                foreach (var f in typeViolationFiles)
                {
                    if (currentFileHashes.ContainsKey(f))
                    {
                        snapshot.TypeViolationFiles.Add(f);
                    }
                }

                // In incremental update with a baseline, preserve violations for unchanged files that were not re-analyzed
                if (baseline != null)
                {
                    foreach (var f in baseline.TypeViolationFiles)
                    {
                        if (baseline.Files.TryGetValue(f, out var oldFileInfo) &&
                            currentFileHashes.TryGetValue(f, out var currentHash) &&
                            string.Equals(oldFileInfo.FileHash, currentHash, StringComparison.OrdinalIgnoreCase))
                        {
                            snapshot.TypeViolationFiles.Add(f);
                        }
                    }
                }
            }
            else if (baseline != null)
            {
                foreach (var f in baseline.TypeViolationFiles)
                {
                    if (currentFileHashes.ContainsKey(f))
                    {
                        snapshot.TypeViolationFiles.Add(f);
                    }
                }
            }
        }
        else
        {
            // Full scan / baseline update: typeViolationFiles is authoritative
            if (typeViolationFiles != null)
            {
                foreach (var f in typeViolationFiles)
                {
                    if (currentFileHashes.ContainsKey(f))
                    {
                        snapshot.TypeViolationFiles.Add(f);
                    }
                }
            }
        }

        // 1. Files
        foreach (var (path, hash) in currentFileHashes)
        {
            if (baseline != null &&
                baseline.Files.TryGetValue(path, out var oldInfo) &&
                string.Equals(oldInfo.FileHash, hash, StringComparison.OrdinalIgnoreCase))
            {
                snapshot.Files[path] = new FileSnapshotInfo
                {
                    FileHash = hash,
                    LastAnalyzed = oldInfo.LastAnalyzed
                };
            }
            else
            {
                snapshot.Files[path] = new FileSnapshotInfo
                {
                    FileHash = hash,
                    LastAnalyzed = DateTimeOffset.UtcNow
                };
            }
        }

        // 2. Method Hashes
        var methodsList = analyzedMethods as IReadOnlyList<MethodCloneInfo> ?? analyzedMethods.ToList();

        // Preserve method hashes for unchanged files that were not re-analyzed
        if (baseline != null)
        {
            foreach (var (key, hash) in baseline.MethodHashes)
            {
                var filePath = MethodKeyHelper.GetFilePath(key);
                if (!string.IsNullOrEmpty(filePath))
                {
                    if (baseline.Files.TryGetValue(filePath, out var oldFileInfo) &&
                        currentFileHashes.TryGetValue(filePath, out var currentHash) &&
                        string.Equals(oldFileInfo.FileHash, currentHash, StringComparison.OrdinalIgnoreCase))
                    {
                        snapshot.MethodHashes[key] = hash;
                    }
                }
            }
        }

        // Add method hashes for analyzed methods
        foreach (var method in methodsList)
        {
            var key = MethodKeyHelper.BuildKey(method);
            snapshot.MethodHashes[key] = method.NormalizedHash;
        }

        // 3. Comment Cache
        if (baseline != null)
        {
            foreach (var (key, value) in baseline.CommentCache)
            {
                snapshot.CommentCache[key] = value;
            }
        }

        foreach (var (key, value) in commentCache)
        {
            snapshot.CommentCache[key] = value;
        }

        return snapshot;
    }

    private static bool IsMethodNewOrModified(
        MethodCloneInfo method,
        AnalysisSnapshot baseline,
        FileDiffResult? fileDiff)
    {
        var normalizedPath = method.FilePath.Replace('\\', '/');

        // Check if method belongs to an added or modified file
        if (fileDiff != null)
        {
            if (fileDiff.AddedFiles.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase) ||
                fileDiff.ModifiedFiles.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var keySignature = MethodKeyHelper.BuildKey(method);
        var keyName = MethodKeyHelper.BuildKey(method.FilePath, method.MethodName);

        if (!baseline.MethodHashes.TryGetValue(keySignature, out var oldHash) &&
            !baseline.MethodHashes.TryGetValue(keyName, out oldHash))
        {
            return true; // Newly added method
        }

        return !string.Equals(oldHash, method.NormalizedHash, StringComparison.OrdinalIgnoreCase);
    }
}
