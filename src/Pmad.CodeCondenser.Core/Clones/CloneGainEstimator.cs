using Pmad.AI.Tokens;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Clones;

/// <summary>
/// Estimates the potential token gain of deduplicating code clones, by measuring the source of each occurrence
/// and discounting the one copy that would remain after extraction into a shared helper.
/// </summary>
public static class CloneGainEstimator
{
    /// <summary>
    /// Estimates the tokens saved by keeping a single copy of a method clone group.
    /// Returns 0 when source files cannot be read.
    /// </summary>
    public static int EstimateGainTokens(CodeCloneGroup group, HeuristicTokenEstimator? estimator = null, string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(group);
        return EstimateGain(
            group.Methods.Select(m => (m.FilePath, m.StartLine, m.EndLine)),
            estimator,
            baseDirectory,
            new Dictionary<string, string[]?>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Estimates the tokens saved by keeping a single copy of a sub-method clone group.
    /// Returns 0 when source files cannot be read.
    /// </summary>
    public static int EstimateGainTokens(SubMethodCloneGroup group, HeuristicTokenEstimator? estimator = null, string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(group);
        return EstimateGain(
            group.Occurrences.Select(o => (o.FilePath, o.StartLine, o.EndLine)),
            estimator,
            baseDirectory,
            new Dictionary<string, string[]?>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Estimates the total potential token gain across all method and sub-method clone groups.
    /// </summary>
    public static int EstimateTotalGainTokens(
        IReadOnlyList<CodeCloneGroup> cloneGroups,
        IReadOnlyList<SubMethodCloneGroup> subMethodGroups,
        HeuristicTokenEstimator? estimator = null,
        string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(cloneGroups);
        ArgumentNullException.ThrowIfNull(subMethodGroups);

        var cache = new Dictionary<string, string[]?>(StringComparer.OrdinalIgnoreCase);
        int total = 0;
        foreach (var g in cloneGroups)
        {
            total += EstimateGain(g.Methods.Select(m => (m.FilePath, m.StartLine, m.EndLine)), estimator, baseDirectory, cache);
        }
        foreach (var g in subMethodGroups)
        {
            total += EstimateGain(g.Occurrences.Select(o => (o.FilePath, o.StartLine, o.EndLine)), estimator, baseDirectory, cache);
        }
        return total;
    }

    private static int EstimateGain(
        IEnumerable<(string FilePath, int StartLine, int EndLine)> ranges,
        HeuristicTokenEstimator? estimator,
        string? baseDirectory,
        Dictionary<string, string[]?> cache)
    {
        var effEstimator = estimator ?? HeuristicTokenEstimator.Default;
        var tokens = new List<int>();
        foreach (var (path, start, end) in ranges)
        {
            tokens.Add(MeasureRange(path, start, end, effEstimator, baseDirectory, cache));
        }

        if (tokens.Count < 2)
        {
            return 0;
        }

        // Keep the largest copy; every other occurrence is removable.
        return Math.Max(0, tokens.Sum() - tokens.Max());
    }

    private static int MeasureRange(
        string path,
        int startLine,
        int endLine,
        HeuristicTokenEstimator estimator,
        string? baseDirectory,
        Dictionary<string, string[]?> cache)
    {
        if (!cache.TryGetValue(path, out var lines))
        {
            var resolvedPath = path;
            if (!string.IsNullOrEmpty(baseDirectory) && !Path.IsPathRooted(path) && !File.Exists(path))
            {
                var combined = Path.Combine(baseDirectory, path);
                if (File.Exists(combined))
                {
                    resolvedPath = combined;
                }
            }

            try
            {
                lines = File.Exists(resolvedPath) ? File.ReadAllLines(resolvedPath) : null;
            }
            catch (IOException)
            {
                lines = null;
            }
            catch (UnauthorizedAccessException)
            {
                lines = null;
            }
            cache[path] = lines;
        }

        if (lines == null || startLine < 1 || endLine < startLine || startLine > lines.Length)
        {
            return 0;
        }

        int last = Math.Min(endLine, lines.Length);
        var text = string.Join('\n', lines[(startLine - 1)..last]);
        return estimator.Estimate(text.AsSpan());
    }
}
