using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Clones;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Core.Snapshots;

namespace Pmad.CodeCondenser.Core.Tests.Clones;

public class AstNormalizationTuningTests
{
    private readonly AstNormalizer _normalizer = new();
    private readonly MethodHasher _hasher = new();
    private readonly CloneDetector _detector = new();

    private static MethodDeclarationSyntax ParseMethod(string code)
    {
        var source = code.Contains("class") ? code : $"class Wrapper {{ {code} }}";
        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
    }

    [Fact]
    public void Cluster13_Regression_StageRegistryTests_And_ExecutionEnvironmentTests_ProduceDifferentHashes()
    {
        // Test A (StageRegistryAndFactoryTests)
        var testA = ParseMethod(@"
public void StageRegistryTests()
{
    var registry = new StageRegistry();
    Assert.Contains(""docker"", registry.GetTypes());
    Assert.Contains(""shell"", registry.GetTypes());
    Assert.Contains(""dotnet"", registry.GetTypes());
    Assert.Contains(""node"", registry.GetTypes());
    Assert.Contains(""python"", registry.GetTypes());
    Assert.Contains(""ruby"", registry.GetTypes());
    Assert.Contains(""go"", registry.GetTypes());
    Assert.Contains(""rust"", registry.GetTypes());
}");

        // Test B (ExecutionEnvironmentTests - completely unrelated assembly and domain)
        var testB = ParseMethod(@"
public void ExecutionEnvironmentTests()
{
    var env = new ExecutionEnvironment();
    Assert.Contains(""git"", env.GetInstalledTools());
    Assert.Contains(""docker"", env.GetInstalledTools());
    Assert.Contains(""curl"", env.GetInstalledTools());
    Assert.Contains(""wget"", env.GetInstalledTools());
    Assert.Contains(""tar"", env.GetInstalledTools());
    Assert.Contains(""unzip"", env.GetInstalledTools());
    Assert.Contains(""zip"", env.GetInstalledTools());
    Assert.Contains(""bash"", env.GetInstalledTools());
}");

        var normA = _normalizer.Normalize(testA);
        var normB = _normalizer.Normalize(testB);

        var hashA = _hasher.ComputeHash(normA);
        var hashB = _hasher.ComputeHash(normB);

        // Preserved invocation names (GetTypes vs GetInstalledTools) prevent accidental collisions
        hashA.Should().NotBe(hashB);
    }

    [Fact]
    public void Clusters9And18_Unification_EnumAndLiteralMutations_ProduceIdenticalHashes()
    {
        var updateTitle = ParseMethod(@"
public async Task UpdateTitleAsync()
{
    var entity = ArrangeEntity();
    await Service.MutateAsync(entity, ""New Title"");
    Assert.Equal(""New Title"", entity.Value);
    await AuditLog.VerifyRecordedAsync(entity.Id);
    var snapshot = await Service.GetSnapshotAsync(entity.Id);
    Assert.NotNull(snapshot);
    Assert.True(snapshot.IsUpdated);
    Assert.False(snapshot.IsDeleted);
    Metrics.RecordChange(snapshot.Version);
    return;
}");

        var updateDescription = ParseMethod(@"
public async Task UpdateDescriptionAsync()
{
    var entity = ArrangeEntity();
    await Service.MutateAsync(entity, ""New Description"");
    Assert.Equal(""New Description"", entity.Value);
    await AuditLog.VerifyRecordedAsync(entity.Id);
    var snapshot = await Service.GetSnapshotAsync(entity.Id);
    Assert.NotNull(snapshot);
    Assert.True(snapshot.IsUpdated);
    Assert.False(snapshot.IsDeleted);
    Metrics.RecordChange(snapshot.Version);
    return;
}");

        var updatePriority = ParseMethod(@"
public async Task UpdatePriorityAsync()
{
    var entity = ArrangeEntity();
    await Service.MutateAsync(entity, TaskPriority.Critical);
    Assert.Equal(TaskPriority.Critical, entity.Value);
    await AuditLog.VerifyRecordedAsync(entity.Id);
    var snapshot = await Service.GetSnapshotAsync(entity.Id);
    Assert.NotNull(snapshot);
    Assert.True(snapshot.IsUpdated);
    Assert.False(snapshot.IsDeleted);
    Metrics.RecordChange(snapshot.Version);
    return;
}");

        var updateMergeMode = ParseMethod(@"
public async Task UpdateMergeModeAsync()
{
    var entity = ArrangeEntity();
    await Service.MutateAsync(entity, MergeMode.FastForwardOnly);
    Assert.Equal(MergeMode.FastForwardOnly, entity.Value);
    await AuditLog.VerifyRecordedAsync(entity.Id);
    var snapshot = await Service.GetSnapshotAsync(entity.Id);
    Assert.NotNull(snapshot);
    Assert.True(snapshot.IsUpdated);
    Assert.False(snapshot.IsDeleted);
    Metrics.RecordChange(snapshot.Version);
    return;
}");

        var norm1 = _normalizer.Normalize(updateTitle);
        var norm2 = _normalizer.Normalize(updateDescription);
        var norm3 = _normalizer.Normalize(updatePriority);
        var norm4 = _normalizer.Normalize(updateMergeMode);

        var h1 = _hasher.ComputeHash(norm1);
        var h2 = _hasher.ComputeHash(norm2);
        var h3 = _hasher.ComputeHash(norm3);
        var h4 = _hasher.ComputeHash(norm4);

        // String literals and enum values normalize to CONST, merging the clusters
        h1.Should().Be(h2);
        h1.Should().Be(h3);
        h1.Should().Be(h4);

        var methods = new List<MethodCloneInfo>
        {
            new("Tests.cs", "UpdateTitleAsync", 1, 15, 10, h1, "sig1"),
            new("Tests.cs", "UpdateDescriptionAsync", 1, 15, 10, h2, "sig2"),
            new("Tests.cs", "UpdatePriorityAsync", 1, 15, 10, h3, "sig3"),
            new("Tests.cs", "UpdateMergeModeAsync", 1, 15, 10, h4, "sig4")
        };

        var groups = _detector.GroupClones(methods);
        groups.Should().HaveCount(1);
        groups[0].Methods.Should().HaveCount(4);
    }

    [Fact]
    public void Cluster23_SymmetricPair_JoinTaskGroupAsync_And_LeaveTaskGroupAsync_TaggedAsComplementaryPair()
    {
        var m1 = new MethodCloneInfo("Conn.cs", "JoinTaskGroupAsync", 1, 10, 8, "hash_conn", "sig1", "Default");
        var m2 = new MethodCloneInfo("Conn.cs", "LeaveTaskGroupAsync", 11, 20, 8, "hash_conn", "sig2", "Default");

        var groups = _detector.GroupClones([m1, m2]);

        groups.Should().HaveCount(1);
        groups[0].IsComplementaryPair.Should().BeTrue();
    }

    [Fact]
    public void InvocationTargetPreservation_And_ValueContextFolding_BehaveAccurately()
    {
        // 1. Console.WriteLine("Hello") -> VAR.WriteLine(CONST), not CONST(CONST)
        var m1 = ParseMethod(@"public void Log() { Console.WriteLine(""Hello""); }");
        var norm1 = _normalizer.Normalize(m1).NormalizeWhitespace().ToFullString();
        norm1.Should().Contain("VAR.WriteLine(CONST);");
        norm1.Should().NotContain("CONST(CONST)");

        // 2. var x = TaskPriority.Critical; and return String.Empty; fold to CONST
        var m2 = ParseMethod(@"public string GetStatus() { var x = TaskPriority.Critical; return String.Empty; }");
        var norm2 = _normalizer.Normalize(m2).NormalizeWhitespace().ToFullString();
        norm2.Should().Contain("VAR VAR = CONST;");
        norm2.Should().Contain("return CONST;");

        // 3. nameof(MyType.Prop), default(CancellationToken), and default literal fold to CONST
        var m3 = ParseMethod(@"public void Def() { var a = nameof(MyType.Prop); var b = default(CancellationToken); var c = default; }");
        var norm3 = _normalizer.Normalize(m3).NormalizeWhitespace().ToFullString();
        norm3.Should().Contain("VAR VAR = CONST;");
        norm3.Should().NotContain("nameof");
        norm3.Should().NotContain("default");

        // 4. Instance member chains like order.Customer.Name and request.Path do NOT fold to CONST
        var m4 = ParseMethod(@"public void Chain(Order order) { var x = order.Customer.Name; var y = request.Path; }");
        var norm4 = _normalizer.Normalize(m4).NormalizeWhitespace().ToFullString();
        norm4.Should().Contain("VAR.VAR.VAR");
        norm4.Should().NotContain("VAR VAR = CONST;");

        // 5. Direct calls and 'this' prefixed calls normalize identically
        var mDirect = ParseMethod(@"public void Direct(int x) { ProcessOrder(x); }");
        var mThis = ParseMethod(@"public void WithThis(int x) { this.ProcessOrder(x); }");
        var normDirect = _normalizer.Normalize(mDirect).NormalizeWhitespace().ToFullString();
        var normThis = _normalizer.Normalize(mThis).NormalizeWhitespace().ToFullString();
        normDirect.Should().Contain("METHOD_CALL:ProcessOrder(VAR);");
        normDirect.Should().Be(normThis);

        // 6. Generic invocations preserve generic method name with VAR type args
        var mGen = ParseMethod(@"public void Gen(List<string> list) { Assert.Contains<string>(""hello"", list); }");
        var normGen = _normalizer.Normalize(mGen).NormalizeWhitespace().ToFullString();
        normGen.Should().Contain("VAR.Contains<VAR>(CONST, VAR);");

        // 7. Base call preserves target method name
        var mBase = ParseMethod(@"public void BaseCall(int x) { base.Process(x); }");
        var normBase = _normalizer.Normalize(mBase).NormalizeWhitespace().ToFullString();
        normBase.Should().Contain("BASE.Process(VAR);");

        // 8. Null-conditional call preserves target method name
        var mNullCond = ParseMethod(@"public void NullCond(Service s) { s?.Execute(); }");
        var normNullCond = _normalizer.Normalize(mNullCond).NormalizeWhitespace().ToFullString();
        normNullCond.Should().Contain("VAR?.Execute();");
    }

    [Fact]
    public async Task Snapshot_NormalizerUpgradeMigration_RebasesBaselineInIncrementalMode()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "codecondenser_upgrade_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var unchangedFile = Path.Combine(tempDir, "Unchanged.cs");
            var unchangedCode = @"
public class Unchanged
{
    public void Keep()
    {
        int a = 1;
        int b = 2;
        int c = a + b;
        int d = c * 2;
        int e = d - 1;
        int f = e + 3;
        int g = f * 4;
        Console.WriteLine(g);
    }
}";
            await File.WriteAllTextAsync(unchangedFile, unchangedCode);

            var oldHash = "v1_legacy_hash_1234567890abcdef1234567890abcdef1234567890abcdef12345678";
            var fileHash = await new FileHasher().ComputeFileHashAsync(unchangedFile);

            var baseline = new AnalysisSnapshot
            {
                Version = 1,
                NormalizerVersion = 1, // older normalizer version!
                Files = new Dictionary<string, FileSnapshotInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Unchanged.cs"] = new FileSnapshotInfo
                    {
                        FileHash = fileHash
                    }
                },
                MethodHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Unchanged.cs::public void Keep()"] = oldHash
                }
            };

            var fakeStore = new InMemorySnapshotStore(baseline);
            bool infoLogged = false;
            var engine = new CodeCondenserEngine(snapshotStore: fakeStore);
            engine.InfoLogged += msg =>
            {
                if (msg.Contains("Normalizer version upgraded (v1 -> v2)"))
                {
                    infoLogged = true;
                }
            };

            var settings = new CodeCondenserSettings
            {
                DuplicationStatementThreshold = 8,
                EnableDuplicationDetection = true,
                EnableCommentSummarization = false
            };

            var result = await engine.AnalyzeAsync(tempDir, settings, incremental: true);

            // In-memory rebase prevented unchanged methods from falsely reporting as new/modified clones
            result.CloneGroups.Should().BeEmpty();
            infoLogged.Should().BeTrue();

            // Baseline in memory was rebased to NormalizerVersion: 2
            baseline.NormalizerVersion.Should().Be(2);
            baseline.MethodHashes["Unchanged.cs::public void Keep()"].Should().NotBe(oldHash);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ProjectLocator_MemoizesIntermediateAncestors_And_HandlesDefault()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "codecondenser_proj_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var projDir = Path.Combine(tempDir, "MyProject");
            var nested1 = Path.Combine(projDir, "Nested1");
            var nested2 = Path.Combine(nested1, "Nested2");
            Directory.CreateDirectory(nested2);
            File.WriteAllText(Path.Combine(projDir, "MyProject.csproj"), "<Project />");
            var nestedFile = Path.Combine(nested2, "FileInNested.cs");
            File.WriteAllText(nestedFile, "class Foo {}");

            var standaloneDir = Path.Combine(tempDir, "Standalone");
            Directory.CreateDirectory(standaloneDir);
            var standaloneFile = Path.Combine(standaloneDir, "Isolated.cs");
            File.WriteAllText(standaloneFile, "class Bar {}");

            var locator = new ProjectLocator();

            // 1. Locate for nested file
            var projId = locator.LocateProjectId(nestedFile, tempDir);
            projId.Should().Be("MyProject");

            // Verify intermediate ancestors were memoized in locator.Cache
            locator.Cache.Should().ContainKey(Path.GetFullPath(nested2).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            locator.Cache.Should().ContainKey(Path.GetFullPath(nested1).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            locator.Cache.Should().ContainKey(Path.GetFullPath(projDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            // 2. Locate for standalone file outside csproj
            var standaloneProjId = locator.LocateProjectId(standaloneFile, tempDir);
            standaloneProjId.Should().Be("Default");
            var normStandalone = Path.GetFullPath(standaloneDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            locator.Cache.Should().ContainKey(normStandalone);
            locator.Cache[normStandalone].Should().BeNull();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void CrossProjectThreshold_PartitionsSmallClusters_And_PreservesLargeClusters()
    {
        // 1. Two methods from different projects with 8 statements (< 12): partitioned and discarded as noise
        var mProjA = new MethodCloneInfo("src/A/File1.cs", "Calc", 1, 10, 8, "hash_noise", "sig", "ProjectA");
        var mProjB = new MethodCloneInfo("src/B/File2.cs", "Calc", 1, 10, 8, "hash_noise", "sig", "ProjectB");

        var groupsSmall = _detector.GroupClones([mProjA, mProjB]);
        groupsSmall.Should().BeEmpty();

        // 2. Three methods: 2 in ProjectA, 1 in ProjectB (< 12 statements): ProjectA sub-cluster kept!
        var mProjA2 = new MethodCloneInfo("src/A/File2.cs", "Calc2", 1, 10, 8, "hash_partial", "sig", "ProjectA");
        var mProjA3 = new MethodCloneInfo("src/A/File3.cs", "Calc3", 1, 10, 8, "hash_partial", "sig", "ProjectA");
        var mProjB2 = new MethodCloneInfo("src/B/File2.cs", "Calc", 1, 10, 8, "hash_partial", "sig", "ProjectB");

        var groupsPartitioned = _detector.GroupClones([mProjA2, mProjA3, mProjB2]);
        groupsPartitioned.Should().HaveCount(1);
        groupsPartitioned[0].Methods.Should().HaveCount(2);
        groupsPartitioned[0].Methods.All(m => m.ProjectId == "ProjectA").Should().BeTrue();

        // 3. Two methods from different projects with 12 statements (>= 12): kept as cross-project cluster!
        var mLargeA = new MethodCloneInfo("src/A/File1.cs", "Large", 1, 15, 12, "hash_large", "sig", "ProjectA");
        var mLargeB = new MethodCloneInfo("src/B/File2.cs", "Large", 1, 15, 12, "hash_large", "sig", "ProjectB");

        var groupsLarge = _detector.GroupClones([mLargeA, mLargeB]);
        groupsLarge.Should().HaveCount(1);
        groupsLarge[0].Methods.Should().HaveCount(2);
    }

    [Fact]
    public void AssignmentLhs_DoesNotFoldToConst_And_DifferentPropertiesDoNotCollide()
    {
        var m1 = ParseMethod(@"
public void ConfigureA()
{
    Config.Timeout = 10;
    Config.Retries = 3;
}");
        var m2 = ParseMethod(@"
public void ConfigureB()
{
    State.Active = true;
    State.Counter = 0;
}");

        var norm1 = _normalizer.Normalize(m1).NormalizeWhitespace().ToFullString();
        var norm2 = _normalizer.Normalize(m2).NormalizeWhitespace().ToFullString();

        // Left-hand assignment targets must NOT be folded to CONST
        norm1.Should().Contain("VAR.VAR = CONST;");
        norm1.Should().NotContain("CONST = CONST;");

        norm2.Should().Contain("VAR.VAR = CONST;");
        norm2.Should().NotContain("CONST = CONST;");
    }

    [Fact]
    public void EnumAndConstantPattern_FoldsToConst_InPatternsSwitchLabelsAndCasts()
    {
        var mSwitch = ParseMethod(@"
public string Evaluate(TaskPriority priority)
{
    switch (priority)
    {
        case TaskPriority.Critical: return ""high"";
        case TaskPriority.Low: return ""low"";
        default: return ""normal"";
    }
}");
        var normSwitch = _normalizer.Normalize(mSwitch).NormalizeWhitespace().ToFullString();
        normSwitch.Should().Contain("case CONST:");

        var mSwitchExpr = ParseMethod(@"
public string Evaluate(TaskPriority priority)
{
    return priority switch
    {
        TaskPriority.Critical => ""high"",
        _ => ""normal""
    };
}");
        var normSwitchExpr = _normalizer.Normalize(mSwitchExpr).NormalizeWhitespace().ToFullString();
        normSwitchExpr.Should().Contain("CONST =>");

        var mEqual = ParseMethod(@"
public bool IsCritical(TaskPriority priority)
{
    return priority == TaskPriority.Critical;
}");
        var normEqual = _normalizer.Normalize(mEqual).NormalizeWhitespace().ToFullString();
        normEqual.Should().Contain("VAR == CONST;");

        var mCast = ParseMethod(@"
public int ToInt(TaskPriority priority)
{
    return (int)TaskPriority.Critical;
}");
        var normCast = _normalizer.Normalize(mCast).NormalizeWhitespace().ToFullString();
        normCast.Should().Contain("(int)CONST;");

        var mPredefined = ParseMethod(@"
public int GetMax()
{
    return int.MaxValue;
}");
        var normPredefined = _normalizer.Normalize(mPredefined).NormalizeWhitespace().ToFullString();
        normPredefined.Should().Contain("return CONST;");
    }

    [Fact]
    public void MatchesComplementaryPair_RequiresPascalCaseWordBoundary()
    {
        // 1. Valid PascalCase prefix pairs match
        var g1 = _detector.GroupClones([
            new("A.cs", "Add", 1, 5, 8, "h1", "sig"),
            new("A.cs", "Remove", 1, 5, 8, "h1", "sig")
        ]);
        g1.Should().HaveCount(1);
        g1[0].IsComplementaryPair.Should().BeTrue();

        var g2 = _detector.GroupClones([
            new("A.cs", "AddTaskAsync", 1, 5, 8, "h2", "sig"),
            new("A.cs", "RemoveTaskAsync", 1, 5, 8, "h2", "sig")
        ]);
        g2.Should().HaveCount(1);
        g2[0].IsComplementaryPair.Should().BeTrue();

        var g3 = _detector.GroupClones([
            new("A.cs", "AddAsync", 1, 5, 8, "h3", "sig"),
            new("A.cs", "RemoveAsync", 1, 5, 8, "h3", "sig")
        ]);
        g3.Should().HaveCount(1);
        g3[0].IsComplementaryPair.Should().BeTrue();

        // 2. Non-boundary matching (e.g. Address vs Removeress) must NOT match
        var g4 = _detector.GroupClones([
            new("A.cs", "Address", 1, 5, 8, "h4", "sig"),
            new("A.cs", "Removeress", 1, 5, 8, "h4", "sig")
        ]);
        g4.Should().HaveCount(1);
        g4[0].IsComplementaryPair.Should().BeFalse();
    }

    [Theory]
    [InlineData("public async Task JoinTaskGroupAsync(string group)", "JoinTaskGroupAsync")]
    [InlineData("public async Task<List<string>> QueryAsync<TKey, TValue>(TKey key)", "QueryAsync")]
    [InlineData("public void Keep()", "Keep")]
    [InlineData("void Add(int x)", "Add")]
    [InlineData("public static T Process<T>(T item)", "Process")]
    [InlineData("Keep", "Keep")]
    [InlineData("Task<string> GetAsync()", "GetAsync")]
    public void MethodKeyHelper_ExtractMethodName_ExtractsAccurately(string signature, string expectedName)
    {
        var name = MethodKeyHelper.ExtractMethodName(signature);
        name.Should().Be(expectedName);
    }

    [Fact]
    public void ProjectLocator_LocateProjectId_WithRelativePathAndRootDirectory_ResolvesCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "codecondenser_rel_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var projDir = Path.Combine(tempDir, "SubProject");
            Directory.CreateDirectory(projDir);
            File.WriteAllText(Path.Combine(projDir, "SubProject.csproj"), "<Project />");
            var srcDir = Path.Combine(projDir, "Src");
            Directory.CreateDirectory(srcDir);
            File.WriteAllText(Path.Combine(srcDir, "Test.cs"), "class C {}");

            var locator = new ProjectLocator();
            var relPath = "SubProject/Src/Test.cs";

            var projId = locator.LocateProjectId(relPath, tempDir);
            projId.Should().Be("SubProject");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SnapshotDiffer_CreateOrUpdateSnapshot_PreservesBaselineNormalizerVersion_WhenNotRebased()
    {
        var differ = new SnapshotDiffer();
        var baseline = new AnalysisSnapshot
        {
            Version = 1,
            NormalizerVersion = 1, // older normalizer version
            Files = new Dictionary<string, FileSnapshotInfo>
            {
                ["A.cs"] = new() { FileHash = "hashA" }
            },
            MethodHashes = new Dictionary<string, string>
            {
                ["A.cs::void Foo()"] = "oldHash"
            }
        };

        var currentFileHashes = new Dictionary<string, string>
        {
            ["A.cs"] = "hashA"
        };

        var snapshot = differ.CreateOrUpdateSnapshot(
            baseline,
            currentFileHashes,
            analyzedMethods: [],
            commentCache: new Dictionary<string, string>());

        snapshot.NormalizerVersion.Should().Be(1);
    }

    private sealed class InMemorySnapshotStore(AnalysisSnapshot snapshot) : ISnapshotStore
    {
        public AnalysisSnapshot StoredSnapshot { get; private set; } = snapshot;

        public Task<AnalysisSnapshot?> LoadAsync(string path, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AnalysisSnapshot?>(StoredSnapshot);
        }

        public Task SaveAsync(string path, AnalysisSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            StoredSnapshot = snapshot;
            return Task.CompletedTask;
        }
    }
}
