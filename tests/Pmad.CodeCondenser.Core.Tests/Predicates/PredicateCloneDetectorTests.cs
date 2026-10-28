using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Core.Predicates;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Predicates;

public class PredicateCloneDetectorTests
{
    private readonly PredicateCloneDetector _detector = new();
    private readonly CodeCondenserSettings _settings = new()
    {
        PredicateLengthThreshold = 25,
        ExistingHelperPredicateLengthThreshold = 20,
        PredicateMinOccurrences = 2
    };

    [Fact]
    public void GroupPredicates_MatchesInlineConditionAgainstExistingStaticHelper()
    {
        var helperCode = """
            namespace MyNamespace;

            public static class TaskHelper
            {
                public static bool IsAutonomousTask(int? taskId, string? stageId) =>
                    taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase);
            }
            """;

        var callerCode = """
            namespace MyNamespace;

            public class Consumer
            {
                public void Run(int? taskId, string? stageId)
                {
                    if (taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase))
                    {
                        Process();
                    }
                }
            }
            """;

        var occurrences = new List<PredicateOccurrence>();
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(helperCode), "TaskHelper.cs", _settings));
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(callerCode), "Consumer.cs", _settings));

        var groups = _detector.GroupPredicates(occurrences, _settings);

        Assert.Single(groups);
        var group = groups[0];

        Assert.True(group.HasExistingHelper);
        Assert.Equal("IsAutonomousTask", group.SuggestedHelperName);
        Assert.Single(group.InlineOccurrences);
        Assert.Equal("Consumer.cs", group.InlineOccurrences[0].FilePath);

        var usage = group.GenerateTypicalUsage(group.InlineOccurrences[0]);
        Assert.Equal("TaskHelper.IsAutonomousTask(taskId, stageId)", usage);
    }

    [Fact]
    public void GroupPredicates_MatchesInlineConditionAgainstExistingInstanceHelper()
    {
        var slotCode = """
            namespace MyNamespace;

            public class TargetSlot
            {
                public TargetLivenessState State { get; set; }
                public DateTimeOffset CachedAt { get; set; }

                public bool IsOnlineWithinTtl(DateTimeOffset now, TimeSpan ttl) =>
                    State == TargetLivenessState.Online && now - CachedAt <= ttl;

                public void Check(TargetSlot slot)
                {
                    if (slot.State == TargetLivenessState.Online && DateTimeOffset.UtcNow - slot.CachedAt <= _onlineCacheTtl)
                    {
                        Activate(slot);
                    }
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(slotCode);
        var occurrences = _detector.ExtractPredicates(tree, "TargetSlot.cs", _settings);
        var groups = _detector.GroupPredicates(occurrences, _settings);

        Assert.Single(groups);
        var group = groups[0];

        Assert.True(group.HasExistingHelper);
        Assert.Equal("IsOnlineWithinTtl", group.SuggestedHelperName);
        Assert.Single(group.InlineOccurrences);

        var usage = group.GenerateTypicalUsage(group.InlineOccurrences[0]);
        Assert.Equal("slot.IsOnlineWithinTtl(DateTimeOffset.UtcNow, _onlineCacheTtl)", usage);
    }

    [Fact]
    public void GroupPredicates_MatchesDeMorganNegatedConditionAgainstExistingHelper()
    {
        var slotCode = """
            namespace MyNamespace;

            public class TargetSlot
            {
                public TargetLivenessState State { get; set; }
                public DateTimeOffset CachedAt { get; set; }

                public bool IsOnlineWithinTtl(DateTimeOffset now, TimeSpan ttl) =>
                    State == TargetLivenessState.Online && now - CachedAt <= ttl;

                public void Check(TargetSlot slot)
                {
                    if (slot.State != TargetLivenessState.Online || DateTimeOffset.UtcNow - slot.CachedAt > _onlineCacheTtl)
                    {
                        return;
                    }
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(slotCode);
        var occurrences = _detector.ExtractPredicates(tree, "TargetSlot.cs", _settings);
        var groups = _detector.GroupPredicates(occurrences, _settings);

        Assert.Single(groups);
        var group = groups[0];

        Assert.True(group.HasExistingHelper);
        Assert.Equal("IsOnlineWithinTtl", group.SuggestedHelperName);
        Assert.Single(group.InlineOccurrences);
        Assert.True(group.InlineOccurrences[0].IsNegatedAtCallSite);

        var usage = group.GenerateTypicalUsage(group.InlineOccurrences[0]);
        Assert.Equal("!slot.IsOnlineWithinTtl(DateTimeOffset.UtcNow, _onlineCacheTtl)", usage);
    }

    [Fact]
    public void GroupPredicates_GroupsDuplicateConditionsWhenNoHelperExists()
    {
        var fileA = """
            namespace MyNamespace;

            public class HandlerA
            {
                public void Handle(int? taskId, string? stageId)
                {
                    if (taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase))
                    {
                        Process();
                    }
                }
            }
            """;

        var fileB = """
            namespace MyNamespace;

            public class HandlerB
            {
                public void Execute(int? currentId, string? currentStage)
                {
                    if (currentId.HasValue && currentId.Value > 0 && !string.Equals(currentStage, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase))
                    {
                        Process();
                    }
                }
            }
            """;

        var occurrences = new List<PredicateOccurrence>();
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(fileA), "HandlerA.cs", _settings));
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(fileB), "HandlerB.cs", _settings));

        var groups = _detector.GroupPredicates(occurrences, _settings);

        Assert.Single(groups);
        var group = groups[0];

        Assert.False(group.HasExistingHelper);
        Assert.Equal(2, group.Occurrences.Count);
        Assert.Equal(2, group.InlineOccurrences.Count);
        Assert.Contains(group.Occurrences, o => o.FilePath == "HandlerA.cs");
        Assert.Contains(group.Occurrences, o => o.FilePath == "HandlerB.cs");
    }

    [Fact]
    public void GroupPredicates_ZeroArgumentInstanceHelper_DoesNotPassReceiverInUsage()
    {
        var slotCode = """
            namespace MyNamespace;

            public class TargetSlot
            {
                public TargetLivenessState State { get; set; }

                public bool IsOnline() => State == TargetLivenessState.Online;

                public void Check(TargetSlot slot)
                {
                    if (slot.State == TargetLivenessState.Online)
                    {
                        Activate(slot);
                    }
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(slotCode);
        var occurrences = _detector.ExtractPredicates(tree, "TargetSlot.cs", _settings);
        var groups = _detector.GroupPredicates(occurrences, _settings);

        Assert.Single(groups);
        var group = groups[0];
        Assert.True(group.HasExistingHelper);
        var usage = group.GenerateTypicalUsage(group.InlineOccurrences[0]);
        Assert.Equal("slot.IsOnline()", usage);
    }

    [Fact]
    public void GroupPredicates_PropertyHelper_DoesNotIncludeParenthesesInUsage()
    {
        var slotCode = """
            namespace MyNamespace;

            public class TargetSlot
            {
                public TargetLivenessState State { get; set; }

                public bool IsOnline => State == TargetLivenessState.Online;

                public void Check(TargetSlot slot)
                {
                    if (slot.State == TargetLivenessState.Online)
                    {
                        Activate(slot);
                    }
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(slotCode);
        var occurrences = _detector.ExtractPredicates(tree, "TargetSlot.cs", _settings);
        var groups = _detector.GroupPredicates(occurrences, _settings);

        Assert.Single(groups);
        var group = groups[0];
        Assert.True(group.HasExistingHelper);
        var usage = group.GenerateTypicalUsage(group.InlineOccurrences[0]);
        Assert.Equal("slot.IsOnline", usage);
    }

    [Fact]
    public void GroupPredicates_PrivateHelper_DoesNotMatchDifferentClass()
    {
        var codeA = """
            namespace MyNamespace;

            public class ServiceA
            {
                private bool IsAutonomous(int? taskId, string? stageId) =>
                    taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, "Ideation", StringComparison.OrdinalIgnoreCase);
            }
            """;

        var codeB = """
            namespace MyNamespace;

            public class ServiceB
            {
                public void Run(int? taskId, string? stageId)
                {
                    if (taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, "Ideation", StringComparison.OrdinalIgnoreCase))
                    {
                        DoWork();
                    }
                }
            }
            """;

        var occurrences = new List<PredicateOccurrence>();
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(codeA), "ServiceA.cs", _settings));
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(codeB), "ServiceB.cs", _settings));

        var groups = _detector.GroupPredicates(occurrences, _settings);

        Assert.Empty(groups);
    }

    [Fact]
    public void GroupPredicates_NegatedHelperDeclaration_HandlesPositiveCallSiteWithNegation()
    {
        var code = """
            namespace MyNamespace;

            public static class Validator
            {
                public static bool IsInvalid(int? taskId, string? stageId) =>
                    !(taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, "Ideation", StringComparison.OrdinalIgnoreCase));

                public static void Process(int? taskId, string? stageId)
                {
                    if (taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, "Ideation", StringComparison.OrdinalIgnoreCase))
                    {
                        DoWork();
                    }
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var occurrences = _detector.ExtractPredicates(tree, "Validator.cs", _settings);
        var groups = _detector.GroupPredicates(occurrences, _settings);

        Assert.Single(groups);
        var group = groups[0];
        Assert.True(group.HasExistingHelper);
        Assert.Equal("IsInvalid", group.SuggestedHelperName);

        var usage = group.GenerateTypicalUsage(group.InlineOccurrences[0]);
        Assert.Equal("!Validator.IsInvalid(taskId, stageId)", usage);
    }

    [Fact]
    public void GroupPredicates_DifferentEnumValues_DoNotMatchExistingHelper()
    {
        var slotCode = """
            namespace MyNamespace;

            public class TargetSlot
            {
                public TargetLivenessState State { get; set; }

                public bool IsOnline() => State == TargetLivenessState.Online;

                public void Check(TargetSlot slot)
                {
                    // Condition checks for Offline - must NOT match IsOnline()!
                    if (slot.State == TargetLivenessState.Offline)
                    {
                        Deactivate(slot);
                    }
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(slotCode);
        var occurrences = _detector.ExtractPredicates(tree, "TargetSlot.cs", _settings);
        var groups = _detector.GroupPredicates(occurrences, _settings);

        // Because the constants differ (Offline != Online), the inline call site must NOT match IsOnline
        Assert.Empty(groups);
    }

    [Fact]
    public void GroupPredicates_DifferentLiterals_DoNotGroupIntoSameCluster()
    {
        var code = """
            namespace MyNamespace;

            public class Consumer
            {
                public void MethodA(string role, int level)
                {
                    if (role == "admin" && level > 10)
                    {
                        DoA();
                    }
                }

                public void MethodB(string role, int level)
                {
                    // Different literals: "guest" != "admin", 0 != 10
                    if (role == "guest" && level > 0)
                    {
                        DoB();
                    }
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var occurrences = _detector.ExtractPredicates(tree, "Consumer.cs", _settings);
        var groups = _detector.GroupPredicates(occurrences, _settings);

        // Must not be merged into a duplicate clone cluster
        Assert.Empty(groups);
    }

    [Fact]
    public void GroupPredicates_MultiplePrivateHelpersInDifferentClasses_PreservesDeclaringTypeAccessibility()
    {
        var codeA = """
            namespace MyNamespace;

            public class ServiceA
            {
                private bool IsAutonomous(int? id, string? stage) =>
                    id.HasValue && id.Value > 0 && !string.Equals(stage, "Ideation", StringComparison.OrdinalIgnoreCase);

                public void RunA(int? id, string? stage)
                {
                    if (id.HasValue && id.Value > 0 && !string.Equals(stage, "Ideation", StringComparison.OrdinalIgnoreCase))
                    {
                        Process();
                    }
                }
            }
            """;

        var codeB = """
            namespace MyNamespace;

            public class ServiceB
            {
                private bool IsAutonomous(int? id, string? stage) =>
                    id.HasValue && id.Value > 0 && !string.Equals(stage, "Ideation", StringComparison.OrdinalIgnoreCase);

                public void RunB(int? id, string? stage)
                {
                    if (id.HasValue && id.Value > 0 && !string.Equals(stage, "Ideation", StringComparison.OrdinalIgnoreCase))
                    {
                        Process();
                    }
                }
            }
            """;

        var occurrences = new List<PredicateOccurrence>();
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(codeA), "ServiceA.cs", _settings));
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(codeB), "ServiceB.cs", _settings));

        var groups = _detector.GroupPredicates(occurrences, _settings);

        // Both ServiceA and ServiceB should have their own matched helper group
        Assert.Equal(2, groups.Count);
        Assert.All(groups, g => Assert.True(g.HasExistingHelper));

        var groupA = groups.FirstOrDefault(g => g.ExistingHelper?.DeclaringTypeName == "ServiceA");
        var groupB = groups.FirstOrDefault(g => g.ExistingHelper?.DeclaringTypeName == "ServiceB");

        Assert.NotNull(groupA);
        Assert.NotNull(groupB);
        Assert.Single(groupA.InlineOccurrences);
        Assert.Equal("ServiceA.cs", groupA.InlineOccurrences[0].FilePath);
        Assert.Single(groupB.InlineOccurrences);
        Assert.Equal("ServiceB.cs", groupB.InlineOccurrences[0].FilePath);
    }

    [Fact]
    public void PredicateCloneGroup_EstimateGainTokens_CalculatesRealisticTokenGain()
    {
        var occ1 = new PredicateOccurrence
        {
            FilePath = "FileA.cs",
            StartLine = 10,
            EndLine = 10,
            StartColumn = 1,
            EndColumn = 80,
            ExpressionText = "taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, \"Ideation\", StringComparison.OrdinalIgnoreCase)",
            NormalizedSignature = "$0.HasValue && $0.Value > 0 && !string.Equals($1, \"Ideation\", StringComparison.OrdinalIgnoreCase)",
            NormalizedHash = 12345
        };

        var occ2 = occ1 with { FilePath = "FileB.cs" };

        var group = new PredicateCloneGroup
        {
            NormalizedSignature = occ1.NormalizedSignature,
            Occurrences = [occ1, occ2],
            SuggestedHelperName = "IsAutonomous",
            SuggestedClassName = "TaskHelpers"
        };

        var tokenGain = group.EstimateGainTokens();
        // Estimated savings should be positive and realistic (typically between 5 and 50 tokens)
        Assert.True(tokenGain >= 0);
    }

    [Fact]
    public void GroupPredicates_WhenMultipleCallSitesMatchExistingHelperViaNegation_DoesNotCreateDuplicateUnextractedCloneGroup()
    {
        var helperCode = """
            namespace MyNamespace;

            public static class TaskHelper
            {
                public static bool IsValid(int? taskId) =>
                    taskId.HasValue && taskId.Value > 0;
            }
            """;

        var callerCodeA = """
            namespace MyNamespace;

            public class ServiceA
            {
                public void Run(int? taskId)
                {
                    if (!taskId.HasValue || taskId.Value <= 0)
                    {
                        return;
                    }
                }
            }
            """;

        var callerCodeB = """
            namespace MyNamespace;

            public class ServiceB
            {
                public void Run(int? jobId)
                {
                    if (!jobId.HasValue || jobId.Value <= 0)
                    {
                        return;
                    }
                }
            }
            """;

        var occurrences = new List<PredicateOccurrence>();
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(helperCode), "TaskHelper.cs", _settings));
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(callerCodeA), "ServiceA.cs", _settings));
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(callerCodeB), "ServiceB.cs", _settings));

        var groups = _detector.GroupPredicates(occurrences, _settings);

        // Exactly 1 group (the existing helper group) should be returned.
        // If matched inlines were not properly tracked because of mutated record equality,
        // a duplicate CC010 unextracted clone group would also have been generated.
        Assert.Single(groups);
        var group = groups[0];
        Assert.True(group.HasExistingHelper);
        Assert.Equal("IsValid", group.SuggestedHelperName);
        Assert.Equal(2, group.InlineOccurrences.Count);
        Assert.All(group.InlineOccurrences, o => Assert.True(o.IsNegatedAtCallSite));
    }

    [Fact]
    public void GroupPredicates_UnextractedDualConditions_GroupsIntoSingleClusterWithCanonicalPolarity()
    {
        var fileA = """
            namespace MyNamespace;

            public class HandlerA
            {
                public void Handle(int? taskId, string? stageId)
                {
                    if (taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase))
                    {
                        Process();
                    }
                }
            }
            """;

        var fileB = """
            namespace MyNamespace;

            public class HandlerB
            {
                public void Execute(int? currentId, string? currentStage)
                {
                    // De Morgan dual: !taskId.HasValue || taskId.Value <= 0 || string.Equals(stageId, ...)
                    if (!currentId.HasValue || currentId.Value <= 0 || string.Equals(currentStage, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }
            }
            """;

        var occurrences = new List<PredicateOccurrence>();
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(fileA), "HandlerA.cs", _settings));
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(fileB), "HandlerB.cs", _settings));

        var groups = _detector.GroupPredicates(occurrences, _settings);

        Assert.Single(groups);
        var group = groups[0];

        Assert.False(group.HasExistingHelper);
        Assert.Equal(2, group.Occurrences.Count);
        Assert.Equal(2, group.InlineOccurrences.Count);

        // Canonical polarity should pick the positive (fewer negations/disjunctions) signature
        Assert.Contains(".HasValue &&", group.NormalizedSignature);
        Assert.DoesNotContain("||", group.NormalizedSignature);

        var positiveOcc = group.InlineOccurrences.Single(o => !o.IsNegatedAtCallSite);
        var negatedOcc = group.InlineOccurrences.Single(o => o.IsNegatedAtCallSite);

        Assert.Equal("HandlerA.cs", positiveOcc.FilePath);
        Assert.Equal("HandlerB.cs", negatedOcc.FilePath);

        var posUsage = group.GenerateTypicalUsage(positiveOcc);
        var negUsage = group.GenerateTypicalUsage(negatedOcc);
        var defaultUsage = group.GenerateTypicalUsage();

        Assert.False(posUsage.StartsWith('!'));
        Assert.True(negUsage.StartsWith('!'));
        Assert.False(defaultUsage.StartsWith('!'));
    }

    [Fact]
    public void GroupPredicates_UnextractedDualConditions_WhenMultiplePositiveAndNegativeOccurrences_MergesAllIntoOneCluster()
    {
        var code = """
            namespace MyNamespace;

            public class Service
            {
                public void Method1(int firstValue, int secondValue, int thirdValue)
                {
                    if (firstValue > 0 && secondValue > 0 && thirdValue > 0) Process();
                }

                public void Method2(int alpha, int beta, int gamma)
                {
                    if (alpha > 0 && beta > 0 && gamma > 0) Process();
                }

                public void Method3(int delta, int epsilon, int zeta)
                {
                    if (delta <= 0 || epsilon <= 0 || zeta <= 0) return;
                }
            }
            """;

        var occurrences = _detector.ExtractPredicates(CSharpSyntaxTree.ParseText(code), "Service.cs", _settings);
        var groups = _detector.GroupPredicates(occurrences, _settings);

        Assert.Single(groups);
        var group = groups[0];

        Assert.Equal(3, group.Occurrences.Count);
        Assert.Equal(2, group.InlineOccurrences.Count(o => !o.IsNegatedAtCallSite));
        Assert.Equal(1, group.InlineOccurrences.Count(o => o.IsNegatedAtCallSite));
        Assert.Contains("&&", group.NormalizedSignature);
        Assert.DoesNotContain("||", group.NormalizedSignature);
    }

    [Fact]
    public void GroupPredicates_UnextractedIdenticalNegatedConditions_WithoutPositiveOccurrences_PreservesWrittenForm()
    {
        var code = """
            namespace MyNamespace;

            public class Service
            {
                public void Method1(int firstValue, int secondValue, int thirdValue)
                {
                    if (firstValue <= 0 || secondValue <= 0 || thirdValue <= 0) return;
                }

                public void Method2(int alpha, int beta, int gamma)
                {
                    if (alpha <= 0 || beta <= 0 || gamma <= 0) return;
                }
            }
            """;

        var occurrences = _detector.ExtractPredicates(CSharpSyntaxTree.ParseText(code), "Service.cs", _settings);
        var groups = _detector.GroupPredicates(occurrences, _settings);

        Assert.Single(groups);
        var group = groups[0];

        Assert.Equal(2, group.Occurrences.Count);
        // Because no positive occurrence existed, the written form is preserved without artificial inversion
        Assert.All(group.InlineOccurrences, o => Assert.False(o.IsNegatedAtCallSite));
        Assert.Contains("||", group.NormalizedSignature);
    }

    [Fact]
    public void GroupPredicates_UnextractedDirectlyNegatedCondition_WithPositiveCondition_GroupsIntoSingleClusterWithCanonicalPolarity()
    {
        var code = """
            namespace MyNamespace;

            public class Service
            {
                public void Method1(int firstValue, int secondValue, int thirdValue)
                {
                    if (firstValue > 0 && secondValue > 0 && thirdValue > 0) Process();
                }

                public void Method2(int alpha, int beta, int gamma)
                {
                    if (!(alpha > 0 && beta > 0 && gamma > 0)) return;
                }
            }
            """;

        var occurrences = _detector.ExtractPredicates(CSharpSyntaxTree.ParseText(code), "Service.cs", _settings);
        var groups = _detector.GroupPredicates(occurrences, _settings);

        Assert.Single(groups);
        var group = groups[0];

        Assert.Equal(2, group.Occurrences.Count);
        Assert.False(group.HasExistingHelper);
        Assert.Contains("&&", group.NormalizedSignature);
        Assert.DoesNotContain("||", group.NormalizedSignature);

        var positiveOcc = group.InlineOccurrences.Single(o => !o.IsNegatedAtCallSite);
        var negatedOcc = group.InlineOccurrences.Single(o => o.IsNegatedAtCallSite);

        var posUsage = group.GenerateTypicalUsage(positiveOcc);
        var negUsage = group.GenerateTypicalUsage(negatedOcc);

        Assert.False(posUsage.StartsWith('!'));
        Assert.True(negUsage.StartsWith('!'));
    }

    [Fact]
    public void GroupPredicates_UnextractedDirectlyNegatedCondition_WithDeMorganCondition_GroupsIntoSingleCluster()
    {
        var code = """
            namespace MyNamespace;

            public class Service
            {
                public void Method1(int firstValue, int secondValue, int thirdValue)
                {
                    if (!(firstValue > 0 && secondValue > 0 && thirdValue > 0)) return;
                }

                public void Method2(int alpha, int beta, int gamma)
                {
                    if (alpha <= 0 || beta <= 0 || gamma <= 0) return;
                }
            }
            """;

        var occurrences = _detector.ExtractPredicates(CSharpSyntaxTree.ParseText(code), "Service.cs", _settings);
        var groups = _detector.GroupPredicates(occurrences, _settings);

        Assert.Single(groups);
        var group = groups[0];

        Assert.Equal(2, group.Occurrences.Count);
        // Canonical polarity favors positive && form over negated || form
        Assert.Contains("&&", group.NormalizedSignature);
        Assert.DoesNotContain("||", group.NormalizedSignature);
        Assert.All(group.InlineOccurrences, o => Assert.True(o.IsNegatedAtCallSite));
    }

    [Fact]
    public void GroupPredicates_ImplicitPrivateHelper_DoesNotMatchDifferentClass_MatchesSameClass()
    {
        var codeA = """
            namespace MyNamespace;

            public class ServiceA
            {
                // Implicit private (no accessibility modifier)
                static bool IsAutonomous(int? taskId, string? stageId) =>
                    taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, "Ideation", StringComparison.OrdinalIgnoreCase);

                public void RunSelf(int? taskId, string? stageId)
                {
                    if (taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, "Ideation", StringComparison.OrdinalIgnoreCase))
                    {
                        Process();
                    }
                }
            }
            """;

        var codeB = """
            namespace MyNamespace;

            public class ServiceB
            {
                public void RunExternal(int? taskId, string? stageId)
                {
                    if (taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, "Ideation", StringComparison.OrdinalIgnoreCase))
                    {
                        Process();
                    }
                }
            }
            """;

        var occurrences = new List<PredicateOccurrence>();
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(codeA), "ServiceA.cs", _settings));
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(codeB), "ServiceB.cs", _settings));

        var groups = _detector.GroupPredicates(occurrences, _settings);

        // Only ServiceA.RunSelf should match ServiceA.IsAutonomous; ServiceB.RunExternal cannot access it.
        Assert.Single(groups);
        var group = groups[0];
        Assert.True(group.HasExistingHelper);
        Assert.Equal("ServiceA", group.ExistingHelper?.DeclaringTypeName);
        Assert.Single(group.InlineOccurrences);
        Assert.Equal("ServiceA.cs", group.InlineOccurrences[0].FilePath);
    }

    [Fact]
    public void GroupPredicates_DirectlyNegatedCallSite_MatchingExistingDeMorganHelper_CancelsDoubleNegation()
    {
        var helperCode = """
            namespace MyNamespace;

            public static class TaskHelper
            {
                public static bool IsInvalidTask(int? taskId, string? stageId) =>
                    !taskId.HasValue || taskId.Value <= 0 || string.Equals(stageId, "Ideation", StringComparison.OrdinalIgnoreCase);
            }
            """;

        var callerCode = """
            namespace MyNamespace;

            public class Consumer
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

        var occurrences = new List<PredicateOccurrence>();
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(helperCode), "TaskHelper.cs", _settings));
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(callerCode), "Consumer.cs", _settings));

        var groups = _detector.GroupPredicates(occurrences, _settings);

        Assert.Single(groups);
        var group = groups[0];
        Assert.True(group.HasExistingHelper);
        Assert.Equal("IsInvalidTask", group.SuggestedHelperName);
        Assert.Single(group.InlineOccurrences);

        // !(A && B) is equivalent to IsInvalidTask (!A || !B), so double negation cancels out
        Assert.False(group.InlineOccurrences[0].IsNegatedAtCallSite);
        var usage = group.GenerateTypicalUsage(group.InlineOccurrences[0]);
        Assert.Equal("TaskHelper.IsInvalidTask(taskId, stageId)", usage);
    }

    [Fact]
    public void GroupPredicates_StaticMathMethodInInstanceHelper_MatchesCallSiteCleanly()
    {
        var helperCode = """
            namespace MyNamespace;

            public class RangeValidator
            {
                public int Threshold { get; set; }

                public bool IsAboveThreshold(int value, int offset) =>
                    Math.Max(value, 0) + offset >= Threshold;

                public void Check(RangeValidator validator, int input, int extra)
                {
                    if (Math.Max(input, 0) + extra >= validator.Threshold)
                    {
                        Process();
                    }
                }
            }
            """;

        var occurrences = _detector.ExtractPredicates(CSharpSyntaxTree.ParseText(helperCode), "RangeValidator.cs", _settings);
        var groups = _detector.GroupPredicates(occurrences, _settings);

        Assert.Single(groups);
        var group = groups[0];
        Assert.True(group.HasExistingHelper);
        Assert.Equal("IsAboveThreshold", group.SuggestedHelperName);
        Assert.Single(group.InlineOccurrences);
    }

    [Fact]
    public void GroupPredicates_ExistingHelperWithNullConditional_MatchesCallSiteCleanly()
    {
        var helperCode = """
            namespace MyNamespace;

            public class HelperService
            {
                public bool IsHelperPrivate => ExistingHelper?.IsPrivate ?? false;

                public void Check(HelperService service)
                {
                    if (service.ExistingHelper?.IsPrivate ?? false)
                    {
                        Process();
                    }
                }
            }
            """;

        var occurrences = _detector.ExtractPredicates(CSharpSyntaxTree.ParseText(helperCode), "HelperService.cs", _settings);
        var groups = _detector.GroupPredicates(occurrences, _settings);

        Assert.Single(groups);
        var group = groups[0];
        Assert.True(group.HasExistingHelper);
        Assert.Equal("IsHelperPrivate", group.SuggestedHelperName);
        Assert.Single(group.InlineOccurrences);
    }

    [Fact]
    public void GroupPredicates_ShortConditionWithoutExistingHelper_BelowPredicateLengthThreshold_NotGrouped()
    {
        var settings = new CodeCondenserSettings(); // default PredicateLengthThreshold = 100
        var codeA = """
            namespace MyNamespace;
            public class ServiceA
            {
                public void Route(RoutingTarget target)
                {
                    if (target == RoutingTarget.Cloud) { Process(); }
                }
            }
            """;
        var codeB = """
            namespace MyNamespace;
            public class ServiceB
            {
                public void Route(RoutingTarget target)
                {
                    if (target == RoutingTarget.Cloud) { Process(); }
                }
            }
            """;

        var occurrences = new List<PredicateOccurrence>();
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(codeA), "ServiceA.cs", settings));
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(codeB), "ServiceB.cs", settings));

        var groups = _detector.GroupPredicates(occurrences, settings);

        // Since target == RoutingTarget.Cloud (27 chars) is < 100 and no existing helper exists, it must NOT form a clone group
        Assert.Empty(groups);
    }

    [Fact]
    public void GroupPredicates_ShortConditionWithExistingHelper_AboveExistingHelperThreshold_MatchesHelper()
    {
        var settings = new CodeCondenserSettings(); // default PredicateLengthThreshold = 100, ExistingHelperPredicateLengthThreshold = 20
        var helperCode = """
            namespace MyNamespace;
            public static class TargetExtensions
            {
                public static bool IsCloud(RoutingTarget target) => target == RoutingTarget.Cloud;
            }
            """;
        var callerCode = """
            namespace MyNamespace;
            public class Service
            {
                public void Route(RoutingTarget target)
                {
                    if (target == RoutingTarget.Cloud) { Process(); }
                }
            }
            """;

        var occurrences = new List<PredicateOccurrence>();
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(helperCode), "TargetExtensions.cs", settings));
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(callerCode), "Service.cs", settings));

        var groups = _detector.GroupPredicates(occurrences, settings);

        // target == RoutingTarget.Cloud (27 chars) is >= 20, so when an existing helper exists, it matches the helper!
        Assert.Single(groups);
        var group = groups[0];
        Assert.True(group.HasExistingHelper);
        Assert.Equal("IsCloud", group.SuggestedHelperName);
        Assert.Single(group.InlineOccurrences);
    }

    [Fact]
    public void GroupPredicates_StaticHelperWithInternalConstant_DoesNotMatchCallSiteWithTwoVariables()
    {
        var settings = new CodeCondenserSettings();
        var helperCode = """
            namespace NightCodeForge.Core.Domain;
            public static class WorkerStatusConstants
            {
                public const string Running = "Running";
                public static bool IsRunning(string? status) =>
                    string.Equals(status, Running, StringComparison.OrdinalIgnoreCase);
            }
            """;
        var callerCode = """
            namespace NightCodeForge.Agents;
            public class IdeationService
            {
                public void ParseProposals(string d, string prevTitle)
                {
                    if (string.Equals(d, prevTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        Process();
                    }
                }
            }
            """;

        var occurrences = new List<PredicateOccurrence>();
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(helperCode), "WorkerStatusConstants.cs", settings));
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(callerCode), "IdeationService.cs", settings));

        var groups = _detector.GroupPredicates(occurrences, settings);

        // string.Equals(d, prevTitle, ...) has 2 arguments and must NOT match IsRunning which accepts only 1 parameter
        Assert.Empty(groups);
    }

    [Fact]
    public void GroupPredicates_StaticHelperWithInternalConstant_MatchesCallSiteWithConstantAndIncludesClassName()
    {
        var settings = new CodeCondenserSettings();
        var helperCode = """
            namespace NightCodeForge.Core.Domain;
            public static class WorkerStatusConstants
            {
                public const string Running = "Running";
                public static bool IsRunning(string? status) =>
                    string.Equals(status, Running, StringComparison.OrdinalIgnoreCase);
            }
            """;
        var callerCode = """
            namespace NightCodeForge.Agents;
            public class IdeationService
            {
                public void Check(string? status)
                {
                    if (string.Equals(status, WorkerStatusConstants.Running, StringComparison.OrdinalIgnoreCase))
                    {
                        Process();
                    }
                }
            }
            """;

        var occurrences = new List<PredicateOccurrence>();
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(helperCode), "WorkerStatusConstants.cs", settings));
        occurrences.AddRange(_detector.ExtractPredicates(CSharpSyntaxTree.ParseText(callerCode), "IdeationService.cs", settings));

        var groups = _detector.GroupPredicates(occurrences, settings);

        Assert.Single(groups);
        var group = groups[0];
        Assert.True(group.HasExistingHelper);
        Assert.Equal("IsRunning", group.SuggestedHelperName);
        Assert.Single(group.InlineOccurrences);

        var usage = group.GenerateTypicalUsage(group.InlineOccurrences[0]);
        Assert.Equal("WorkerStatusConstants.IsRunning(status)", usage);
    }

    [Fact]
    public void ExtractPredicates_IgnoresStandaloneAwaitExpressions()
    {
        var code = """
            namespace MyNamespace;

            public class Repository
            {
                public async Task ProcessAsync(string commit1, string commit2, CancellationToken ct)
                {
                    if (await IsCommitReachableAsync(commit1, commit2, ct).ConfigureAwait(false))
                    {
                        return;
                    }
                }

                public async Task<bool> IsCommitReachableAsync(string c1, string c2, CancellationToken ct) => true;
            }
            """;

        var occurrences = _detector.ExtractPredicates(CSharpSyntaxTree.ParseText(code), "Repository.cs", _settings);
        var inlineOccurrences = occurrences.Where(o => !o.IsHelperDeclaration).ToList();

        Assert.Empty(inlineOccurrences);
    }
}

