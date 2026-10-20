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

        // Modify only File2
        await File.AppendAllTextAsync(file2, "\n// added line");

        // Act: Incremental run
        var incrementalResult = await engine.AnalyzeAsync(_testDir, settings, incremental: true);

        // Assert: Only File2 should be scanned
        incrementalResult.AnalyzedFilesCount.Should().Be(1);
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
            var dict = new Dictionary<string, string>(existingCache);
            foreach (var c in comments)
            {
                if (!dict.ContainsKey(c.ContentHash))
                {
                    dict[c.ContentHash] = "// [Summarized] Concise summary";
                }
            }

            return Task.FromResult<IReadOnlyDictionary<string, string>>(dict);
        }
    }
}
