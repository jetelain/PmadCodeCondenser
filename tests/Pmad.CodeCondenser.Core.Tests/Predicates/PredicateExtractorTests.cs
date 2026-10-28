using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Predicates;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Predicates;

public class PredicateExtractorTests
{
    private readonly PredicateExtractor _extractor = new();
    private readonly CodeCondenserSettings _settings = new()
    {
        PredicateLengthThreshold = 25,
        ExistingHelperPredicateLengthThreshold = 20
    };

    [Fact]
    public void ExtractPredicates_ExtractsIfConditionAndStaticHelper()
    {
        var code = """
            namespace MyNamespace;

            public static class TaskHelper
            {
                public static bool IsAutonomousTask(int? taskId, string? stageId) =>
                    taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase);

                public static void Process(int? taskId, string? stageId)
                {
                    if (taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase))
                    {
                        DoWork();
                    }
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var occurrences = _extractor.ExtractPredicates(tree, "TaskHelper.cs", _settings);

        Assert.Equal(2, occurrences.Count);

        var helper = occurrences.Single(o => o.IsHelperDeclaration);
        Assert.NotNull(helper.HelperInfo);
        Assert.Equal("IsAutonomousTask", helper.HelperInfo.HelperName);
        Assert.True(helper.HelperInfo.IsStatic);
        Assert.Equal(2, helper.HelperInfo.Parameters.Count);

        var callSite = occurrences.Single(o => !o.IsHelperDeclaration);
        Assert.Equal(helper.NormalizedSignature, callSite.NormalizedSignature);
        Assert.Equal(helper.NormalizedHash, callSite.NormalizedHash);
    }

    [Fact]
    public void ExtractPredicates_ExtractsLinqWhereLambda()
    {
        var code = """
            namespace MyNamespace;

            public class TargetSlot
            {
                public TargetLivenessState State { get; set; }
                public DateTimeOffset CachedAt { get; set; }

                public bool IsOnlineWithinTtl(DateTimeOffset now, TimeSpan ttl) =>
                    State == TargetLivenessState.Online && now - CachedAt <= ttl;

                public void FilterSlots(List<TargetSlot> slots)
                {
                    var online = slots.Where(slot => slot.State == TargetLivenessState.Online && DateTimeOffset.UtcNow - slot.CachedAt <= _onlineCacheTtl);
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var occurrences = _extractor.ExtractPredicates(tree, "TargetSlot.cs", _settings);

        Assert.Equal(2, occurrences.Count);

        var helper = occurrences.Single(o => o.IsHelperDeclaration);
        Assert.Equal("IsOnlineWithinTtl", helper.HelperInfo?.HelperName);
        Assert.False(helper.HelperInfo?.IsStatic);

        var callSite = occurrences.Single(o => !o.IsHelperDeclaration);
        Assert.Equal("slot", callSite.ReceiverExpression);
        Assert.Equal(helper.NormalizedSignature, callSite.InstanceNormalizedSignature);
    }

    [Fact]
    public void ExtractPredicates_ExtractsBooleanReturnStatement()
    {
        var code = """
            namespace MyNamespace;

            public class Service
            {
                public bool Validate(int? taskId, string? stageId)
                {
                    return taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase);
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var occurrences = _extractor.ExtractPredicates(tree, "Service.cs", _settings);

        // Single-statement method returning bool is extracted as helper declaration
        Assert.Single(occurrences);
        Assert.True(occurrences[0].IsHelperDeclaration);
        Assert.Equal("Validate", occurrences[0].HelperInfo?.HelperName);
    }

    [Fact]
    public void ExtractPredicates_IgnoresTrivialExpressions()
    {
        var code = """
            namespace MyNamespace;

            public class Service
            {
                public void Test(string? name, int count, bool isValid)
                {
                    if (name == null) { }
                    if (name != null) { }
                    if (count > 0) { }
                    if (isValid) { }
                    if (!isValid) { }
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var occurrences = _extractor.ExtractPredicates(tree, "Service.cs", _settings);

        Assert.Empty(occurrences);
    }

    [Fact]
    public void ExtractPredicates_RespectsSuppressionMarker()
    {
        var code = """
            namespace MyNamespace;

            public class Service
            {
                public void Test(int? taskId, string? stageId)
                {
                    // codecondenser:ignore-predicate
                    if (taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase))
                    {
                        DoWork();
                    }
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var occurrences = _extractor.ExtractPredicates(tree, "Service.cs", _settings);

        Assert.Empty(occurrences);
    }

    [Fact]
    public void ExtractPredicates_IgnoresPatternNullChecks()
    {
        var code = """
            namespace MyNamespace;

            public class Service
            {
                public void Test(object? item1, object? item2)
                {
                    if (item1 is not null) { }
                    if (item2 is null) { }
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var occurrences = _extractor.ExtractPredicates(tree, "Service.cs", _settings);

        Assert.Empty(occurrences);
    }

    [Theory]
    [InlineData("// nopredicate")]
    [InlineData("// codecondenser:ignore-bool")]
    [InlineData("[PredicateIgnore]")]
    public void ExtractPredicates_RespectsAdditionalSuppressionMarkers(string marker)
    {
        var code = $$"""
            namespace MyNamespace;

            public class Service
            {
                {{(marker.StartsWith('[') ? marker : "")}}
                public void Test(int? taskId, string? stageId)
                {
                    {{(marker.StartsWith('/') ? marker : "")}}
                    if (taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase))
                    {
                        DoWork();
                    }
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var occurrences = _extractor.ExtractPredicates(tree, "Service.cs", _settings);

        Assert.Empty(occurrences);
    }

    [Theory]
    [InlineData("[SuppressMessage(\"CodeCondenser\", \"PredicateDuplication\")]")]
    [InlineData("[SuppressMessage(\"CodeCondenser\", \"CC010\")]")]
    [InlineData("[CodeCondenserIgnore]")]
    [InlineData("// nopredicate")]
    public void ExtractPredicates_EnclosingClassLevelSuppression_SuppressesMethodPredicates(string marker)
    {
        var code = $$"""
            using System.Diagnostics.CodeAnalysis;
            namespace MyNamespace;

            {{marker}}
            public class Service
            {
                public void Test(int? taskId, string? stageId)
                {
                    if (taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase))
                    {
                        DoWork();
                    }
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var occurrences = _extractor.ExtractPredicates(tree, "Service.cs", _settings);

        Assert.Empty(occurrences);
    }

    [Fact]
    public void ExtractPredicates_ImplicitPrivateHelper_SetsIsPrivateTrueAndAccessibilityPrivate()
    {
        var code = """
            namespace MyNamespace;

            public class Service
            {
                // Implicit private method in class
                static bool IsAutonomous(int? taskId, string? stageId) =>
                    taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase);

                // Implicit private property in class
                bool IsReady =>
                    taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase);
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var occurrences = _extractor.ExtractPredicates(tree, "Service.cs", _settings);

        Assert.Equal(2, occurrences.Count);
        Assert.All(occurrences, o =>
        {
            Assert.True(o.IsHelperDeclaration);
            Assert.NotNull(o.HelperInfo);
            Assert.True(o.HelperInfo.IsPrivate);
            Assert.Equal("private", o.HelperInfo.Accessibility);
        });
    }

    [Fact]
    public void ExtractPredicates_InterfaceHelper_DefaultsToPublicAccessibility()
    {
        var code = """
            namespace MyNamespace;

            public interface IService
            {
                // Interface member with default implementation has public default accessibility
                bool IsValid(int? taskId, string? stageId) =>
                    taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase);
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var occurrences = _extractor.ExtractPredicates(tree, "IService.cs", _settings);

        Assert.Single(occurrences);
        var occ = occurrences[0];
        Assert.True(occ.IsHelperDeclaration);
        Assert.NotNull(occ.HelperInfo);
        Assert.False(occ.HelperInfo.IsPrivate);
        Assert.Equal("public", occ.HelperInfo.Accessibility);
    }

    [Fact]
    public void ExtractPredicates_DirectlyNegatedCondition_SetsDeMorganDualNegatedSignature()
    {
        var code = """
            namespace MyNamespace;

            public class Service
            {
                public void Run(int? taskId, string? stageId)
                {
                    if (!(taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, "Ideation", StringComparison.OrdinalIgnoreCase)))
                    {
                        Process();
                    }
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var occurrences = _extractor.ExtractPredicates(tree, "Service.cs", _settings);

        Assert.Single(occurrences);
        var occ = occurrences[0];
        Assert.True(occ.IsNegatedAtCallSite);

        // NormalizedSignature has inner positive form
        Assert.Contains("&&", occ.NormalizedSignature);
        Assert.DoesNotContain("||", occ.NormalizedSignature);

        // NegatedNormalizedSignature has De Morgan dual form
        Assert.NotNull(occ.NegatedNormalizedSignature);
        Assert.Contains("||", occ.NegatedNormalizedSignature);
        Assert.DoesNotContain("&&", occ.NegatedNormalizedSignature);
    }
}
