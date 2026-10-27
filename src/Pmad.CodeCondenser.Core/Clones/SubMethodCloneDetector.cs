using System.Buffers;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Filtering;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Clones;

/// <summary>
/// Detects duplicate statement blocks across methods using sliding-window AST sequence analysis.
/// </summary>
public sealed class SubMethodCloneDetector : ISubMethodCloneDetector
{
    private readonly IAstNormalizer _normalizer;
    private readonly IProjectLocator _projectLocator;

    public SubMethodCloneDetector(
        IAstNormalizer? normalizer = null,
        IProjectLocator? projectLocator = null)
    {
        _normalizer = normalizer ?? new AstNormalizer();
        _projectLocator = projectLocator ?? new ProjectLocator();
    }

    /// <inheritdoc />
    public IReadOnlyList<SubMethodBlock> ExtractBlocks(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings,
        string? projectId = null)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        ArgumentNullException.ThrowIfNull(settings);

        projectId ??= _projectLocator.LocateProjectId(filePath);
        int threshold = settings.SubMethodStatementThreshold;

        var root = syntaxTree.GetRoot();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        var result = new List<SubMethodBlock>();

        foreach (var method in methods)
        {
            if (DuplicationIgnoreMarker.IsIgnored(method) ||
                GeneratedCodeDetector.HasGeneratedCodeAttribute(method) ||
                method.Body == null)
            {
                continue;
            }

            var blocks = CollectDiscreteBlocks(method.Body);

            for (int blockIdx = 0; blockIdx < blocks.Count; blockIdx++)
            {
                var block = blocks[blockIdx];
                var filtered = new List<StatementSyntax>();

                foreach (var stmt in block.Statements)
                {
                    if (IsValidStatement(stmt))
                    {
                        filtered.Add(stmt);
                    }
                }

                if (filtered.Count < threshold)
                {
                    continue;
                }

                var hashes = new ulong[filtered.Count];
                var startLines = new int[filtered.Count];
                var endLines = new int[filtered.Count];

                for (int i = 0; i < filtered.Count; i++)
                {
                    var stmt = filtered[i];
                    var normalized = _normalizer.NormalizeStatement(stmt);
                    var normalizedStr = normalized.NormalizeWhitespace("", "").ToFullString();

                    hashes[i] = ComputeStatementHash(normalizedStr);

                    var lineSpan = syntaxTree.GetLineSpan(stmt.Span);
                    startLines[i] = lineSpan.StartLinePosition.Line + 1;
                    endLines[i] = lineSpan.EndLinePosition.Line + 1;
                }

                var methodStartLine = syntaxTree.GetLineSpan(method.Span).StartLinePosition.Line + 1;
                var blockScopeId = block == method.Body
                    ? $"{method.Identifier.ValueText}:{methodStartLine}#body"
                    : $"{method.Identifier.ValueText}:{methodStartLine}#block:{syntaxTree.GetLineSpan(block.Span).StartLinePosition.Line + 1}";

                result.Add(new SubMethodBlock
                {
                    FilePath = filePath,
                    MethodName = method.Identifier.ValueText,
                    BlockScopeId = blockScopeId,
                    ProjectId = projectId,
                    MethodStartLine = methodStartLine,
                    StatementHashes = hashes,
                    StatementStartLines = startLines,
                    StatementEndLines = endLines
                });
            }
        }

        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<SubMethodCloneGroup> DetectClones(
        IEnumerable<SubMethodBlock> blocks,
        int threshold,
        IReadOnlyList<CodeCloneGroup>? wholeMethodClones = null,
        Func<string, FileCategory>? fileClassifier = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        if (threshold <= 0)
        {
            return [];
        }

        var blockList = blocks.Where(b => b.StatementCount >= threshold).ToList();
        if (blockList.Count < 2)
        {
            return [];
        }

        // 1. Build lookup of whole-method clone pairs to subsume
        static string NormalizePath(string p) => p.Replace('\\', '/').ToLowerInvariant();

        var wholeMethodClonePairs = new HashSet<(string Path1, string Method1, string Path2, string Method2)>();
        var wholeMethodCloneLinePairs = new HashSet<(string Path1, string Method1, int Line1, string Path2, string Method2, int Line2)>();
        if (wholeMethodClones != null)
        {
            foreach (var group in wholeMethodClones)
            {
                for (int i = 0; i < group.Methods.Count; i++)
                {
                    for (int j = i + 1; j < group.Methods.Count; j++)
                    {
                        var m1 = group.Methods[i];
                        var m2 = group.Methods[j];
                        var p1 = NormalizePath(m1.FilePath);
                        var p2 = NormalizePath(m2.FilePath);

                        wholeMethodClonePairs.Add((p1, m1.MethodName, p2, m2.MethodName));
                        wholeMethodClonePairs.Add((p2, m2.MethodName, p1, m1.MethodName));

                        if (m1.StartLine > 0 && m2.StartLine > 0)
                        {
                            wholeMethodCloneLinePairs.Add((p1, m1.MethodName, m1.StartLine, p2, m2.MethodName, m2.StartLine));
                            wholeMethodCloneLinePairs.Add((p2, m2.MethodName, m2.StartLine, p1, m1.MethodName, m1.StartLine));
                        }
                    }
                }
            }
        }

        // 2. Index sliding windows by sequence hash
        var windowsByHash = new Dictionary<ulong, List<(SubMethodBlock Block, int StartIndex)>>();

        for (int bIdx = 0; bIdx < blockList.Count; bIdx++)
        {
            var b = blockList[bIdx];
            for (int i = 0; i <= b.StatementCount - threshold; i++)
            {
                ulong seqHash = ComputeWindowSequenceHash(b.StatementHashes.AsSpan(i, threshold));

                if (!windowsByHash.TryGetValue(seqHash, out var list))
                {
                    list = new List<(SubMethodBlock Block, int StartIndex)>();
                    windowsByHash[seqHash] = list;
                }
                list.Add((b, i));
            }
        }

        // 3. Process candidate buckets, expand contiguous matches, and group occurrences
        var processedIntervals = new HashSet<(string, string, int, string, string, int, int)>();
        var rawClusters = new Dictionary<string, (int StatementCount, HashSet<SubMethodCloneOccurrence> Occurrences)>(StringComparer.Ordinal);

        foreach (var bucket in windowsByHash.Values)
        {
            if (bucket.Count < 2)
            {
                continue;
            }

            for (int i = 0; i < bucket.Count; i++)
            {
                var w1 = bucket[i];

                for (int j = i + 1; j < bucket.Count; j++)
                {
                    var w2 = bucket[j];

                    var normP1 = NormalizePath(w1.Block.FilePath);
                    var normP2 = NormalizePath(w2.Block.FilePath);

                    // Self-match check: intra-method matches within the exact same method declaration are suppressed
                    bool isSameMethod = string.Equals(normP1, normP2, StringComparison.OrdinalIgnoreCase) &&
                                        (w1.Block.MethodStartLine > 0 && w2.Block.MethodStartLine > 0
                                            ? w1.Block.MethodStartLine == w2.Block.MethodStartLine
                                            : string.Equals(w1.Block.BlockScopeId, w2.Block.BlockScopeId, StringComparison.Ordinal));

                    if (isSameMethod)
                    {
                        continue;
                    }

                    // Whole-method subsumption: suppress sub-method clone if methods already match whole-method
                    bool isWholeMethodSubsumed = false;
                    if (w1.Block.MethodStartLine > 0 && w2.Block.MethodStartLine > 0 && wholeMethodCloneLinePairs.Count > 0)
                    {
                        isWholeMethodSubsumed = wholeMethodCloneLinePairs.Contains((normP1, w1.Block.MethodName, w1.Block.MethodStartLine, normP2, w2.Block.MethodName, w2.Block.MethodStartLine));
                    }
                    if (!isWholeMethodSubsumed)
                    {
                        isWholeMethodSubsumed = wholeMethodClonePairs.Contains((normP1, w1.Block.MethodName, normP2, w2.Block.MethodName));
                    }

                    if (isWholeMethodSubsumed)
                    {
                        continue;
                    }

                    // Collision-free verification of initial window
                    bool matches = true;
                    for (int k = 0; k < threshold; k++)
                    {
                        if (w1.Block.StatementHashes[w1.StartIndex + k] != w2.Block.StatementHashes[w2.StartIndex + k])
                        {
                            matches = false;
                            break;
                        }
                    }

                    if (!matches)
                    {
                        continue; // Hash collision
                    }

                    // Optimization: if preceding statements also match, this window is a continuation
                    // of an earlier match and was already expanded starting at that earlier index.
                    if (w1.StartIndex > 0 && w2.StartIndex > 0 &&
                        w1.Block.StatementHashes[w1.StartIndex - 1] == w2.Block.StatementHashes[w2.StartIndex - 1])
                    {
                        continue;
                    }

                    // Greedy maximal expansion
                    int i1 = w1.StartIndex;
                    int i2 = w2.StartIndex;

                    // Expand backwards
                    while (i1 > 0 && i2 > 0 && w1.Block.StatementHashes[i1 - 1] == w2.Block.StatementHashes[i2 - 1])
                    {
                        i1--;
                        i2--;
                    }

                    // Expand forwards (start after known matching statements)
                    int length = (w1.StartIndex - i1) + threshold;
                    while (i1 + length < w1.Block.StatementCount &&
                           i2 + length < w2.Block.StatementCount &&
                           w1.Block.StatementHashes[i1 + length] == w2.Block.StatementHashes[i2 + length])
                    {
                        length++;
                    }

                    // Canonical pair ordering to avoid duplicate interval tracking and eliminate string allocations
                    var isFirst = string.Compare(normP1, normP2, StringComparison.Ordinal) < 0 ||
                                  (string.Equals(normP1, normP2, StringComparison.Ordinal) && string.Compare(w1.Block.BlockScopeId, w2.Block.BlockScopeId, StringComparison.Ordinal) < 0) ||
                                  (string.Equals(normP1, normP2, StringComparison.Ordinal) && string.Equals(w1.Block.BlockScopeId, w2.Block.BlockScopeId, StringComparison.Ordinal) && i1 <= i2);

                    var intervalKey = isFirst
                        ? (normP1, w1.Block.BlockScopeId, i1, normP2, w2.Block.BlockScopeId, i2, length)
                        : (normP2, w2.Block.BlockScopeId, i2, normP1, w1.Block.BlockScopeId, i1, length);

                    if (!processedIntervals.Add(intervalKey))
                    {
                        continue;
                    }

                    // Compute sequence hash for the expanded interval
                    string sequenceHash = ComputeExpandedSequenceHash(w1.Block.StatementHashes.AsSpan(i1, length));

                    if (!rawClusters.TryGetValue(sequenceHash, out var clusterData))
                    {
                        clusterData = (length, new HashSet<SubMethodCloneOccurrence>());
                        rawClusters[sequenceHash] = clusterData;
                    }

                    var occ1 = new SubMethodCloneOccurrence(
                        FilePath: w1.Block.FilePath,
                        MethodName: w1.Block.MethodName,
                        StartLine: w1.Block.StatementStartLines[i1],
                        EndLine: w1.Block.StatementEndLines[i1 + length - 1],
                        StatementCount: length,
                        StatementIndex: i1,
                        BlockScopeId: w1.Block.BlockScopeId,
                        ProjectId: w1.Block.ProjectId);

                    var occ2 = new SubMethodCloneOccurrence(
                        FilePath: w2.Block.FilePath,
                        MethodName: w2.Block.MethodName,
                        StartLine: w2.Block.StatementStartLines[i2],
                        EndLine: w2.Block.StatementEndLines[i2 + length - 1],
                        StatementCount: length,
                        StatementIndex: i2,
                        BlockScopeId: w2.Block.BlockScopeId,
                        ProjectId: w2.Block.ProjectId);

                    clusterData.Occurrences.Add(occ1);
                    clusterData.Occurrences.Add(occ2);
                }
            }
        }

        // 4. Build preliminary candidate clusters with >= 2 distinct methods
        var candidateClusters = new List<(string Hash, int StatementCount, List<SubMethodCloneOccurrence> Occurrences)>();

        foreach (var (hash, data) in rawClusters)
        {
            var distinctMethods = data.Occurrences
                .Select(o => (Path: NormalizePath(o.FilePath), o.MethodName, o.BlockScopeId))
                .Distinct()
                .ToList();

            if (distinctMethods.Count >= 2)
            {
                candidateClusters.Add((hash, data.StatementCount, data.Occurrences.OrderBy(o => o.FilePath).ThenBy(o => o.StartLine).ToList()));
            }
        }

        // 5. Remove clusters that are completely subsumed by longer clusters between the same methods
        var nonSubsumed = new List<(string Hash, int StatementCount, List<SubMethodCloneOccurrence> Occurrences)>();

        for (int i = 0; i < candidateClusters.Count; i++)
        {
            var c1 = candidateClusters[i];
            bool isSubsumed = false;

            for (int j = 0; j < candidateClusters.Count; j++)
            {
                if (i == j) continue;
                var c2 = candidateClusters[j];

                if (c2.StatementCount > c1.StatementCount)
                {
                    // Check if all occurrences in c1 are within occurrences in c2
                    bool allContained = true;
                    foreach (var o1 in c1.Occurrences)
                    {
                        bool containedInAny = c2.Occurrences.Any(o2 =>
                            string.Equals(o1.FilePath, o2.FilePath, StringComparison.OrdinalIgnoreCase) &&
                            (o1.BlockScopeId != null && o2.BlockScopeId != null
                                ? string.Equals(o1.BlockScopeId, o2.BlockScopeId, StringComparison.Ordinal)
                                : string.Equals(o1.MethodName, o2.MethodName, StringComparison.Ordinal)) &&
                            o1.StartLine >= o2.StartLine &&
                            o1.EndLine <= o2.EndLine);

                        if (!containedInAny)
                        {
                            allContained = false;
                            break;
                        }
                    }

                    if (allContained)
                    {
                        isSubsumed = true;
                        break;
                    }
                }
            }

            if (!isSubsumed)
            {
                nonSubsumed.Add(c1);
            }
        }

        // 6. Map to SubMethodCloneGroup, calculate impact, and sort
        var results = new List<SubMethodCloneGroup>();

        foreach (var (hash, statementCount, occurrences) in nonSubsumed)
        {
            int impact = statementCount * Math.Max(0, occurrences.Count - 1);
            var category = DetermineCategory(occurrences, fileClassifier);
            var rec = $"Extract duplicate {statementCount}-statement block into a shared helper method.";

            results.Add(new SubMethodCloneGroup(
                NormalizedHash: hash,
                Occurrences: occurrences,
                StatementCount: statementCount,
                Category: category,
                ImpactScore: impact,
                Recommendation: rec));
        }

        return results
            .OrderByDescending(g => g.ImpactScore)
            .ThenByDescending(g => g.StatementCount)
            .ThenBy(g => g.NormalizedHash, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<SubMethodCloneGroup> DetectClones(
        IEnumerable<(SyntaxTree Tree, string FilePath)> syntaxTrees,
        CodeCondenserSettings settings,
        IReadOnlyList<CodeCloneGroup>? wholeMethodClones = null,
        Func<string, FileCategory>? fileClassifier = null)
    {
        ArgumentNullException.ThrowIfNull(syntaxTrees);
        ArgumentNullException.ThrowIfNull(settings);

        var allBlocks = new List<SubMethodBlock>();
        foreach (var (tree, path) in syntaxTrees)
        {
            allBlocks.AddRange(ExtractBlocks(tree, path, settings));
        }

        return DetectClones(allBlocks, settings.SubMethodStatementThreshold, wholeMethodClones, fileClassifier);
    }

    private static List<BlockSyntax> CollectDiscreteBlocks(BlockSyntax methodBody)
    {
        var blocks = new List<BlockSyntax>();

        void Traverse(SyntaxNode node)
        {
            // Do not descend into local functions or anonymous lambdas
            if (node is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)
            {
                return;
            }

            if (node is BlockSyntax b)
            {
                blocks.Add(b);
            }

            foreach (var child in node.ChildNodes())
            {
                Traverse(child);
            }
        }

        Traverse(methodBody);
        return blocks;
    }

    private static bool IsValidStatement(StatementSyntax statement)
    {
        if (statement is EmptyStatementSyntax)
        {
            return false;
        }

        if (statement is LocalDeclarationStatementSyntax localDecl)
        {
            if (localDecl.Declaration.Variables.All(v => v.Initializer == null))
            {
                return false;
            }
        }

        return true;
    }

    private static ulong ComputeStatementHash(string normalizedString)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(normalizedString.Length);
        byte[]? rented = null;
        Span<byte> utf8Bytes = maxByteCount <= 1024
            ? stackalloc byte[maxByteCount]
            : (rented = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            int written = Encoding.UTF8.GetBytes(normalizedString, utf8Bytes);
            return XxHash64.HashToUInt64(utf8Bytes[..written]);
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private static ulong ComputeWindowSequenceHash(ReadOnlySpan<ulong> windowSpan)
    {
        return XxHash64.HashToUInt64(MemoryMarshal.AsBytes(windowSpan));
    }

    private static string ComputeExpandedSequenceHash(ReadOnlySpan<ulong> statementHashes)
    {
        Span<byte> sha = stackalloc byte[32];
        SHA256.HashData(MemoryMarshal.AsBytes(statementHashes), sha);
        return Convert.ToHexStringLower(sha);
    }

    private static CloneCategory DetermineCategory(
        List<SubMethodCloneOccurrence> occurrences,
        Func<string, FileCategory>? fileClassifier)
    {
        if (fileClassifier == null || occurrences.Count == 0)
        {
            return CloneCategory.Production;
        }

        bool hasProduction = false;
        bool hasTest = false;

        foreach (var occ in occurrences)
        {
            var cat = fileClassifier(occ.FilePath);
            if (cat == FileCategory.Test)
            {
                hasTest = true;
            }
            else
            {
                hasProduction = true;
            }
        }

        if (hasProduction && hasTest)
        {
            return CloneCategory.CrossBoundary;
        }

        if (hasTest)
        {
            return CloneCategory.Test;
        }

        return CloneCategory.Production;
    }
}
