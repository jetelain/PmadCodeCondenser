using AwesomeAssertions;
using Pmad.CodeCondenser.Core.Clones;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Core.Snapshots;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Clones;

public class SubMethodIncrementalCacheTests : IDisposable
{
    private readonly string _testDir;
    private readonly InMemorySnapshotStore _snapshotStore;
    private readonly InMemorySubMethodCacheStore _subMethodCacheStore;

    public SubMethodIncrementalCacheTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "SubMethodIncrementalTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _snapshotStore = new InMemorySnapshotStore();
        _subMethodCacheStore = new InMemorySubMethodCacheStore();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task AnalyzeAsync_WithSubMethodClones_SavesSubMethodCache()
    {
        var file1 = Path.Combine(_testDir, "File1.cs");
        var file2 = Path.Combine(_testDir, "File2.cs");

        var code1 = @"
namespace App;
public class S1
{
    public void M1()
    {
        int a1 = 1;
        string a2 = ""2"";
        bool a3 = true;
        double a4 = 4.0;
        char a5 = '5';
        long a6 = 6L;
        short a7 = 7;
        byte a8 = 8;
        float a9 = 9f;
        decimal a10 = 10m;
        return;
    }
}";

        var code2 = @"
namespace App;
public class S2
{
    public void M2()
    {
        throw new System.InvalidOperationException();
        int b1 = 10;
        string b2 = ""20"";
        bool b3 = false;
        double b4 = 40.0;
        char b5 = 'z';
        long b6 = 60L;
        short b7 = 70;
        byte b8 = 80;
        float b9 = 90f;
        decimal b10 = 100m;
    }
}";

        await File.WriteAllTextAsync(file1, code1);
        await File.WriteAllTextAsync(file2, code2);

        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            subMethodCacheStore: _subMethodCacheStore);

        var settings = new CodeCondenserSettings
        {
            EnableSubMethodCloneDetection = true,
            SubMethodStatementThreshold = 10,
            EnableCommentSummarization = false
        };

        var result = await engine.AnalyzeAsync(_testDir, settings);

        result.SubMethodCloneGroups.Should().HaveCount(1);
        result.SubMethodCloneGroups[0].StatementCount.Should().Be(10);

        _subMethodCacheStore.StoredCache.Should().NotBeNull();
        _subMethodCacheStore.StoredCache!.Files.Should().HaveCount(2);
    }

    [Fact]
    public async Task AnalyzeAsync_IncrementalMode_LoadsUnchangedFilesFromCache()
    {
        var file1 = Path.Combine(_testDir, "File1.cs");
        var file2 = Path.Combine(_testDir, "File2.cs");

        var code1 = @"
namespace App;
public class S1
{
    public void M1()
    {
        int a1 = 1;
        string a2 = ""2"";
        bool a3 = true;
        double a4 = 4.0;
        char a5 = '5';
        long a6 = 6L;
        short a7 = 7;
        byte a8 = 8;
        float a9 = 9f;
        decimal a10 = 10m;
        return;
    }
}";

        var code2 = @"
namespace App;
public class S2
{
    public void M2()
    {
        throw new System.InvalidOperationException();
        int b1 = 10;
        string b2 = ""20"";
        bool b3 = false;
        double b4 = 40.0;
        char b5 = 'z';
        long b6 = 60L;
        short b7 = 70;
        byte b8 = 80;
        float b9 = 90f;
        decimal b10 = 100m;
    }
}";

        await File.WriteAllTextAsync(file1, code1);
        await File.WriteAllTextAsync(file2, code2);

        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            subMethodCacheStore: _subMethodCacheStore);

        var settings = new CodeCondenserSettings
        {
            EnableSubMethodCloneDetection = true,
            SubMethodStatementThreshold = 10,
            EnableCommentSummarization = false
        };

        // 1. Create baseline snapshot
        await engine.UpdateSnapshotBaselineAsync(_testDir, settings);
        _subMethodCacheStore.StoredCache.Should().NotBeNull();

        // 2. Add a 3rd file that duplicates the same 10 statements
        var file3 = Path.Combine(_testDir, "File3.cs");
        var code3 = @"
