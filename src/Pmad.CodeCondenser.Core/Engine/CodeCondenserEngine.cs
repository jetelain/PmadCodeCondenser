using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Clones;
using Pmad.CodeCondenser.Core.Comments;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Core.Snapshots;

namespace Pmad.CodeCondenser.Core.Engine;

/// <summary>
/// Top-level engine orchestrating Roslyn AST parsing, clone detection, comment extraction/summarization,
/// and snapshot management.
/// </summary>
public sealed class CodeCondenserEngine : ICodeCondenserEngine
{
    private readonly ICloneDetector _cloneDetector;
    private readonly ICommentExtractor _commentExtractor;
    private readonly ICommentRewriter _commentRewriter;
    private readonly ISnapshotDiffer _snapshotDiffer;
    private readonly ISnapshotStore? _snapshotStore;
    private readonly ICommentSummarizer? _commentSummarizer;
    private readonly IFileHasher _fileHasher;
    private string? _lastBaseDirectory;

    /// <summary>
    /// Event raised when a file's comments have been rewritten and saved to disk.
    /// Passes the modified file's path.
    /// </summary>
    public event Action<string>? FileApplied;

    /// <summary>
    /// Event raised when comment summarization progress is updated (completed count, total count).
    /// </summary>
    public event Action<CommentSummarizationProgress>? CommentSummarizationProgress;

    public CodeCondenserEngine(
        ICloneDetector? cloneDetector = null,
        ICommentExtractor? commentExtractor = null,
        ICommentRewriter? commentRewriter = null,
        ISnapshotDiffer? snapshotDiffer = null,
        ISnapshotStore? snapshotStore = null,
        ICommentSummarizer? commentSummarizer = null,
        IFileHasher? fileHasher = null)
    {
        _cloneDetector = cloneDetector ?? new CloneDetector();
        _commentExtractor = commentExtractor ?? new VerboseCommentExtractor();
        _commentRewriter = commentRewriter ?? new VerboseCommentRewriter();
        _snapshotDiffer = snapshotDiffer ?? new SnapshotDiffer(fileHasher);
        _snapshotStore = snapshotStore;
        _commentSummarizer = commentSummarizer;
        _fileHasher = fileHasher ?? new FileHasher();
    }

