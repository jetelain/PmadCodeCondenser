using AwesomeAssertions;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Core.Snapshots;

namespace Pmad.CodeCondenser.Core.Tests.Snapshots;

public class SnapshotDifferTests : IDisposable
{
    private readonly SnapshotDiffer _differ;
    private readonly string _tempDir;

    public SnapshotDifferTests()
    {
        _differ = new SnapshotDiffer();
        _tempDir = Path.Combine(Path.GetTempPath(), "SnapshotDifferTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    private static MethodCloneInfo CreateMethod(
        string filePath,
        string methodName,
        string signature,
        string normalizedHash)
    {
        return new MethodCloneInfo(
            FilePath: filePath,
            MethodName: methodName,
            StartLine: 1,
            EndLine: 10,
            StatementCount: 8,
            NormalizedHash: normalizedHash,
            Signature: signature);
    }

    private static VerboseComment CreateComment(string text, string filePath = "src/Foo.cs")
    {
        return new VerboseComment(
            FilePath: filePath,
            StartLine: 1,
            EndLine: 2,
            SpanStart: 0,
            SpanLength: text.Length,
            Kind: CommentKind.SingleLine,
            OriginalText: text,
            LeadingWhitespace: "    ");
    }

    [Fact]
    public void DiffFiles_WhenBaselineIsNull_MarksAllFilesAsAdded()
    {
        var currentHashes = new Dictionary<string, string>
        {
            ["src/A.cs"] = "hashA",
            ["src/B.cs"] = "hashB"
        };

        var result = _differ.DiffFiles(currentHashes, null);

        result.HasChanges.Should().BeTrue();
        result.AddedFiles.Should().BeEquivalentTo(["src/A.cs", "src/B.cs"]);
        result.ModifiedFiles.Should().BeEmpty();
        result.UnchangedFiles.Should().BeEmpty();
        result.DeletedFiles.Should().BeEmpty();
        result.FilesToAnalyze.Should().BeEquivalentTo(["src/A.cs", "src/B.cs"]);
    }

    [Fact]
    public void DiffFiles_WithBaseline_CorrectlyCategorizesAddedModifiedUnchangedDeleted()
    {
        var baseline = new AnalysisSnapshot
        {
            Files =
            {
                ["src/Unchanged.cs"] = new FileSnapshotInfo { FileHash = "hash1" },
                ["src/Modified.cs"] = new FileSnapshotInfo { FileHash = "oldHash" },
                ["src/Deleted.cs"] = new FileSnapshotInfo { FileHash = "hashDel" }
            }
        };

        var currentHashes = new Dictionary<string, string>
        {
            ["src/Unchanged.cs"] = "hash1",
            ["src/Modified.cs"] = "newHash",
            ["src/Added.cs"] = "hashAdd"
        };

        var result = _differ.DiffFiles(currentHashes, baseline);

        result.HasChanges.Should().BeTrue();
        result.UnchangedFiles.Should().Equal(["src/Unchanged.cs"]);
        result.ModifiedFiles.Should().Equal(["src/Modified.cs"]);
        result.AddedFiles.Should().Equal(["src/Added.cs"]);
        result.DeletedFiles.Should().Equal(["src/Deleted.cs"]);
        result.FilesToAnalyze.Should().BeEquivalentTo(["src/Modified.cs", "src/Added.cs"]);
    }

    [Fact]
    public void DiffFiles_WhenNoChanges_ReturnsAllUnchangedAndHasChangesIsFalse()
    {
        var baseline = new AnalysisSnapshot
        {
            Files =
            {
                ["src/A.cs"] = new FileSnapshotInfo { FileHash = "hashA" }
            }
        };

        var currentHashes = new Dictionary<string, string>
        {
            ["src/A.cs"] = "hashA"
        };

        var result = _differ.DiffFiles(currentHashes, baseline);

        result.HasChanges.Should().BeFalse();
        result.UnchangedFiles.Should().Equal(["src/A.cs"]);
        result.AddedFiles.Should().BeEmpty();
        result.ModifiedFiles.Should().BeEmpty();
        result.DeletedFiles.Should().BeEmpty();
        result.FilesToAnalyze.Should().BeEmpty();
    }

    [Fact]
    public async Task DiffFilesAsync_ReadsFilesFromDiskAndDiffsAgainstBaseline()
    {
        var fileA = Path.Combine(_tempDir, "A.cs");
        var fileB = Path.Combine(_tempDir, "B.cs");
        await File.WriteAllTextAsync(fileA, "Content A");
        await File.WriteAllTextAsync(fileB, "Content B");

        var baseline = new AnalysisSnapshot
        {
            Files =
            {
                ["A.cs"] = new FileSnapshotInfo { FileHash = new FileHasher().ComputeHash("Content A") },
                ["B.cs"] = new FileSnapshotInfo { FileHash = "oldHashForB" }
            }
        };

        var result = await _differ.DiffFilesAsync(_tempDir, [fileA, fileB], baseline);

        result.UnchangedFiles.Should().Equal(["A.cs"]);
        result.ModifiedFiles.Should().Equal(["B.cs"]);
        result.AddedFiles.Should().BeEmpty();
        result.DeletedFiles.Should().BeEmpty();
    }

    [Fact]
    public void DiffMethods_WhenBaselineIsNull_ReturnsAllAsNew()
    {
        var method1 = CreateMethod("src/A.cs", "Foo", "void Foo()", "hash1");
        var method2 = CreateMethod("src/B.cs", "Bar", "void Bar()", "hash2");

        var result = _differ.DiffMethods([method1, method2], null);

        result.HasChanges.Should().BeTrue();
        result.NewMethods.Should().BeEquivalentTo([method1, method2]);
        result.ModifiedMethods.Should().BeEmpty();
        result.UnchangedMethods.Should().BeEmpty();
    }

    [Fact]
    public void DiffMethods_WithBaseline_CategorizesNewModifiedAndUnchanged()
    {
        var baseline = new AnalysisSnapshot
        {
            MethodHashes =
            {
                ["src/A.cs::void Unchanged()"] = "hash1",
                ["src/A.cs::void Modified()"] = "oldHash"
            }
        };

        var mUnchanged = CreateMethod("src/A.cs", "Unchanged", "void Unchanged()", "hash1");
        var mModified = CreateMethod("src/A.cs", "Modified", "void Modified()", "newHash");
        var mNew = CreateMethod("src/A.cs", "BrandNew", "void BrandNew()", "hashNew");

        var result = _differ.DiffMethods([mUnchanged, mModified, mNew], baseline);

        result.HasChanges.Should().BeTrue();
        result.UnchangedMethods.Should().Equal([mUnchanged]);
        result.ModifiedMethods.Should().Equal([mModified]);
        result.NewMethods.Should().Equal([mNew]);
    }

    [Fact]
    public void FilterNewCloneGroups_WhenBaselineIsNull_ReturnsAllGroups()
    {
        var m1 = CreateMethod("src/A.cs", "Foo", "void Foo()", "hashClone");
        var m2 = CreateMethod("src/B.cs", "Bar", "void Bar()", "hashClone");
        var group = new CodeCloneGroup("hashClone", [m1, m2]);

        var filtered = _differ.FilterNewCloneGroups([group], null);

        filtered.Should().ContainSingle().Which.Should().Be(group);
    }

    [Fact]
    public void FilterNewCloneGroups_WhenAllMethodsAreUnchanged_FiltersOutGroup()
    {
        var baseline = new AnalysisSnapshot
        {
            Files =
            {
                ["src/A.cs"] = new FileSnapshotInfo { FileHash = "fHashA" },
                ["src/B.cs"] = new FileSnapshotInfo { FileHash = "fHashB" }
            },
            MethodHashes =
            {
                ["src/A.cs::void Foo()"] = "hashClone",
                ["src/B.cs::void Bar()"] = "hashClone"
            }
        };

        var m1 = CreateMethod("src/A.cs", "Foo", "void Foo()", "hashClone");
        var m2 = CreateMethod("src/B.cs", "Bar", "void Bar()", "hashClone");
        var group = new CodeCloneGroup("hashClone", [m1, m2]);

        var fileDiff = new FileDiffResult([], [], ["src/A.cs", "src/B.cs"], []);

        var filtered = _differ.FilterNewCloneGroups([group], baseline, fileDiff);

        filtered.Should().BeEmpty();
    }

    [Fact]
    public void FilterNewCloneGroups_WhenOneMethodIsNew_RetainsGroup()
    {
        var baseline = new AnalysisSnapshot
        {
            MethodHashes =
            {
                ["src/A.cs::void Foo()"] = "hashClone"
            }
        };

        var m1 = CreateMethod("src/A.cs", "Foo", "void Foo()", "hashClone");
        var m2 = CreateMethod("src/B.cs", "Bar", "void Bar()", "hashClone"); // new method!
        var group = new CodeCloneGroup("hashClone", [m1, m2]);

        var filtered = _differ.FilterNewCloneGroups([group], baseline);

        filtered.Should().ContainSingle().Which.Should().Be(group);
    }

    [Fact]
    public void FilterNewCloneGroups_WhenOneMethodIsFromModifiedFile_RetainsGroup()
    {
        var baseline = new AnalysisSnapshot
        {
            Files =
            {
                ["src/A.cs"] = new FileSnapshotInfo { FileHash = "oldA" },
                ["src/B.cs"] = new FileSnapshotInfo { FileHash = "oldB" }
            },
            MethodHashes =
            {
                ["src/A.cs::void Foo()"] = "hashClone",
                ["src/B.cs::void Bar()"] = "hashClone"
            }
        };

        var m1 = CreateMethod("src/A.cs", "Foo", "void Foo()", "hashClone");
        var m2 = CreateMethod("src/B.cs", "Bar", "void Bar()", "hashClone");
        var group = new CodeCloneGroup("hashClone", [m1, m2]);

        // File A is modified!
        var fileDiff = new FileDiffResult([], ["src/A.cs"], ["src/B.cs"], []);

        var filtered = _differ.FilterNewCloneGroups([group], baseline, fileDiff);

        filtered.Should().ContainSingle().Which.Should().Be(group);
    }

    [Fact]
    public void FilterNewComments_WhenBaselineIsNull_ReturnsAllComments()
    {
        var c1 = CreateComment("// First comment");
        var c2 = CreateComment("// Second comment");

        var filtered = _differ.FilterNewComments([c1, c2], null);

        filtered.Should().Equal([c1, c2]);
    }

    [Fact]
    public void FilterNewComments_WithBaseline_FiltersOutCachedComments()
    {
        var cCached = CreateComment("// Already summarized comment");
        var cUncached = CreateComment("// Brand new uncached comment");

        var baseline = new AnalysisSnapshot
        {
            CommentCache =
            {
                [cCached.ContentHash] = "Existing summary"
            }
        };

        var filtered = _differ.FilterNewComments([cCached, cUncached], baseline);

        filtered.Should().Equal([cUncached]);
    }

    [Fact]
    public void CreateOrUpdateSnapshot_FullRun_BuildsCompleteSnapshot()
    {
        var fileHashes = new Dictionary<string, string>
        {
            ["src/A.cs"] = "hashA",
            ["src/B.cs"] = "hashB"
        };

        var m1 = CreateMethod("src/A.cs", "Foo", "void Foo()", "methodHashA");
        var m2 = CreateMethod("src/B.cs", "Bar", "void Bar()", "methodHashB");

        var commentCache = new Dictionary<string, string>
        {
            ["commentHash1"] = "Summary 1"
        };

        var snapshot = _differ.CreateOrUpdateSnapshot(
            baseline: null,
            currentFileHashes: fileHashes,
            analyzedMethods: [m1, m2],
            commentCache: commentCache);

        snapshot.Version.Should().Be(1);
        snapshot.Files.Should().HaveCount(2);
        snapshot.Files["src/A.cs"].FileHash.Should().Be("hashA");
        snapshot.Files["src/B.cs"].FileHash.Should().Be("hashB");
        snapshot.MethodHashes.Should().HaveCount(2);
        snapshot.MethodHashes["src/A.cs::void Foo()"].Should().Be("methodHashA");
        snapshot.MethodHashes["src/B.cs::void Bar()"].Should().Be("methodHashB");
        snapshot.CommentCache.Should().HaveCount(1);
        snapshot.CommentCache["commentHash1"].Should().Be("Summary 1");
    }

    [Fact]
    public void CreateOrUpdateSnapshot_IncrementalRun_PreservesUnchangedFilesAndMethods()
    {
        var oldTimestamp = DateTimeOffset.UtcNow.AddHours(-5);
        var baseline = new AnalysisSnapshot
        {
            Version = 1,
            Files =
            {
                ["src/Unchanged.cs"] = new FileSnapshotInfo { FileHash = "hashUnchanged", LastAnalyzed = oldTimestamp },
                ["src/Modified.cs"] = new FileSnapshotInfo { FileHash = "oldModHash", LastAnalyzed = oldTimestamp },
                ["src/Deleted.cs"] = new FileSnapshotInfo { FileHash = "hashDel", LastAnalyzed = oldTimestamp }
            },
            MethodHashes =
            {
                ["src/Unchanged.cs::void MethodInUnchanged()"] = "methodHashUnchanged",
                ["src/Modified.cs::void OldMethod()"] = "oldMethodHash",
                ["src/Deleted.cs::void MethodInDeleted()"] = "methodHashDeleted"
            },
            CommentCache =
            {
                ["commentHashExisting"] = "Existing Summary"
            }
        };

        var currentFileHashes = new Dictionary<string, string>
        {
            ["src/Unchanged.cs"] = "hashUnchanged",
            ["src/Modified.cs"] = "newModHash",
            ["src/Added.cs"] = "hashAdded"
        };

        // In incremental run, only modified and added files were analyzed!
        var mModifiedNew = CreateMethod("src/Modified.cs", "NewMethod", "void NewMethod()", "newMethodHash");
        var mAdded = CreateMethod("src/Added.cs", "AddedMethod", "void AddedMethod()", "addedMethodHash");

        var newComments = new Dictionary<string, string>
        {
            ["commentHashNew"] = "New Summary"
        };

        var snapshot = _differ.CreateOrUpdateSnapshot(
            baseline: baseline,
            currentFileHashes: currentFileHashes,
            analyzedMethods: [mModifiedNew, mAdded],
            commentCache: newComments);

        // Files verification:
        snapshot.Files.Should().HaveCount(3);
        snapshot.Files.Should().ContainKey("src/Unchanged.cs");
        snapshot.Files["src/Unchanged.cs"].FileHash.Should().Be("hashUnchanged");
        snapshot.Files["src/Unchanged.cs"].LastAnalyzed.Should().Be(oldTimestamp); // Timestamp preserved

        snapshot.Files.Should().ContainKey("src/Modified.cs");
        snapshot.Files["src/Modified.cs"].FileHash.Should().Be("newModHash");
        snapshot.Files["src/Modified.cs"].LastAnalyzed.Should().BeAfter(oldTimestamp); // Updated

        snapshot.Files.Should().ContainKey("src/Added.cs");
        snapshot.Files["src/Added.cs"].FileHash.Should().Be("hashAdded");

        snapshot.Files.Should().NotContainKey("src/Deleted.cs"); // Deleted removed

        // MethodHashes verification:
        snapshot.MethodHashes.Should().HaveCount(3);
        // Method from unchanged file is PRESERVED from baseline!
        snapshot.MethodHashes.Should().ContainKey("src/Unchanged.cs::void MethodInUnchanged()");
        snapshot.MethodHashes["src/Unchanged.cs::void MethodInUnchanged()"].Should().Be("methodHashUnchanged");

        // Old method from modified file is replaced with new method
        snapshot.MethodHashes.Should().NotContainKey("src/Modified.cs::void OldMethod()");
        snapshot.MethodHashes.Should().ContainKey("src/Modified.cs::void NewMethod()");
        snapshot.MethodHashes["src/Modified.cs::void NewMethod()"].Should().Be("newMethodHash");

        // Method from added file is added
        snapshot.MethodHashes.Should().ContainKey("src/Added.cs::void AddedMethod()");
        snapshot.MethodHashes["src/Added.cs::void AddedMethod()"].Should().Be("addedMethodHash");

        // Method from deleted file is removed
        snapshot.MethodHashes.Should().NotContainKey("src/Deleted.cs::void MethodInDeleted()");

        // CommentCache verification:
        snapshot.CommentCache.Should().HaveCount(2);
        snapshot.CommentCache["commentHashExisting"].Should().Be("Existing Summary");
        snapshot.CommentCache["commentHashNew"].Should().Be("New Summary");
    }

    [Fact]
    public void CreateOrUpdateSnapshot_WhenModifiedFileHasAllMethodsDeleted_DoesNotRetainGhostMethods()
    {
        var baseline = new AnalysisSnapshot
        {
            Files =
            {
                ["src/Service.cs"] = new FileSnapshotInfo { FileHash = "oldHash" }
            },
            MethodHashes =
            {
                ["src/Service.cs::void OldDeletedMethod()"] = "oldMethodHash"
            }
        };

        var currentFileHashes = new Dictionary<string, string>
        {
            ["src/Service.cs"] = "newHashModified"
        };

        // All methods were deleted from Service.cs, so analyzedMethods is empty
        var snapshot = _differ.CreateOrUpdateSnapshot(
            baseline: baseline,
            currentFileHashes: currentFileHashes,
            analyzedMethods: [],
            commentCache: new Dictionary<string, string>());

        snapshot.MethodHashes.Should().BeEmpty();
    }

    [Fact]
    public void CreateOrUpdateSnapshot_PreservesUnchangedBaselineTypeViolationsAndDropsDeletedFiles()
    {
        var baseline = new AnalysisSnapshot
        {
            Files =
            {
                ["src/UnchangedViolation.cs"] = new FileSnapshotInfo { FileHash = "hash1" },
                ["src/FixedFile.cs"] = new FileSnapshotInfo { FileHash = "oldHashFixed" },
                ["src/DeletedViolation.cs"] = new FileSnapshotInfo { FileHash = "delHash" }
            },
            TypeViolationFiles =
            {
                "src/UnchangedViolation.cs",
                "src/FixedFile.cs",
                "src/DeletedViolation.cs"
            }
        };

        var currentFileHashes = new Dictionary<string, string>
        {
            ["src/UnchangedViolation.cs"] = "hash1", // Unchanged
            ["src/FixedFile.cs"] = "newHashFixed",   // Modified and fixed
            ["src/NewViolation.cs"] = "newViolHash"   // Added
            // DeletedViolation.cs deleted
        };

        var snapshot = _differ.CreateOrUpdateSnapshot(
            baseline: baseline,
            currentFileHashes: currentFileHashes,
            analyzedMethods: [],
            commentCache: new Dictionary<string, string>(),
            typeViolationFiles: ["src/NewViolation.cs"], // Only NewViolation.cs has violation among scanned files
            isIncremental: true);

        snapshot.TypeViolationFiles.Should().BeEquivalentTo(
            ["src/UnchangedViolation.cs", "src/NewViolation.cs"]);
        snapshot.TypeViolationFiles.Should().NotContain("src/FixedFile.cs");
        snapshot.TypeViolationFiles.Should().NotContain("src/DeletedViolation.cs");
    }

    [Fact]
    public void CreateOrUpdateSnapshot_FullRunOrBaselineUpdate_AuthoritativeTypeViolationsDoesNotPreserveFixedUnchangedFiles()
    {
        var baseline = new AnalysisSnapshot
        {
            Files =
            {
                ["src/UnchangedPreviouslyViolating.cs"] = new FileSnapshotInfo { FileHash = "hash1" }
            },
            TypeViolationFiles =
            {
                "src/UnchangedPreviouslyViolating.cs"
            }
        };

        var currentFileHashes = new Dictionary<string, string>
        {
            ["src/UnchangedPreviouslyViolating.cs"] = "hash1"
        };

        // In full run / baseline update (isIncremental: false), all files are scanned and typeViolationFiles is authoritative.
        // If the file is now compliant (e.g. via tolerance or ignore settings change), it is not in typeViolationFiles.
        var snapshot = _differ.CreateOrUpdateSnapshot(
            baseline: baseline,
            currentFileHashes: currentFileHashes,
            analyzedMethods: [],
            commentCache: new Dictionary<string, string>(),
            typeViolationFiles: [], // Clean scan
            isIncremental: false);

        snapshot.TypeViolationFiles.Should().BeEmpty();
    }

    [Fact]
    public void FilterNewLinesOfCodeViolations_WhenBaselineIsNull_ReturnsAllViolations()
    {
        var violations = new List<LinesOfCodeViolation>
        {
            new() { FilePath = "src/A.cs", Identifier = "A.Method1()", Kind = LinesOfCodeKind.Method, LinesOfCode = 160, Threshold = 150, StartLine = 1, EndLine = 10, Category = FileCategory.Production },
            new() { FilePath = "src/B.cs", Identifier = "B", Kind = LinesOfCodeKind.Class, LinesOfCode = 1600, Threshold = 1500, StartLine = 1, EndLine = 100, Category = FileCategory.Production }
        };

        var filtered = _differ.FilterNewLinesOfCodeViolations(violations, null);

        filtered.Should().HaveCount(2);
    }

    [Fact]
    public void FilterNewLinesOfCodeViolations_WhenBaselineContainsViolations_FiltersOutBaselineViolations()
    {
        var baseline = new AnalysisSnapshot
        {
            LinesOfCodeViolationKeys = { "src/A.cs::Method::A.Method1()" }
        };

        var violations = new List<LinesOfCodeViolation>
        {
            new() { FilePath = "src/A.cs", Identifier = "A.Method1()", Kind = LinesOfCodeKind.Method, LinesOfCode = 160, Threshold = 150, StartLine = 1, EndLine = 10, Category = FileCategory.Production },
            new() { FilePath = "src/B.cs", Identifier = "B", Kind = LinesOfCodeKind.Class, LinesOfCode = 1600, Threshold = 1500, StartLine = 1, EndLine = 100, Category = FileCategory.Production }
        };

        var filtered = _differ.FilterNewLinesOfCodeViolations(violations, baseline);

        filtered.Should().HaveCount(1);
        filtered[0].Identifier.Should().Be("B");
    }

    [Fact]
    public void CreateOrUpdateSnapshot_StoresAndPreservesLinesOfCodeViolations()
    {
        var baseline = new AnalysisSnapshot
        {
            Files =
            {
                ["src/A.cs"] = new FileSnapshotInfo { FileHash = "h1" }
            },
            LinesOfCodeViolationKeys =
            {
                "src/A.cs::Method::A.Method1()"
            }
        };

        var snapshot = _differ.CreateOrUpdateSnapshot(
            baseline: baseline,
            currentFileHashes: new Dictionary<string, string> { ["src/A.cs"] = "h1", ["src/B.cs"] = "h2" },
            analyzedMethods: [],
            commentCache: new Dictionary<string, string>(),
            typeViolationFiles: [],
            linesOfCodeViolationKeys: ["src/B.cs::Class::B"],
            isIncremental: true);

        snapshot.LinesOfCodeViolationKeys.Should().Contain("src/A.cs::Method::A.Method1()");
        snapshot.LinesOfCodeViolationKeys.Should().Contain("src/B.cs::Class::B");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }
}
