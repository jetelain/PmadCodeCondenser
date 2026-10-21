using FluentAssertions;
using Pmad.CodeCondenser.Core.Comments;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Core.Snapshots;

namespace Pmad.CodeCondenser.Core.Tests.Engine;

public class CodeCondenserEngineTests : IDisposable
{
    private readonly string _testDir;
    private readonly InMemorySnapshotStore _snapshotStore;
    private readonly MockCommentSummarizer _commentSummarizer;

    public CodeCondenserEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "EngineTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _snapshotStore = new InMemorySnapshotStore();
        _commentSummarizer = new MockCommentSummarizer();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsClonesAndVerboseComments()
    {
        // Arrange
        var file1 = Path.Combine(_testDir, "Service1.cs");
        var file2 = Path.Combine(_testDir, "Service2.cs");

        var code1 = """
        namespace MyTest;
        public class Service1
        {
            // This is an extremely long and verbose single line comment that easily exceeds the default threshold of one hundred characters in total length
            public void ProcessItems(int a, int b)
            {
                int x = a + b;
                int y = x * 2;
                int z = y - 5;
                if (z > 10)
                {
                    z = 10;
                }
                int result = z + 1;
                Console.WriteLine(result);
                int finalVal = result * 3;
                Console.WriteLine(finalVal);
            }
        }
        """;

        var code2 = """
        namespace MyTest;
        public class Service2
        {
            public void HandleItems(int p1, int p2)
            {
                int val1 = p1 + p2;
                int val2 = val1 * 2;
                int val3 = val2 - 5;
                if (val3 > 10)
                {
                    val3 = 10;
                }
                int output = val3 + 1;
                Console.WriteLine(output);
                int finish = output * 3;
                Console.WriteLine(finish);
            }
        }
        """;

        await File.WriteAllTextAsync(file1, code1);
        await File.WriteAllTextAsync(file2, code2);

        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            commentSummarizer: _commentSummarizer);

        var settings = new CodeCondenserSettings
        {
            CommentLengthThreshold = 100,
            DuplicationStatementThreshold = 8
        };

        // Act
        var result = await engine.AnalyzeAsync(_testDir, settings);

        // Assert
        result.AnalyzedFilesCount.Should().Be(2);
        result.VerboseComments.Should().HaveCount(1);
        result.CloneGroups.Should().HaveCount(1);
        result.CloneGroups[0].Methods.Should().HaveCount(2);