    /// <inheritdoc />
    public Task<AnalysisResult> AnalyzeAsync(
        string targetPath,
        CodeCondenserSettings settings,
        bool incremental = false,
        CancellationToken cancellationToken = default)
    {
        return AnalyzeAsync(targetPath, settings, incremental, progress: null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AnalysisResult> AnalyzeAsync(
        string targetPath,
        CodeCondenserSettings settings,
        bool incremental,
        IProgress<CommentSummarizationProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        settings ??= new CodeCondenserSettings();

        var (baseDirectory, discoveredFiles) = ResolveFiles(targetPath);
        _lastBaseDirectory = baseDirectory;

        // 1. Compute file hashes for all discovered files in parallel
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
            CancellationToken = cancellationToken
        };

        var currentFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fileToNormalizedPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fileHashPairs = new (string FullPath, string NormalizedRelPath, string Hash)[discoveredFiles.Count];

        await Parallel.ForEachAsync(
            discoveredFiles.Select((f, idx) => (f, idx)),
            parallelOptions,
            async (item, ct) =>
            {
                var normalizedRelPath = _fileHasher.NormalizeRelativePath(baseDirectory, item.f);
                var hash = await _fileHasher.ComputeFileHashAsync(item.f, ct).ConfigureAwait(false);
                fileHashPairs[item.idx] = (item.f, normalizedRelPath, hash);
            }).ConfigureAwait(false);

        foreach (var (fullPath, normalizedRelPath, hash) in fileHashPairs)
        {
            currentFileHashes[normalizedRelPath] = hash;
            fileToNormalizedPath[fullPath] = normalizedRelPath;
        }

        // 2. Load baseline snapshot if store is available
        var snapshotPath = ResolveSnapshotPath(baseDirectory, settings.SnapshotFilePath);
        AnalysisSnapshot? baseline = null;
        if (_snapshotStore != null)
        {
            baseline = await _snapshotStore.LoadAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
        }

        // 3. Initialize WYSIWYG comment cache from baseline
        var cache = WysiwygCommentCache.FromSnapshot(baseline);

        // 4. Determine files to scan
        FileDiffResult? fileDiff = null;
        List<string> filesToScan;

        if (incremental && baseline != null)
        {
            fileDiff = _snapshotDiffer.DiffFiles(currentFileHashes, baseline);
            var changedSet = new HashSet<string>(fileDiff.AddedFiles.Concat(fileDiff.ModifiedFiles), StringComparer.OrdinalIgnoreCase);
            filesToScan = discoveredFiles.Where(f => changedSet.Contains(fileToNormalizedPath[f])).ToList();
        }
        else
        {
            filesToScan = discoveredFiles;
        }

        // 5. Parse syntax trees and extract comments & methods in parallel (if features are enabled)
        var fileResults = new (List<VerboseComment> Comments, List<MethodCloneInfo> Methods)[filesToScan.Count];

        if (settings.EnableCommentSummarization || settings.EnableDuplicationDetection)
        {
            await Parallel.ForEachAsync(
                filesToScan.Select((f, idx) => (f, idx)),
                parallelOptions,
                async (item, ct) =>
                {
                    var file = item.f;
                    var normalizedRelPath = fileToNormalizedPath[file];
                    var sourceText = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                    var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: file, cancellationToken: ct);

                    var comments = settings.EnableCommentSummarization
                        ? _commentExtractor.Extract(syntaxTree, file, settings).ToList()
                        : [];

                    var methods = settings.EnableDuplicationDetection
                        ? _cloneDetector.ExtractMethods(syntaxTree, normalizedRelPath, settings).ToList()
                        : [];

                    fileResults[item.idx] = (comments, methods);
                }).ConfigureAwait(false);
        }

        var allExtractedComments = new List<VerboseComment>();
        var allAnalyzedMethods = new List<MethodCloneInfo>();

        foreach (var (comments, methods) in fileResults)
        {
            if (comments != null) allExtractedComments.AddRange(comments);
            if (methods != null) allAnalyzedMethods.AddRange(methods);
        }

        // 6. Detect Clones
        IReadOnlyList<CodeCloneGroup> cloneGroups;
        if (!settings.EnableDuplicationDetection)
        {
            cloneGroups = [];
        }
        else if (incremental && baseline != null)
        {
            // Include methods from unchanged files from baseline so clones between modified and unchanged files are caught
            var combinedMethods = new List<MethodCloneInfo>(allAnalyzedMethods);
            if (fileDiff != null)
            {
                var unchangedSet = new HashSet<string>(fileDiff.UnchangedFiles, StringComparer.OrdinalIgnoreCase);
                foreach (var (key, hash) in baseline.MethodHashes)
                {
                    if (MethodKeyHelper.TryParseKey(key, out var path, out var signature) && unchangedSet.Contains(path))
                    {
                        combinedMethods.Add(new MethodCloneInfo(
                            FilePath: path,
                            MethodName: signature,
                            StartLine: 1,
                            EndLine: 1,
                            StatementCount: 0,
                            NormalizedHash: hash,
                            Signature: signature));
                    }
                }
            }

            var allGroups = _cloneDetector.GroupClones(combinedMethods);
            cloneGroups = _snapshotDiffer.FilterNewCloneGroups(allGroups, baseline, fileDiff);
        }
        else
        {
            cloneGroups = _cloneDetector.GroupClones(allAnalyzedMethods);
        }

        // 7. Filter & Summarize comments via LLM
        if (settings.EnableCommentSummarization && _commentSummarizer != null)
        {
            var commentsToSummarize = (incremental && baseline != null)
                ? _snapshotDiffer.FilterNewComments(allExtractedComments, baseline)
                : allExtractedComments;

            if (commentsToSummarize.Count > 0)
            {
                var progressHandler = new Progress<CommentSummarizationProgress>(p =>
                {
                    progress?.Report(p);
                    CommentSummarizationProgress?.Invoke(p);
                });

                var summaries = await _commentSummarizer.SummarizeAsync(
                    commentsToSummarize,
                    cache.ToDictionary(),
                    settings,
                    progress: progressHandler,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                cache.SetRange(summaries);
            }
        }

        // 8. Persist/update snapshot WYSIWYG cache and file baseline
        if (_snapshotStore != null)
        {
            // When duplication detection is disabled, preserve baseline methods instead of clearing them
            var methodsForSnapshot = settings.EnableDuplicationDetection
                ? allAnalyzedMethods
                : (baseline != null ? [] : allAnalyzedMethods);

            // When comment summarization is disabled, preserve baseline comment cache
            var cacheForSnapshot = settings.EnableCommentSummarization
                ? cache.ToDictionary()
                : (baseline?.CommentCache ?? cache.ToDictionary());

            var newSnapshot = _snapshotDiffer.CreateOrUpdateSnapshot(
                baseline,
                currentFileHashes,
                methodsForSnapshot,
                cacheForSnapshot);

            // If duplication detection was disabled, restore baseline method hashes
            if (!settings.EnableDuplicationDetection && baseline?.MethodHashes != null)
            {
                foreach (var (k, v) in baseline.MethodHashes)
                {
                    newSnapshot.MethodHashes[k] = v;
                }
            }

            await _snapshotStore.SaveAsync(snapshotPath, newSnapshot, cancellationToken).ConfigureAwait(false);
        }

        return new AnalysisResult
        {
            VerboseComments = allExtractedComments,
            CloneGroups = cloneGroups,
            SummarizedComments = cache.ToDictionary(),
            AnalyzedFilesCount = filesToScan.Count,
            TotalMethodsScanned = allAnalyzedMethods.Count
        };
    }

    /// <inheritdoc />
    public async Task ApplySummariesAsync(
        AnalysisResult analysisResult,
        CodeCondenserSettings settings,
        CancellationToken cancellationToken = default)
    {
        await ApplySummariesWithModifiedFilesAsync(analysisResult, settings, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Rewrites source files on disk applying the cached comment summaries, and returns the list of modified file paths.
    /// </summary>
    public async Task<IReadOnlyList<string>> ApplySummariesWithModifiedFilesAsync(
        AnalysisResult analysisResult,
        CodeCondenserSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysisResult);
        settings ??= new CodeCondenserSettings();

        var modifiedFiles = new List<string>();
        if (!settings.EnableCommentSummarization || analysisResult.VerboseComments.Count == 0 || analysisResult.SummarizedComments.Count == 0)
        {
            return modifiedFiles;
        }

        var commentsByFile = analysisResult.VerboseComments
            .GroupBy(c => c.FilePath, StringComparer.OrdinalIgnoreCase);

        foreach (var group in commentsByFile)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = group.Key;
            var fullPath = Path.IsPathRooted(filePath)
                ? Path.GetFullPath(filePath)
                : Path.GetFullPath(Path.Combine(_lastBaseDirectory ?? Directory.GetCurrentDirectory(), filePath));

            if (!File.Exists(fullPath))
            {
                continue;
            }

            var originalText = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
            var tree = CSharpSyntaxTree.ParseText(originalText, path: fullPath, cancellationToken: cancellationToken);

            var newTree = _commentRewriter.Rewrite(tree, analysisResult.SummarizedComments);
            var newText = newTree.GetText(cancellationToken).ToString();

            if (!string.Equals(originalText, newText, StringComparison.Ordinal))
            {
                await File.WriteAllTextAsync(fullPath, newText, cancellationToken).ConfigureAwait(false);
                modifiedFiles.Add(fullPath);
                FileApplied?.Invoke(fullPath);
            }
        }

        return modifiedFiles;
    }

    /// <summary>
    /// Forces a snapshot baseline update for the given target path and current settings.
    /// </summary>
    public async Task<AnalysisSnapshot?> UpdateSnapshotBaselineAsync(
        string targetPath,
        CodeCondenserSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (_snapshotStore == null)
        {
            return null;
        }

        var (baseDirectory, discoveredFiles) = ResolveFiles(targetPath);
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
            CancellationToken = cancellationToken
        };

        var baselineResults = new (string RelPath, string Hash, List<MethodCloneInfo> Methods)[discoveredFiles.Count];

        await Parallel.ForEachAsync(
            discoveredFiles.Select((f, idx) => (f, idx)),
            parallelOptions,
            async (item, ct) =>
            {
                var file = item.f;
                var relPath = _fileHasher.NormalizeRelativePath(baseDirectory, file);
                var hash = await _fileHasher.ComputeFileHashAsync(file, ct).ConfigureAwait(false);

                List<MethodCloneInfo> methods = [];
                if (settings.EnableDuplicationDetection)
                {
                    var sourceText = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                    var tree = CSharpSyntaxTree.ParseText(sourceText, path: file, cancellationToken: ct);
                    methods = _cloneDetector.ExtractMethods(tree, relPath, settings).ToList();
                }

                baselineResults[item.idx] = (relPath, hash, methods);
            }).ConfigureAwait(false);

        var currentFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var allMethods = new List<MethodCloneInfo>();

        foreach (var (relPath, hash, methods) in baselineResults)
        {
            currentFileHashes[relPath] = hash;
            if (methods != null) allMethods.AddRange(methods);
        }

        var snapshotPath = ResolveSnapshotPath(baseDirectory, settings.SnapshotFilePath);
        var baseline = await _snapshotStore.LoadAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
        var commentCache = baseline?.CommentCache ?? new Dictionary<string, string>();

        var methodsForSnapshot = settings.EnableDuplicationDetection
            ? allMethods
            : (baseline != null ? [] : allMethods);

        var newSnapshot = _snapshotDiffer.CreateOrUpdateSnapshot(
            baseline,
            currentFileHashes,
            methodsForSnapshot,
            commentCache);

        if (!settings.EnableDuplicationDetection && baseline?.MethodHashes != null)
        {
            foreach (var (k, v) in baseline.MethodHashes)
            {
                newSnapshot.MethodHashes[k] = v;
            }
        }

        await _snapshotStore.SaveAsync(snapshotPath, newSnapshot, cancellationToken).ConfigureAwait(false);
        return newSnapshot;
    }

