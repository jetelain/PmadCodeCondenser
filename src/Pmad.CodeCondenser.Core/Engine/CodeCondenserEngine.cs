using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Clones;
using Pmad.CodeCondenser.Core.Comments;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Filtering;
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
    private readonly ISummarizedCommentsStore? _summarizedCommentsStore;
    private readonly ICommentSummarizer? _commentSummarizer;
    private readonly IFileHasher _fileHasher;
    private readonly IProjectLocator _projectLocator;
    private readonly ISubMethodCacheStore? _subMethodCacheStore;
    private string? _lastBaseDirectory;

    /// <summary>
    /// Event raised when informational messages (e.g. normalizer upgrades) are emitted.
    /// </summary>
    public event Action<string>? InfoLogged;

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
        IFileHasher? fileHasher = null,
        ISummarizedCommentsStore? summarizedCommentsStore = null,
        IProjectLocator? projectLocator = null,
        ISubMethodCacheStore? subMethodCacheStore = null)
    {
        _cloneDetector = cloneDetector ?? new CloneDetector();
        _commentExtractor = commentExtractor ?? new VerboseCommentExtractor();
        _commentRewriter = commentRewriter ?? new VerboseCommentRewriter();
        _snapshotDiffer = snapshotDiffer ?? new SnapshotDiffer(fileHasher);
        _snapshotStore = snapshotStore;
        _summarizedCommentsStore = summarizedCommentsStore;
        _commentSummarizer = commentSummarizer;
        _fileHasher = fileHasher ?? new FileHasher();
        _projectLocator = projectLocator ?? new ProjectLocator();
        _subMethodCacheStore = subMethodCacheStore;
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
        var effectiveSettings = (settings ?? new CodeCondenserSettings()).Clone();
        if (effectiveSettings.SummaryMode == null && !effectiveSettings.IsLlmConfigured && _commentSummarizer != null)
        {
            if (effectiveSettings.EnableCommentSummarization)
            {
                effectiveSettings.SummaryMode = SummaryMode.Summarize;
            }
        }
        var effectiveSummaryMode = effectiveSettings.GetEffectiveSummaryMode();

        var (baseDirectory, discoveredFiles, fileCategories, skippedGeneratedCount) = ResolveFiles(targetPath, effectiveSettings);
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
        var snapshotPath = ResolveSnapshotPath(baseDirectory, effectiveSettings.SnapshotFilePath);
        AnalysisSnapshot? baseline = null;
        if (_snapshotStore != null)
        {
            baseline = await _snapshotStore.LoadAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
        }

        if (baseline != null && baseline.NormalizerVersion < AnalysisSnapshot.CurrentNormalizerVersion)
        {
            InfoLogged?.Invoke($"[Info] Normalizer version upgraded (v{baseline.NormalizerVersion} -> v{AnalysisSnapshot.CurrentNormalizerVersion}). Baseline snapshot was created with an older normalizer version.");
        }

        // 3. Initialize WYSIWYG comment cache from baseline
        var cache = WysiwygCommentCache.FromSnapshot(baseline);

        // Load stored summarized comments from git file if store is available
        var summarizedPath = ResolveSnapshotPath(baseDirectory, effectiveSettings.SummarizedCommentsFilePath);
        if (_summarizedCommentsStore != null)
        {
            var storedSummaries = await _summarizedCommentsStore.LoadAsync(summarizedPath, cancellationToken).ConfigureAwait(false);
            foreach (var hash in storedSummaries.Keys)
            {
                effectiveSettings.IgnoredCommentHashes.Add(hash);
            }
        }

        if (baseline?.CommentCache != null)
        {
            foreach (var summaryText in baseline.CommentCache.Values)
            {
                var summaryHash = VerboseComment.ComputeSha256(summaryText.Trim());
                effectiveSettings.IgnoredCommentHashes.Add(summaryHash);
            }
        }

        // 4. Determine files to scan
        FileDiffResult? fileDiff = null;
        List<string> filesToScan;

        if (incremental && baseline != null)
        {
            fileDiff = _snapshotDiffer.DiffFiles(currentFileHashes, baseline);
            var changedSet = new HashSet<string>(fileDiff.AddedFiles.Concat(fileDiff.ModifiedFiles), StringComparer.OrdinalIgnoreCase);
            filesToScan = discoveredFiles.Where(f => changedSet.Contains(fileToNormalizedPath[f])).ToList();

            if (baseline.NormalizerVersion < AnalysisSnapshot.CurrentNormalizerVersion && effectiveSettings.EnableDuplicationDetection)
            {
                var unchangedFiles = discoveredFiles.Where(f => !changedSet.Contains(fileToNormalizedPath[f])).ToList();
                var rebasedBaselineHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                await Parallel.ForEachAsync(
                    unchangedFiles,
                    parallelOptions,
                    async (file, ct) =>
                    {
                        var relPath = fileToNormalizedPath[file];
                        var sourceText = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: file, cancellationToken: ct);
                        var projectId = _projectLocator.LocateProjectId(file, baseDirectory);
                        var methods = _cloneDetector.ExtractMethods(syntaxTree, relPath, effectiveSettings, projectId);

                        lock (rebasedBaselineHashes)
                        {
                            foreach (var m in methods)
                            {
                                var key = MethodKeyHelper.BuildKey(m);
                                rebasedBaselineHashes[key] = m.NormalizedHash;
                            }
                        }
                    }).ConfigureAwait(false);

                baseline.MethodHashes = rebasedBaselineHashes;
                baseline.NormalizerVersion = AnalysisSnapshot.CurrentNormalizerVersion;
            }
        }
        else
        {
            filesToScan = discoveredFiles;
        }

        // 5. Parse syntax trees and extract comments & methods in parallel (if features are enabled)
        var fileResults = new (List<VerboseComment> Comments, List<MethodCloneInfo> Methods, List<SubMethodBlock> SubMethodBlocks)[filesToScan.Count];

        if (effectiveSummaryMode != SummaryMode.Disabled || effectiveSettings.EnableDuplicationDetection || effectiveSettings.EnableSubMethodCloneDetection)
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
                    var projectId = _projectLocator.LocateProjectId(file, baseDirectory);

                    var comments = effectiveSummaryMode != SummaryMode.Disabled
                        ? _commentExtractor.Extract(syntaxTree, file, effectiveSettings).ToList()
                        : [];

                    var methods = effectiveSettings.EnableDuplicationDetection
                        ? _cloneDetector.ExtractMethods(syntaxTree, normalizedRelPath, effectiveSettings, projectId).ToList()
                        : [];

                    var subMethodBlocks = effectiveSettings.EnableSubMethodCloneDetection
                        ? _cloneDetector.ExtractSubMethodBlocks(syntaxTree, normalizedRelPath, effectiveSettings, projectId).ToList()
                        : [];

                    fileResults[item.idx] = (comments, methods, subMethodBlocks);
                }).ConfigureAwait(false);
        }

        var allExtractedComments = new List<VerboseComment>();
        var allAnalyzedMethods = new List<MethodCloneInfo>();

        foreach (var (comments, methods, _) in fileResults)
        {
            if (comments != null) allExtractedComments.AddRange(comments);
            if (methods != null) allAnalyzedMethods.AddRange(methods);
        }

        // 6. Detect Clones
        IReadOnlyList<CodeCloneGroup> cloneGroups;
        PathFilter? fallbackFilter = null;
        Func<string, FileCategory> fileClassifier = path =>
        {
            if (fileCategories.TryGetValue(path, out var cat))
            {
                return cat;
            }
            var rel = path.Replace('\\', '/');
            if (fileCategories.TryGetValue(rel, out cat))
            {
                return cat;
            }
            fallbackFilter ??= new PathFilter(effectiveSettings, targetPath);
            return fallbackFilter.ClassifyFile(rel, path);
        };

        if (!effectiveSettings.EnableDuplicationDetection)
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
                        var fullOrRel = Path.Combine(baseDirectory, path);
                        var projId = _projectLocator.LocateProjectId(fullOrRel, baseDirectory);
                        combinedMethods.Add(new MethodCloneInfo(
                            FilePath: path,
                            MethodName: MethodKeyHelper.ExtractMethodName(signature),
                            StartLine: 1,
                            EndLine: 1,
                            StatementCount: 0,
                            NormalizedHash: hash,
                            Signature: signature,
                            ProjectId: projId));
                    }
                }
            }

            var allGroups = _cloneDetector.GroupClones(combinedMethods, fileClassifier);
            cloneGroups = _snapshotDiffer.FilterNewCloneGroups(allGroups, baseline, fileDiff);
        }
        else
        {
            cloneGroups = _cloneDetector.GroupClones(allAnalyzedMethods, fileClassifier);
        }

        // Sub-method sliding-window clone detection
        IReadOnlyList<SubMethodCloneGroup> subMethodGroups = [];

        if (effectiveSettings.EnableSubMethodCloneDetection)
        {
            var allSubMethodBlocks = new List<SubMethodBlock>();
            foreach (var item in fileResults)
            {
                if (item.SubMethodBlocks != null)
                {
                    allSubMethodBlocks.AddRange(item.SubMethodBlocks);
                }
            }

            var cachePath = ResolveSnapshotPath(baseDirectory, effectiveSettings.SubMethodCacheFilePath);
            SubMethodCacheModel? subMethodCache = null;
            if (_subMethodCacheStore != null)
            {
                subMethodCache = await _subMethodCacheStore.LoadAsync(cachePath, cancellationToken).ConfigureAwait(false);
            }

            if (subMethodCache == null || subMethodCache.NormalizerVersion < AnalysisSnapshot.CurrentNormalizerVersion)
            {
                subMethodCache = new SubMethodCacheModel
                {
                    Version = SubMethodCacheModel.CurrentVersion,
                    NormalizerVersion = AnalysisSnapshot.CurrentNormalizerVersion
                };
            }

            // In incremental mode with baseline, retrieve blocks for unchanged files from cache
            if (incremental && baseline != null && fileDiff != null)
            {
                var unchangedFilesToParse = new List<string>();

                foreach (var unchangedRel in fileDiff.UnchangedFiles)
                {
                    if (currentFileHashes.TryGetValue(unchangedRel, out var currentHash) &&
                        subMethodCache.Files.TryGetValue(unchangedRel, out var cacheEntry) &&
                        string.Equals(cacheEntry.FileHash, currentHash, StringComparison.OrdinalIgnoreCase) &&
                        cacheEntry.NormalizerVersion >= AnalysisSnapshot.CurrentNormalizerVersion)
                    {
                        foreach (var dto in cacheEntry.Blocks)
                        {
                            allSubMethodBlocks.Add(new SubMethodBlock
                            {
                                FilePath = unchangedRel,
                                MethodName = dto.MethodName,
                                BlockScopeId = dto.BlockScopeId,
                                ProjectId = dto.ProjectId,
                                MethodStartLine = dto.MethodStartLine,
                                StatementHashes = dto.StatementHashes,
                                StatementStartLines = dto.StatementStartLines,
                                StatementEndLines = dto.StatementEndLines
                            });
                        }
                    }
                    else
                    {
                        var fullPath = Path.Combine(baseDirectory, unchangedRel);
                        if (File.Exists(fullPath))
                        {
                            unchangedFilesToParse.Add(fullPath);
                        }
                    }
                }

                if (unchangedFilesToParse.Count > 0)
                {
                    await Parallel.ForEachAsync(
                        unchangedFilesToParse,
                        parallelOptions,
                        async (file, ct) =>
                        {
                            var relPath = fileToNormalizedPath.TryGetValue(file, out var r) ? r : _fileHasher.NormalizeRelativePath(baseDirectory, file);
                            var sourceText = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: file, cancellationToken: ct);
                            var projectId = _projectLocator.LocateProjectId(file, baseDirectory);
                            var blocks = _cloneDetector.ExtractSubMethodBlocks(syntaxTree, relPath, effectiveSettings, projectId);

                            lock (allSubMethodBlocks)
                            {
                                allSubMethodBlocks.AddRange(blocks);
                            }

                            if (currentFileHashes.TryGetValue(relPath, out var hash))
                            {
                                lock (subMethodCache.Files)
                                {
                                    subMethodCache.Files[relPath] = new FileSubMethodCacheEntry
                                    {
                                        FileHash = hash,
                                        NormalizerVersion = AnalysisSnapshot.CurrentNormalizerVersion,
                                        Blocks = blocks.Select(b => new SubMethodBlockCacheDto
                                        {
                                            MethodName = b.MethodName,
                                            BlockScopeId = b.BlockScopeId,
                                            ProjectId = b.ProjectId,
                                            MethodStartLine = b.MethodStartLine,
                                            StatementHashes = b.StatementHashes,
                                            StatementStartLines = b.StatementStartLines,
                                            StatementEndLines = b.StatementEndLines
                                        }).ToList()
                                    };
                                }
                            }
                        }).ConfigureAwait(false);
                }

                // Evict deleted files from cache
                foreach (var deletedRel in fileDiff.DeletedFiles)
                {
                    subMethodCache.Files.Remove(deletedRel);
                }
            }

            // Update cache entries for all analyzed files (including files with 0 blocks)
            for (int i = 0; i < filesToScan.Count; i++)
            {
                var file = filesToScan[i];
                var relPath = fileToNormalizedPath[file];
                var (_, _, blocks) = fileResults[i];

                if (currentFileHashes.TryGetValue(relPath, out var hash))
                {
                    subMethodCache.Files[relPath] = new FileSubMethodCacheEntry
                    {
                        FileHash = hash,
                        NormalizerVersion = AnalysisSnapshot.CurrentNormalizerVersion,
                        Blocks = blocks?.Select(b => new SubMethodBlockCacheDto
                        {
                            MethodName = b.MethodName,
                            BlockScopeId = b.BlockScopeId,
                            ProjectId = b.ProjectId,
                            MethodStartLine = b.MethodStartLine,
                            StatementHashes = b.StatementHashes,
                            StatementStartLines = b.StatementStartLines,
                            StatementEndLines = b.StatementEndLines
                        }).ToList() ?? []
                    };
                }
            }

            var allSubGroups = _cloneDetector.GroupSubMethodClones(
                allSubMethodBlocks,
                effectiveSettings.SubMethodStatementThreshold,
                cloneGroups,
                fileClassifier);

            if (incremental && baseline != null && fileDiff != null)
            {
                var changedSet = new HashSet<string>(fileDiff.AddedFiles.Concat(fileDiff.ModifiedFiles), StringComparer.OrdinalIgnoreCase);
                subMethodGroups = allSubGroups.Where(g => g.Occurrences.Any(o => changedSet.Contains(o.FilePath.Replace('\\', '/')))).ToList();
            }
            else
            {
                subMethodGroups = allSubGroups;
            }

            if (_subMethodCacheStore != null)
            {
                await _subMethodCacheStore.SaveAsync(cachePath, subMethodCache, cancellationToken).ConfigureAwait(false);
            }
        }

        // 7. Filter & Summarize comments via LLM
        if (effectiveSummaryMode == SummaryMode.Summarize && _commentSummarizer != null)
        {
            var commentsToSummarize = (incremental && baseline != null)
                ? _snapshotDiffer.FilterNewComments(allExtractedComments, baseline)
                : allExtractedComments;

            if (commentsToSummarize.Count > 0)
            {
                var progressHandler = new SynchronousProgress<CommentSummarizationProgress>(p =>
                {
                    progress?.Report(p);
                    CommentSummarizationProgress?.Invoke(p);
                });

                var summaries = await _commentSummarizer.SummarizeAsync(
                    commentsToSummarize,
                    cache.ToDictionary(),
                    effectiveSettings,
                    progress: progressHandler,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                cache.SetRange(summaries);
            }
        }

        // 8. Persist/update snapshot WYSIWYG cache and file baseline
        if (_snapshotStore != null)
        {
            // When duplication detection is disabled, preserve baseline methods instead of clearing them
            var methodsForSnapshot = effectiveSettings.EnableDuplicationDetection
                ? allAnalyzedMethods
                : (baseline != null ? [] : allAnalyzedMethods);

            // When comment summarization is not performed, preserve baseline comment cache
            var cacheForSnapshot = effectiveSummaryMode == SummaryMode.Summarize
                ? cache.ToDictionary()
                : (baseline?.CommentCache ?? cache.ToDictionary());

            var newSnapshot = _snapshotDiffer.CreateOrUpdateSnapshot(
                baseline,
                currentFileHashes,
                methodsForSnapshot,
                cacheForSnapshot);

            // If duplication detection was disabled, restore baseline method hashes
            if (!effectiveSettings.EnableDuplicationDetection && baseline?.MethodHashes != null)
            {
                foreach (var (k, v) in baseline.MethodHashes)
                {
                    newSnapshot.MethodHashes[k] = v;
                }
            }

            await _snapshotStore.SaveAsync(snapshotPath, newSnapshot, cancellationToken).ConfigureAwait(false);
        }

        var resultComments = (incremental && baseline != null)
            ? _snapshotDiffer.FilterNewComments(allExtractedComments, baseline)
            : allExtractedComments;

        return new AnalysisResult
        {
            VerboseComments = resultComments,
            CloneGroups = cloneGroups,
            SubMethodCloneGroups = subMethodGroups,
            SummarizedComments = cache.ToDictionary(),
            AnalyzedFilesCount = filesToScan.Count,
            TotalMethodsScanned = allAnalyzedMethods.Count,
            ProductionFilesCount = filesToScan.Count(f => fileToNormalizedPath.TryGetValue(f, out var norm) && fileCategories.TryGetValue(norm, out var c) && c == FileCategory.Production),
            TestFilesCount = filesToScan.Count(f => fileToNormalizedPath.TryGetValue(f, out var norm) && fileCategories.TryGetValue(norm, out var c) && c == FileCategory.Test),
            SkippedGeneratedFilesCount = skippedGeneratedCount
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
        if (settings.GetEffectiveSummaryMode() == SummaryMode.Disabled || analysisResult.VerboseComments.Count == 0 || analysisResult.SummarizedComments.Count == 0)
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

        if (_summarizedCommentsStore != null && modifiedFiles.Count > 0)
        {
            var baseDir = _lastBaseDirectory ?? Directory.GetCurrentDirectory();
            var summarizedPath = ResolveSnapshotPath(baseDir, settings.SummarizedCommentsFilePath);
            var existingSummaries = await _summarizedCommentsStore.LoadAsync(summarizedPath, cancellationToken).ConfigureAwait(false);
            var merged = new Dictionary<string, string>(existingSummaries, StringComparer.Ordinal);

            foreach (var group in commentsByFile)
            {
                foreach (var comment in group)
                {
                    if (analysisResult.SummarizedComments.TryGetValue(comment.ContentHash, out var summaryText))
                    {
                        var appliedHash = VerboseComment.ComputeSha256(summaryText.Trim());
                        merged[appliedHash] = summaryText.Trim();
                    }
                }
            }

            await _summarizedCommentsStore.SaveAsync(summarizedPath, merged, cancellationToken).ConfigureAwait(false);
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

        var (baseDirectory, discoveredFiles, _, _) = ResolveFiles(targetPath, settings);
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
            CancellationToken = cancellationToken
        };

        var baselineResults = new (string RelPath, string Hash, List<MethodCloneInfo> Methods, List<SubMethodBlock> SubMethodBlocks)[discoveredFiles.Count];

        await Parallel.ForEachAsync(
            discoveredFiles.Select((f, idx) => (f, idx)),
            parallelOptions,
            async (item, ct) =>
            {
                var file = item.f;
                var relPath = _fileHasher.NormalizeRelativePath(baseDirectory, file);
                var hash = await _fileHasher.ComputeFileHashAsync(file, ct).ConfigureAwait(false);

                List<MethodCloneInfo> methods = [];
                List<SubMethodBlock> subMethodBlocks = [];

                if (settings.EnableDuplicationDetection || settings.EnableSubMethodCloneDetection)
                {
                    var sourceText = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                    var tree = CSharpSyntaxTree.ParseText(sourceText, path: file, cancellationToken: ct);
                    var projectId = _projectLocator.LocateProjectId(file, baseDirectory);

                    if (settings.EnableDuplicationDetection)
                    {
                        methods = _cloneDetector.ExtractMethods(tree, relPath, settings, projectId).ToList();
                    }

                    if (settings.EnableSubMethodCloneDetection)
                    {
                        subMethodBlocks = _cloneDetector.ExtractSubMethodBlocks(tree, relPath, settings, projectId).ToList();
                    }
                }

                baselineResults[item.idx] = (relPath, hash, methods, subMethodBlocks);
            }).ConfigureAwait(false);

        var currentFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var allMethods = new List<MethodCloneInfo>();

        foreach (var (relPath, hash, methods, _) in baselineResults)
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

        if (settings.EnableSubMethodCloneDetection && _subMethodCacheStore != null)
        {
            var cachePath = ResolveSnapshotPath(baseDirectory, settings.SubMethodCacheFilePath);
            var subMethodCache = new SubMethodCacheModel
            {
                Version = SubMethodCacheModel.CurrentVersion,
                NormalizerVersion = AnalysisSnapshot.CurrentNormalizerVersion
            };

            foreach (var (relPath, hash, _, blocks) in baselineResults)
            {
                subMethodCache.Files[relPath] = new FileSubMethodCacheEntry
                {
                    FileHash = hash,
                    NormalizerVersion = AnalysisSnapshot.CurrentNormalizerVersion,
                    Blocks = blocks?.Select(b => new SubMethodBlockCacheDto
                    {
                        MethodName = b.MethodName,
                        BlockScopeId = b.BlockScopeId,
                        ProjectId = b.ProjectId,
                        MethodStartLine = b.MethodStartLine,
                        StatementHashes = b.StatementHashes,
                        StatementStartLines = b.StatementStartLines,
                        StatementEndLines = b.StatementEndLines
                    }).ToList() ?? []
                };
            }

            await _subMethodCacheStore.SaveAsync(cachePath, subMethodCache, cancellationToken).ConfigureAwait(false);
        }

        return newSnapshot;
    }

    private static (string BaseDirectory, List<string> Files, Dictionary<string, FileCategory> Categories, int SkippedGeneratedCount) ResolveFiles(
        string targetPath,
        CodeCondenserSettings settings)
    {
        var pathFilter = new PathFilter(settings, targetPath);
        var baseDirectory = pathFilter.BaseDirectory;
        var fullPath = Path.GetFullPath(targetPath);

        var files = new List<string>();
        var categories = new Dictionary<string, FileCategory>(StringComparer.OrdinalIgnoreCase);
        int skippedGeneratedCount = 0;

        if (File.Exists(fullPath))
        {
            var relPath = pathFilter.NormalizeRelativePath(fullPath);
            if (pathFilter.ShouldAnalyzeFile(relPath, fullPath, out var category, out var isSkippedGenerated))
            {
                files.Add(fullPath);
                categories[relPath] = category;
            }
            else if (isSkippedGenerated)
            {
                skippedGeneratedCount++;
            }
            return (baseDirectory, files, categories, skippedGeneratedCount);
        }

        if (Directory.Exists(fullPath))
        {
            void Traverse(string currentDir)
            {
                if (pathFilter.ShouldPruneDirectory(currentDir, isTargetRoot: string.Equals(currentDir, fullPath, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                try
                {
                    foreach (var file in Directory.EnumerateFiles(currentDir, "*.cs"))
                    {
                        var filePath = Path.GetFullPath(file);
                        var relPath = pathFilter.NormalizeRelativePath(filePath);
                        if (pathFilter.ShouldAnalyzeFile(relPath, filePath, out var cat, out var isSkippedGen))
                        {
                            files.Add(filePath);
                            categories[relPath] = cat;
                        }
                        else if (isSkippedGen)
                        {
                            skippedGeneratedCount++;
                        }
                    }

                    foreach (var subDir in Directory.EnumerateDirectories(currentDir))
                    {
                        Traverse(subDir);
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }

            Traverse(fullPath);
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return (baseDirectory, files, categories, skippedGeneratedCount);
        }

        throw new DirectoryNotFoundException($"Target path '{targetPath}' was not found.");
    }

    private static string ResolveSnapshotPath(string baseDirectory, string snapshotFilePath)
    {
        if (Path.IsPathRooted(snapshotFilePath))
        {
            return snapshotFilePath;
        }

        return Path.Combine(baseDirectory, snapshotFilePath);
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        private readonly object _gate = new();

        public void Report(T value)
        {
            lock (_gate)
            {
                handler(value);
            }
        }
    }
}
