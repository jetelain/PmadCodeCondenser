using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Benchmark.Models;
using Pmad.CodeCondenser.Core.Comments;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Benchmark.Extraction;

/// <summary>
/// Scans reference C# projects to extract and curate verbose comments for benchmarking.
/// </summary>
public sealed class BenchmarkCommentExtractor
{
    private static readonly HashSet<string> IgnoredDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", ".codecondenser", "node_modules", "testresults", "artifacts"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    /// <summary>
    /// Extracts comments from the given project directories.
    /// </summary>
    public async Task<BenchmarkDataset> ExtractAsync(
        IReadOnlyList<string> projectPaths,
        CodeCondenserSettings? settings = null,
        int? sampleSize = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectPaths);
        settings ??= new CodeCondenserSettings();

        var dataset = new BenchmarkDataset();
        var allExtracted = new List<BenchmarkComment>();
        var seenHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var projectPath in projectPaths)
        {
            var fullProjectPath = Path.GetFullPath(projectPath);
            if (!Directory.Exists(fullProjectPath))
            {
                Console.Error.WriteLine($"[Warning] Project path '{projectPath}' not found. Skipping.");
                continue;
            }

            var projectName = Path.GetFileName(fullProjectPath.TrimEnd('\\', '/'));
            dataset.SourceProjects.Add(projectName);

            var csFiles = DiscoverCSharpFiles(fullProjectPath);
            int projectCommentCount = 0;

            foreach (var file in csFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string sourceText;
                try
                {
                    sourceText = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Warning] Could not read file '{file}': {ex.Message}");
                    continue;
                }

                var relPath = Path.GetRelativePath(fullProjectPath, file);
                var tree = CSharpSyntaxTree.ParseText(sourceText, path: file, cancellationToken: cancellationToken);
                var extractor = new VerboseCommentExtractor();
                var comments = extractor.Extract(tree, relPath, settings);

                foreach (var comment in comments)
                {
                    if (seenHashes.Add(comment.ContentHash))
                    {
                        projectCommentCount++;
                        var id = $"{projectName}_{allExtracted.Count + 1:D4}";
                        var lineCount = comment.EndLine - comment.StartLine + 1;

                        var commentText = comment.OriginalText;
                        if (comment.Kind == CommentKind.XmlDocumentation)
                        {
                            var lines = commentText.Replace("\r\n", "\n").Split('\n');
                            var formattedLines = lines
                                .Select(l => l.Trim())
                                .Where(l => !string.IsNullOrEmpty(l))
                                .Select(l => l.StartsWith("///") ? l : $"/// {l}");
                            commentText = string.Join("\r\n", formattedLines);
                        }

                        allExtracted.Add(new BenchmarkComment(
                            Id: id,
                            Project: projectName,
                            FilePath: relPath,
                            StartLine: comment.StartLine,
                            EndLine: comment.EndLine,
                            Kind: comment.Kind,
                            OriginalText: commentText,
                            ContentHash: comment.ContentHash,
                            CharacterCount: commentText.Length,
                            LineCount: lineCount));
                    }
                }
            }
        }

        List<BenchmarkComment> finalComments;
        if (sampleSize.HasValue && sampleSize.Value > 0 && sampleSize.Value < allExtracted.Count)
        {
            finalComments = StratifiedSample(allExtracted, sampleSize.Value);
        }
        else
        {
            finalComments = allExtracted;
        }

        dataset.Comments = finalComments;

        // Recompute statistics
        foreach (var comment in finalComments)
        {
            dataset.ProjectCounts[comment.Project] = dataset.ProjectCounts.GetValueOrDefault(comment.Project) + 1;
            var kindKey = comment.Kind.ToString();
            dataset.KindCounts[kindKey] = dataset.KindCounts.GetValueOrDefault(kindKey) + 1;
        }

        return dataset;
    }

    /// <summary>
    /// Performs stratified sampling to ensure a representative mix across projects, comment kinds, and length tiers.
    /// </summary>
    public static List<BenchmarkComment> StratifiedSample(IReadOnlyList<BenchmarkComment> comments, int targetCount)
    {
        if (comments.Count <= targetCount)
        {
            return comments.ToList();
        }

        // Group by Project and Kind
        var groups = comments
            .GroupBy(c => (c.Project, c.Kind))
            .ToList();

        var selected = new List<BenchmarkComment>();
        int perGroupBudget = Math.Max(1, targetCount / groups.Count);

        var random = new Random(42); // Deterministic seed for reproducible benchmark sets

        foreach (var group in groups)
        {
            // Sub-group by length tier: Short (<250), Medium (250-600), Long (>=600)
            var sorted = group
                .OrderBy(c => c.CharacterCount)
                .ToList();

            int takeCount = Math.Min(sorted.Count, perGroupBudget);
            if (takeCount >= sorted.Count)
            {
                selected.AddRange(sorted);
            }
            else
            {
                // Sample evenly across the length spectrum
                for (int i = 0; i < takeCount; i++)
                {
                    int index = (int)((double)i / takeCount * sorted.Count);
                    selected.Add(sorted[index]);
                }
            }
        }

        // If we still need more comments to reach targetCount, pick randomly from remaining
        if (selected.Count < targetCount)
        {
            var remaining = comments.Except(selected).ToList();
            var needed = Math.Min(targetCount - selected.Count, remaining.Count);
            selected.AddRange(remaining.OrderBy(_ => random.Next()).Take(needed));
        }
        // If we exceeded targetCount, trim proportionally
        else if (selected.Count > targetCount)
        {
            selected = selected.OrderBy(_ => random.Next()).Take(targetCount).ToList();
        }

        // Re-index IDs cleanly
        return selected
            .OrderBy(c => c.Project)
            .ThenBy(c => c.FilePath)
            .ThenBy(c => c.StartLine)
            .Select((c, idx) => c with { Id = $"sample_{idx + 1:D3}" })
            .ToList();
    }

    /// <summary>
    /// Saves a benchmark dataset to a JSON file.
    /// </summary>
    public async Task SaveDatasetAsync(BenchmarkDataset dataset, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, dataset, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads a benchmark dataset from a JSON file.
    /// </summary>
    public async Task<BenchmarkDataset> LoadDatasetAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Dataset file not found: {filePath}", filePath);
        }

        await using var stream = File.OpenRead(filePath);
        var dataset = await JsonSerializer.DeserializeAsync<BenchmarkDataset>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return dataset ?? throw new InvalidOperationException($"Failed to deserialize dataset from {filePath}");
    }

    private static List<string> DiscoverCSharpFiles(string directory)
    {
        var results = new List<string>();

        void Traverse(string currentDir)
        {
            var dirInfo = new DirectoryInfo(currentDir);
            if (IgnoredDirs.Contains(dirInfo.Name) || (dirInfo.Name.StartsWith('.') && dirInfo.Name.Length > 1))
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
}