    private static (string BaseDirectory, List<string> Files) ResolveFiles(string targetPath)
    {
        var fullPath = Path.GetFullPath(targetPath);

        if (File.Exists(fullPath))
        {
            var dir = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
            return (dir, new List<string> { fullPath });
        }

        if (Directory.Exists(fullPath))
        {
            var files = DiscoverCSharpFiles(fullPath);
            return (fullPath, files);
        }

        throw new DirectoryNotFoundException($"Target path '{targetPath}' was not found.");
    }

    private static List<string> DiscoverCSharpFiles(string directory)
    {
        var results = new List<string>();
        var ignoredDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", ".git", ".vs", ".codecondenser", "node_modules", "testresults"
        };

        void Traverse(string currentDir)
        {
            var dirInfo = new DirectoryInfo(currentDir);
            if (ignoredDirs.Contains(dirInfo.Name) || (dirInfo.Name.StartsWith('.') && dirInfo.Name.Length > 1))
            {
                return;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(currentDir, "*.cs"))
                {
                    results.Add(Path.GetFullPath(file));
                }

                foreach (var subDir in Directory.EnumerateDirectories(currentDir))
                {
                    Traverse(subDir);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        Traverse(directory);
        results.Sort(StringComparer.OrdinalIgnoreCase);
        return results;
    }

    private static string ResolveSnapshotPath(string baseDirectory, string snapshotFilePath)
    {
        if (Path.IsPathRooted(snapshotFilePath))
        {
            return snapshotFilePath;
        }

        return Path.Combine(baseDirectory, snapshotFilePath);
    }
}
