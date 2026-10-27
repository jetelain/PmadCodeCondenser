using AwesomeAssertions;
using Pmad.CodeCondenser.Core.Snapshots;
using Pmad.CodeCondenser.Infrastructure.Storage;

namespace Pmad.CodeCondenser.Infrastructure.Tests.Storage;

public class JsonSnapshotStoreTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _snapshotPath;
    private readonly JsonSnapshotStore _store;

    public JsonSnapshotStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "CodeCondenserTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _snapshotPath = Path.Combine(_tempDirectory, "snapshot.json");
        _store = new JsonSnapshotStore();
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsNull()
    {
        var nonExistentPath = Path.Combine(_tempDirectory, "does_not_exist.json");
        var result = await _store.LoadAsync(nonExistentPath);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_WhenFileIsCorrupted_ReturnsNull()
    {
        var corruptPath = Path.Combine(_tempDirectory, "corrupt.json");
        await File.WriteAllTextAsync(corruptPath, "{ invalid json content");

        var result = await _store.LoadAsync(corruptPath);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoadAsync_WhenPathIsInvalid_ThrowsArgumentException(string? invalidPath)
    {
        var act = async () => await _store.LoadAsync(invalidPath!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveAsync_WhenSnapshotIsNull_ThrowsArgumentNullException()
    {
        var act = async () => await _store.SaveAsync(_snapshotPath, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAsync_WhenPathIsInvalid_ThrowsArgumentException(string? invalidPath)
    {
        var snapshot = new AnalysisSnapshot();
        var act = async () => await _store.SaveAsync(invalidPath!, snapshot);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveAndLoadAsync_PreservesAllSnapshotData()
    {
        var snapshot = new AnalysisSnapshot
        {
            Version = 1,
            Files =
            {
                ["src/Foo.cs"] = new FileSnapshotInfo { FileHash = "hash123", LastAnalyzed = DateTimeOffset.UtcNow }
            },
            MethodHashes =
            {
                ["src/Foo.cs::Bar()"] = "methodHashABC"
            },
            CommentCache =
            {
                ["commentHashXYZ"] = "Short summary"
            }
        };

        await _store.SaveAsync(_snapshotPath, snapshot);

        File.Exists(_snapshotPath).Should().BeTrue();

        var loaded = await _store.LoadAsync(_snapshotPath);
        loaded.Should().NotBeNull();
        loaded!.Version.Should().Be(1);
        loaded.Files.Should().ContainKey("src/Foo.cs");
        loaded.Files["src/Foo.cs"].FileHash.Should().Be("hash123");
        loaded.MethodHashes["src/Foo.cs::Bar()"].Should().Be("methodHashABC");
        loaded.CommentCache["commentHashXYZ"].Should().Be("Short summary");
    }

    [Fact]
    public async Task SaveAsync_ProducesCamelCaseJsonMatchingSpecification()
    {
        var snapshot = new AnalysisSnapshot
        {
            Version = 1,
            Files =
            {
                ["src/Services/OrderService.cs"] = new FileSnapshotInfo { FileHash = "a1b2c3d4", LastAnalyzed = DateTimeOffset.UtcNow }
            },
            MethodHashes =
            {
                ["src/Services/OrderService.cs::ProcessOrder"] = "e5f6g7h8"
            },
            CommentCache =
            {
                ["sha256_of_original_comment"] = "Concise summarized comment text"
            }
        };

        await _store.SaveAsync(_snapshotPath, snapshot);

        var json = await File.ReadAllTextAsync(_snapshotPath);
        json.Should().Contain("\"version\": 1");
        json.Should().Contain("\"createdAt\":");
        json.Should().Contain("\"files\":");
        json.Should().Contain("\"fileHash\": \"a1b2c3d4\"");
        json.Should().Contain("\"lastAnalyzed\":");
        json.Should().Contain("\"methodHashes\":");
        json.Should().Contain("\"commentCache\":");
    }

    [Fact]
    public async Task SaveAsync_WhenDirectoryDoesNotExist_CreatesDirectoryAutomatically()
    {
        var nestedPath = Path.Combine(_tempDirectory, "nested", "subfolder", ".codecondenser", "snapshot.json");
        var snapshot = new AnalysisSnapshot { Version = 1 };

        await _store.SaveAsync(nestedPath, snapshot);

        File.Exists(nestedPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_PerformsAtomicSaveWithoutLeavingTempFile()
    {
        var snapshot = new AnalysisSnapshot { Version = 1 };

        await _store.SaveAsync(_snapshotPath, snapshot);

        File.Exists(_snapshotPath).Should().BeTrue();
        File.Exists(_snapshotPath + ".tmp").Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_WhenFileAlreadyExists_OverwritesWithNewContent()
    {
        var snapshot1 = new AnalysisSnapshot
        {
            Version = 1,
            CommentCache = { ["hash1"] = "Summary 1" }
        };
        await _store.SaveAsync(_snapshotPath, snapshot1);

        var snapshot2 = new AnalysisSnapshot
        {
            Version = 2,
            CommentCache = { ["hash2"] = "Summary 2" }
        };
        await _store.SaveAsync(_snapshotPath, snapshot2);

        var loaded = await _store.LoadAsync(_snapshotPath);
        loaded.Should().NotBeNull();
        loaded!.Version.Should().Be(2);
        loaded.CommentCache.Should().ContainKey("hash2");
        loaded.CommentCache.Should().NotContainKey("hash1");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
