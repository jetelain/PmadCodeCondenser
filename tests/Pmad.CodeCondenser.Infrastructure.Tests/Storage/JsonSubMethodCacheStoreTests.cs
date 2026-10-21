using FluentAssertions;
using Pmad.CodeCondenser.Core.Snapshots;
using Pmad.CodeCondenser.Infrastructure.Storage;
using Xunit;

namespace Pmad.CodeCondenser.Infrastructure.Tests.Storage;

public class JsonSubMethodCacheStoreTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _cachePath;
    private readonly JsonSubMethodCacheStore _store;

    public JsonSubMethodCacheStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "SubMethodCacheTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _cachePath = Path.Combine(_tempDirectory, "submethod-cache.json");
        _store = new JsonSubMethodCacheStore();
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
    public async Task SaveAsync_WhenCacheIsNull_ThrowsArgumentNullException()
    {
        var act = async () => await _store.SaveAsync(_cachePath, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAsync_WhenPathIsInvalid_ThrowsArgumentException(string? invalidPath)
    {
        var model = new SubMethodCacheModel();
        var act = async () => await _store.SaveAsync(invalidPath!, model);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesData()
    {
        var cache = new SubMethodCacheModel
        {
            Version = 1,
            NormalizerVersion = 2,
            Files =
            {
                ["FileA.cs"] = new FileSubMethodCacheEntry
                {
                    FileHash = "hash123",
                    NormalizerVersion = 2,
                    Blocks =
                    [
                        new SubMethodBlockCacheDto
                        {
                            MethodName = "Process",
                            BlockScopeId = "scope1",
                            ProjectId = "Project1",
                            StatementHashes = [100ul, 200ul, 300ul],
                            StatementStartLines = [10, 11, 12],
                            StatementEndLines = [10, 11, 12]
                        }
                    ]
                }
            }
        };

        await _store.SaveAsync(_cachePath, cache);
        var loaded = await _store.LoadAsync(_cachePath);

        loaded.Should().NotBeNull();
        loaded!.Version.Should().Be(1);
        loaded.NormalizerVersion.Should().Be(2);
        loaded.Files.Should().ContainKey("FileA.cs");
        var entry = loaded.Files["FileA.cs"];
        entry.FileHash.Should().Be("hash123");
        entry.NormalizerVersion.Should().Be(2);
        entry.Blocks.Should().HaveCount(1);
        entry.Blocks[0].MethodName.Should().Be("Process");
        entry.Blocks[0].StatementHashes.Should().Equal([100ul, 200ul, 300ul]);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