        // Comment should be summarized
        result.SummarizedComments.Should().ContainKey(result.VerboseComments[0].ContentHash);
    }

    [Fact]
    public async Task ApplySummariesAsync_ModifiesSourceFileOnDisk()
    {
        // Arrange
        var file = Path.Combine(_testDir, "OrderService.cs");
        var initialCode = """
        namespace MyTest;
        public class OrderService
        {
            // This is an extremely long and verbose single line comment that easily exceeds the default threshold of one hundred characters in total length
            public void Execute()
            {
                Console.WriteLine("Done");
            }
        }
        """;

        await File.WriteAllTextAsync(file, initialCode);

        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            commentSummarizer: _commentSummarizer);

        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };

        var analysisResult = await engine.AnalyzeAsync(_testDir, settings);
        analysisResult.VerboseComments.Should().HaveCount(1);

        // Act
        var appliedFiles = await engine.ApplySummariesWithModifiedFilesAsync(analysisResult, settings);

        // Assert
        appliedFiles.Should().ContainSingle().Which.Should().Be(file);
        var updatedText = await File.ReadAllTextAsync(file);
        updatedText.Should().NotContain("This is an extremely long and verbose single line comment");
        updatedText.Should().Contain("// [Summarized]");
    }

    [Fact]
    public async Task AnalyzeAsync_IncrementalMode_SkipsUnchangedFiles()
    {
        // Arrange
        var file1 = Path.Combine(_testDir, "File1.cs");
        var file2 = Path.Combine(_testDir, "File2.cs");

        var code1 = """
        namespace MyTest;
        public class File1
        {
            // This is an extremely long and verbose single line comment that easily exceeds the default threshold of one hundred characters in total length
            public void DoSomething()
            {
            }
        }
        """;

        var code2 = """
        namespace MyTest;
        public class File2
        {
            public void Other()
            {
            }
        }
        """;

        await File.WriteAllTextAsync(file1, code1);
        await File.WriteAllTextAsync(file2, code2);

        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            commentSummarizer: _commentSummarizer);

        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };

        // Baseline analysis (populates snapshot in store)
        var baselineResult = await engine.AnalyzeAsync(_testDir, settings, incremental: false);
        baselineResult.AnalyzedFilesCount.Should().Be(2);
        baselineResult.ProductionFilesCount.Should().Be(2);
        baselineResult.TestFilesCount.Should().Be(0);

        // Modify only File2
        await File.AppendAllTextAsync(file2, "\n// added line");

        // Act: Incremental run
        var incrementalResult = await engine.AnalyzeAsync(_testDir, settings, incremental: true);

        // Assert: Only File2 should be scanned
        incrementalResult.AnalyzedFilesCount.Should().Be(1);
        incrementalResult.ProductionFilesCount.Should().Be(1);
        incrementalResult.TestFilesCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateSnapshotBaselineAsync_CreatesBaselineInSnapshotStore()
    {
        // Arrange
        var file = Path.Combine(_testDir, "BaselineFile.cs");
        var code = """
        namespace MyTest;
        public class BaselineFile
        {
            public void TestMethod(int a, int b)
            {
                int c = a + b;
                int d = c * 2;
                int e = d - 1;
                if (e > 5) e = 5;
                int f = e + 2;
                Console.WriteLine(f);
                int g = f * 3;
                Console.WriteLine(g);
            }
        }
        """;
        await File.WriteAllTextAsync(file, code);

        var engine = new CodeCondenserEngine(snapshotStore: _snapshotStore);
        var settings = new CodeCondenserSettings();

        // Act
        var snapshot = await engine.UpdateSnapshotBaselineAsync(_testDir, settings);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot!.Files.Should().ContainKey("BaselineFile.cs");
        snapshot.MethodHashes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UpdateSnapshotBaselineAsync_UpgradesNormalizerVersion_WhenBaselineHasOlderVersion()
    {
        var file = Path.Combine(_testDir, "BaselineFile.cs");
        var code = """
        namespace MyTest;
        public class BaselineFile
        {
            public void TestMethod(int a, int b)
            {
                int c = a + b;
                int d = c * 2;
                int e = d - 1;
                if (e > 5) e = 5;
                int f = e + 2;
                Console.WriteLine(f);
                int g = f * 3;
                Console.WriteLine(g);
            }
        }
        """;
        await File.WriteAllTextAsync(file, code);

        var baselineSnapshot = new AnalysisSnapshot
        {
            Version = 1,
            NormalizerVersion = 1, // older normalizer version
            CreatedAt = DateTimeOffset.UtcNow
        };
        var snapshotPath = Path.Combine(_testDir, ".codecondenser", "snapshot.json");
        await _snapshotStore.SaveAsync(snapshotPath, baselineSnapshot);

        var engine = new CodeCondenserEngine(snapshotStore: _snapshotStore);
        var settings = new CodeCondenserSettings { EnableDuplicationDetection = true };

        var newSnapshot = await engine.UpdateSnapshotBaselineAsync(_testDir, settings);

        newSnapshot.Should().NotBeNull();
        newSnapshot!.NormalizerVersion.Should().Be(AnalysisSnapshot.CurrentNormalizerVersion);
    }

    [Fact]
    public async Task AppliedXmlDocumentationComment_IsRecognizedAsIgnoredOnSubsequentAnalysis_EvenWithIndentation()
    {
        var file = Path.Combine(_testDir, "XmlDocService.cs");
        var originalCode = """
        namespace MyTest;
        public class XmlDocService
        {
            /// <summary>
            /// This is a verbose XML documentation comment that describes the Calculate method in excessive detail to exceed threshold.
            /// </summary>
            public void Calculate()
            {
            }
        }
        """;
        await File.WriteAllTextAsync(file, originalCode);

        var summarizer = new MockCommentSummarizer();
        var summarizedStore = new InMemorySummarizedCommentsStore();
        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            commentSummarizer: summarizer,
            summarizedCommentsStore: summarizedStore);

        var settings = new CodeCondenserSettings
        {
            CommentLengthThreshold = 50,
            XmlSummaryLengthThreshold = 40,
            EnableCommentSummarization = true,
            SummaryMode = SummaryMode.Summarize
        };

        // 1. Initial analysis produces verbose comment
        var initialResult = await engine.AnalyzeAsync(_testDir, settings);
        initialResult.VerboseComments.Should().HaveCount(1);

        // 2. Apply summaries to source file
        var modified = await engine.ApplySummariesWithModifiedFilesAsync(initialResult, settings);
        modified.Should().Contain(file);

        // 3. Subsequent analysis on modified file: applied XML comment should be recognized as ignored and NOT flagged again!
        var secondResult = await engine.AnalyzeAsync(_testDir, settings, incremental: false);
        secondResult.VerboseComments.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenCommentSummarizationDisabled_SkipsComments()
    {
        var file = Path.Combine(_testDir, "CommentSkip.cs");
        await File.WriteAllTextAsync(file, """
        public class CommentSkip
        {
            // This is an extremely long and verbose single line comment that easily exceeds the default threshold of one hundred characters in total length
            public void Run() {}
        }
        """);

        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            commentSummarizer: _commentSummarizer);

        var settings = new CodeCondenserSettings
        {
            EnableCommentSummarization = false
        };

        var result = await engine.AnalyzeAsync(_testDir, settings);

        result.VerboseComments.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenDuplicationDetectionDisabled_SkipsClones()
    {
        var file1 = Path.Combine(_testDir, "CloneSkip1.cs");
        var file2 = Path.Combine(_testDir, "CloneSkip2.cs");

        var code = """
        public class CloneSkip
        {
            public void M()
            {
                int a = 1; int b = 2; int c = a + b; int d = c * 2;
                int e = d - 1; if (e > 5) e = 5; int f = e + 2;
                Console.WriteLine(f); int g = f * 3; Console.WriteLine(g);
            }
        }
        """;
        await File.WriteAllTextAsync(file1, code);
        await File.WriteAllTextAsync(file2, code);

        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            commentSummarizer: _commentSummarizer);

        var settings = new CodeCondenserSettings
        {
            EnableDuplicationDetection = false
        };

        var result = await engine.AnalyzeAsync(_testDir, settings);

        result.CloneGroups.Should().BeEmpty();
        result.TotalMethodsScanned.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsCommentSummarizationProgress()
    {
        var file = Path.Combine(_testDir, "ProgressTest.cs");
        await File.WriteAllTextAsync(file, """
        public class ProgressTest
        {
            // This is an extremely long and verbose single line comment that easily exceeds the default threshold of one hundred characters in total length
            public void Run() {}
        }
        """);

        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            commentSummarizer: _commentSummarizer);

        var progressList = new List<CommentSummarizationProgress>();
        engine.CommentSummarizationProgress += p => progressList.Add(p);

        var settings = new CodeCondenserSettings();
        await engine.AnalyzeAsync(_testDir, settings);

        progressList.Should().NotBeEmpty();
        progressList.Last().Completed.Should().Be(1);
    }

    [Fact]
    public async Task ApplySummariesAsync_PersistsSummariesToSummarizedCommentsStore()
    {
        var file = Path.Combine(_testDir, "ApplyTest.cs");
        var code = """
        namespace MyTest;
        public class ApplyTest
        {
            // This is an extremely long and verbose single line comment that easily exceeds the default threshold of one hundred characters in total length
            public void TestMethod() {}
        }
        """;
        await File.WriteAllTextAsync(file, code);

        var summarizedStore = new InMemorySummarizedCommentsStore();
        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            commentSummarizer: _commentSummarizer,
            summarizedCommentsStore: summarizedStore);

        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };
        var analysis = await engine.AnalyzeAsync(_testDir, settings);
        analysis.VerboseComments.Should().HaveCount(1);

        var applied = await engine.ApplySummariesWithModifiedFilesAsync(analysis, settings);
        applied.Should().HaveCount(1);

        summarizedStore.Stored.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenCommentInSummarizedCommentsStore_SkipsItEvenIfExceedingThreshold()
    {
        var file = Path.Combine(_testDir, "AlreadySummarizedTest.cs");
        var comment = "// This is an already summarized comment that happens to still exceed fifty characters in total length";
        var code = $$"""
        namespace MyTest;
        public class AlreadySummarizedTest
        {
            {{comment}}
            public void TestMethod() {}
        }
        """;
        await File.WriteAllTextAsync(file, code);

        var hash = VerboseComment.ComputeSha256(comment.Trim());
        var summarizedStore = new InMemorySummarizedCommentsStore();
        summarizedStore.Stored[hash] = comment.Trim();

        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            commentSummarizer: _commentSummarizer,
            summarizedCommentsStore: summarizedStore);

        var settings = new CodeCondenserSettings { CommentLengthThreshold = 50 };
        var analysis = await engine.AnalyzeAsync(_testDir, settings);

        analysis.VerboseComments.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotMutateCallerSettingsIgnoredCommentHashes()
    {
        var file = Path.Combine(_testDir, "SettingsMutationTest.cs");
        var comment = "// This is an already summarized comment that happens to still exceed fifty characters in total length";
        var code = $$"""
        namespace MyTest;
        public class SettingsMutationTest
        {
            {{comment}}
            public void TestMethod() {}
        }
        """;
        await File.WriteAllTextAsync(file, code);

        var hash = VerboseComment.ComputeSha256(comment.Trim());
        var summarizedStore = new InMemorySummarizedCommentsStore();
        summarizedStore.Stored[hash] = comment.Trim();

        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            commentSummarizer: _commentSummarizer,
            summarizedCommentsStore: summarizedStore);

        var settings = new CodeCondenserSettings { CommentLengthThreshold = 50 };
        settings.IgnoredCommentHashes.Should().BeEmpty();

        await engine.AnalyzeAsync(_testDir, settings);

        // Caller's settings.IgnoredCommentHashes must remain empty and not be mutated by AnalyzeAsync
        settings.IgnoredCommentHashes.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateSnapshotBaselineAsync_DoesNotSaveUnappliedCandidateCommentsToSummarizedStore()
    {
        var file = Path.Combine(_testDir, "BaselineCandidateTest.cs");
        var code = """
        namespace MyTest;
        public class BaselineCandidateTest
        {
            public void TestMethod() {}
        }
        """;
        await File.WriteAllTextAsync(file, code);

        // Preload snapshot with a candidate summary in CommentCache
        var existingSnapshot = new AnalysisSnapshot
        {
            Version = 1,
            CommentCache = new Dictionary<string, string>
            {
                ["candidate-hash"] = "// [Summarized] This candidate has NOT been applied to source files yet"
            }
        };
        await _snapshotStore.SaveAsync("snapshot.json", existingSnapshot);

        var summarizedStore = new InMemorySummarizedCommentsStore();
        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            commentSummarizer: _commentSummarizer,
            summarizedCommentsStore: summarizedStore);

        var settings = new CodeCondenserSettings();
        await engine.UpdateSnapshotBaselineAsync(_testDir, settings);

        // Unapplied candidate summaries in snapshot CommentCache must NOT be exported to summarized-comments.json
        summarizedStore.Stored.Should().BeEmpty();
    }

    private sealed class InMemorySummarizedCommentsStore : ISummarizedCommentsStore
    {
        public Dictionary<string, string> Stored { get; } = new(StringComparer.Ordinal);

        public Task<IReadOnlyDictionary<string, string>> LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(Stored));
        }

        public Task SaveAsync(string filePath, IReadOnlyDictionary<string, string> summarizedComments, CancellationToken cancellationToken = default)
        {
            Stored.Clear();
            foreach (var (k, v) in summarizedComments)
            {
                Stored[k] = v;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySnapshotStore : ISnapshotStore
    {
        private AnalysisSnapshot? _stored;

        public Task<AnalysisSnapshot?> LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_stored);
        }

        public Task SaveAsync(string filePath, AnalysisSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            _stored = snapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class MockCommentSummarizer : ICommentSummarizer
    {
        public Task<IReadOnlyDictionary<string, string>> SummarizeAsync(
            IReadOnlyList<VerboseComment> comments,
            IReadOnlyDictionary<string, string> existingCache,
            CodeCondenserSettings settings,
            CancellationToken cancellationToken = default)
        {
            return SummarizeAsync(comments, existingCache, settings, progress: null, cancellationToken);
        }

        public Task<IReadOnlyDictionary<string, string>> SummarizeAsync(
            IReadOnlyList<VerboseComment> comments,
            IReadOnlyDictionary<string, string> existingCache,
            CodeCondenserSettings settings,
            IProgress<CommentSummarizationProgress>? progress,
            CancellationToken cancellationToken = default)
        {
            var dict = new Dictionary<string, string>(existingCache);
            int count = 0;
            foreach (var c in comments)
            {
                if (!dict.ContainsKey(c.ContentHash))
                {
                    dict[c.ContentHash] = c.Kind == CommentKind.XmlDocumentation
                        ? "/// <summary>\n/// Concise summary of calculate method exceeding forty characters.\n/// </summary>"
                        : "// [Summarized] Concise summary";
                    count++;
                    progress?.Report(new CommentSummarizationProgress(count, comments.Count, c));
                }
            }

            return Task.FromResult<IReadOnlyDictionary<string, string>>(dict);
        }
    }
}