namespace App;
public class S3
{
    public void M3()
    {
        while (false) {}
        int c1 = 100;
        string c2 = ""200"";
        bool c3 = true;
        double c4 = 400.0;
        char c5 = 'w';
        long c6 = 600L;
        short c7 = 700;
        byte c8 = 88;
        float c9 = 900f;
        decimal c10 = 1000m;
    }
}";
        await File.WriteAllTextAsync(file3, code3);

        // 3. Incremental analyze
        var incResult = await engine.AnalyzeAsync(_testDir, settings, incremental: true);

        // The incremental analyze should detect clones involving the new/modified file
        incResult.SubMethodCloneGroups.Should().NotBeEmpty();
        var occurrences = incResult.SubMethodCloneGroups.SelectMany(g => g.Occurrences).ToList();
        occurrences.Select(o => o.MethodName).Should().Contain("M3");

        // The submethod cache should now have entries for all 3 files
        _subMethodCacheStore.StoredCache!.Files.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateSnapshotAsync_WithSubMethodClones_PersistsCache()
    {
        var file1 = Path.Combine(_testDir, "File1.cs");
        var code1 = @"
namespace App;
public class S1
{
    public void M1()
    {
        int a1 = 1;
        string a2 = ""2"";
        bool a3 = true;
        double a4 = 4.0;
        char a5 = '5';
        long a6 = 6L;
        short a7 = 7;
        byte a8 = 8;
        float a9 = 9f;
        decimal a10 = 10m;
    }
}";
        await File.WriteAllTextAsync(file1, code1);

        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            subMethodCacheStore: _subMethodCacheStore);

        var settings = new CodeCondenserSettings
        {
            EnableSubMethodCloneDetection = true,
            SubMethodStatementThreshold = 10
        };

        var snapshot = await engine.UpdateSnapshotBaselineAsync(_testDir, settings);
        snapshot.Should().NotBeNull();

        _subMethodCacheStore.StoredCache.Should().NotBeNull();
        _subMethodCacheStore.StoredCache!.Files.Should().ContainKey("File1.cs");
        var entry = _subMethodCacheStore.StoredCache.Files["File1.cs"];
        entry.Blocks.Should().HaveCount(1);
        entry.Blocks[0].StatementHashes.Should().HaveCount(10);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenNormalizerVersionMismatch_InvalidatesCache()
    {
        var file1 = Path.Combine(_testDir, "File1.cs");
        var code1 = @"
namespace App;
public class S1
{
    public void M1()
    {
        int a1 = 1;
        string a2 = ""2"";
        bool a3 = true;
        double a4 = 4.0;
        char a5 = '5';
        long a6 = 6L;
        short a7 = 7;
        byte a8 = 8;
        float a9 = 9f;
        decimal a10 = 10m;
    }
}";
        await File.WriteAllTextAsync(file1, code1);

        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            subMethodCacheStore: _subMethodCacheStore);

        var settings = new CodeCondenserSettings
        {
            EnableSubMethodCloneDetection = true,
            SubMethodStatementThreshold = 10
        };

        // 1. Pre-seed cache with an outdated normalizer version and dummy data
        var outdatedCache = new SubMethodCacheModel
        {
            Version = 1,
            NormalizerVersion = AnalysisSnapshot.CurrentNormalizerVersion - 1, // outdated
            Files =
            {
                ["File1.cs"] = new FileSubMethodCacheEntry
                {
                    FileHash = "old_hash",
                    NormalizerVersion = AnalysisSnapshot.CurrentNormalizerVersion - 1,
                    Blocks = []
                }
            }
        };
        await _subMethodCacheStore.SaveAsync("dummy_path", outdatedCache);

        // 2. Run analysis
        await engine.AnalyzeAsync(_testDir, settings);

        // 3. Cache should be replaced with updated normalizer version
        _subMethodCacheStore.StoredCache.Should().NotBeNull();
        _subMethodCacheStore.StoredCache!.NormalizerVersion.Should().Be(AnalysisSnapshot.CurrentNormalizerVersion);
        _subMethodCacheStore.StoredCache.Files["File1.cs"].Blocks.Should().HaveCount(1);
    }

    [Fact]
    public async Task AnalyzeAsync_FilesWithZeroBlocks_AreCachedInSubMethodCache()
    {
        var file1 = Path.Combine(_testDir, "File1.cs");
        var fileZero = Path.Combine(_testDir, "EmptyService.cs");

        var code1 = @"
namespace App;
public class S1
{
    public void M1()
    {
        int a1 = 1; string a2 = ""2""; bool a3 = true; double a4 = 4.0; char a5 = '5';
        long a6 = 6L; short a7 = 7; byte a8 = 8; float a9 = 9f; decimal a10 = 10m;
    }
}";
        var codeZero = @"
namespace App;
public interface IEmptyService
{
    void DoSomething();
}";

        await File.WriteAllTextAsync(file1, code1);
        await File.WriteAllTextAsync(fileZero, codeZero);

        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            subMethodCacheStore: _subMethodCacheStore);

        var settings = new CodeCondenserSettings
        {
            EnableSubMethodCloneDetection = true,
            SubMethodStatementThreshold = 10
        };

        // 1. Create baseline snapshot
        await engine.UpdateSnapshotBaselineAsync(_testDir, settings);

        _subMethodCacheStore.StoredCache.Should().NotBeNull();
        _subMethodCacheStore.StoredCache!.Files.Should().ContainKey("EmptyService.cs");
        _subMethodCacheStore.StoredCache.Files["EmptyService.cs"].Blocks.Should().BeEmpty();

        // 2. Run incremental analyze
        var result = await engine.AnalyzeAsync(_testDir, settings, incremental: true);

        // Zero-block file must remain cached so it is never re-parsed on subsequent runs
        _subMethodCacheStore.StoredCache.Files.Should().ContainKey("EmptyService.cs");
        _subMethodCacheStore.StoredCache.Files["EmptyService.cs"].Blocks.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenUnchangedFileMissesCache_ParsesAndUpdatesCache()
    {
        var file1 = Path.Combine(_testDir, "File1.cs");
        var file2 = Path.Combine(_testDir, "File2.cs");

        var code1 = @"
namespace App;
public class S1
{
    public void M1()
    {
        int a1 = 1; string a2 = ""2""; bool a3 = true; double a4 = 4.0; char a5 = '5';
        long a6 = 6L; short a7 = 7; byte a8 = 8; float a9 = 9f; decimal a10 = 10m;
    }
}";
        var code2 = @"
namespace App;
public class S2
{
    public void M2()
    {
        int b1 = 10; string b2 = ""20""; bool b3 = false; double b4 = 40.0; char b5 = 'z';
        long b6 = 60L; short b7 = 70; byte b8 = 80; float b9 = 90f; decimal b10 = 100m;
    }
}";
        await File.WriteAllTextAsync(file1, code1);
        await File.WriteAllTextAsync(file2, code2);

        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            subMethodCacheStore: _subMethodCacheStore);

        var settings = new CodeCondenserSettings
        {
            EnableSubMethodCloneDetection = true,
            SubMethodStatementThreshold = 10
        };

        await engine.UpdateSnapshotBaselineAsync(_testDir, settings);

        // Manually simulate a cache miss for File1.cs in subMethodCache
        _subMethodCacheStore.StoredCache!.Files.Remove("File1.cs");

        // Run incremental analyze
        var result = await engine.AnalyzeAsync(_testDir, settings, incremental: true);

        // File1.cs should have been parsed and re-added to the cache
        _subMethodCacheStore.StoredCache.Files.Should().ContainKey("File1.cs");
        _subMethodCacheStore.StoredCache.Files["File1.cs"].Blocks.Should().HaveCount(1);
    }

    [Fact]
    public async Task AnalyzeAsync_DeletedFiles_AreEvictedFromSubMethodCache()
    {
        var file1 = Path.Combine(_testDir, "File1.cs");
        var file2 = Path.Combine(_testDir, "File2.cs");

        var code1 = @"
namespace App;
public class S1
{
    public void M1()
    {
        int a1 = 1; string a2 = ""2""; bool a3 = true; double a4 = 4.0; char a5 = '5';
        long a6 = 6L; short a7 = 7; byte a8 = 8; float a9 = 9f; decimal a10 = 10m;
    }
}";
        var code2 = @"
namespace App;
public class S2
{
    public void M2()
    {
        int b1 = 10; string b2 = ""20""; bool b3 = false; double b4 = 40.0; char b5 = 'z';
        long b6 = 60L; short b7 = 70; byte b8 = 80; float b9 = 90f; decimal b10 = 100m;
    }
}";
        await File.WriteAllTextAsync(file1, code1);
        await File.WriteAllTextAsync(file2, code2);

        var engine = new CodeCondenserEngine(
            snapshotStore: _snapshotStore,
            subMethodCacheStore: _subMethodCacheStore);

        var settings = new CodeCondenserSettings
        {
            EnableSubMethodCloneDetection = true,
            SubMethodStatementThreshold = 10
        };

        await engine.UpdateSnapshotBaselineAsync(_testDir, settings);
        _subMethodCacheStore.StoredCache!.Files.Should().ContainKey("File2.cs");

        // Delete File2.cs
        File.Delete(file2);

        // Run incremental analyze
        await engine.AnalyzeAsync(_testDir, settings, incremental: true);

        // File2.cs should be evicted from the sub-method cache
        _subMethodCacheStore.StoredCache.Files.Should().NotContainKey("File2.cs");
        _subMethodCacheStore.StoredCache.Files.Should().ContainKey("File1.cs");
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

    private sealed class InMemorySubMethodCacheStore : ISubMethodCacheStore
    {
        public SubMethodCacheModel? StoredCache { get; private set; }

        public Task<SubMethodCacheModel?> LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(StoredCache);
        }

        public Task SaveAsync(string filePath, SubMethodCacheModel cache, CancellationToken cancellationToken = default)
        {
            StoredCache = cache;
            return Task.CompletedTask;
        }
    }
}
