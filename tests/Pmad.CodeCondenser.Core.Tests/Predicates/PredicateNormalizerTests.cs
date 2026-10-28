using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Predicates;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Predicates;

public class PredicateNormalizerTests
{
    private static ExpressionSyntax ParseExpression(string code) =>
        SyntaxFactory.ParseExpression(code);

    [Fact]
    public void Normalize_BasicStaticHelperAndCallSite_ProducesMatchingSignatures()
    {
        // Call site: if (taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase))
        var callSiteExpr = ParseExpression("taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase)");

        // Helper declaration body: public static bool IsAutonomousTask(int? taskId, string? stageId) => ...
        var helperExpr = ParseExpression("taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase)");

        var callSiteResult = PredicateNormalizer.Normalize(callSiteExpr);
        var helperResult = PredicateNormalizer.Normalize(helperExpr, knownParameters: ["taskId", "stageId"]);

        Assert.Equal(callSiteResult.Signature, helperResult.Signature);
        Assert.Equal(callSiteResult.Hash, helperResult.Hash);
        Assert.False(callSiteResult.IsNegated);
        Assert.Contains("$0.HasValue", callSiteResult.Signature);
        Assert.Contains("$0.Value > 0", callSiteResult.Signature);
    }

    [Fact]
    public void Normalize_DifferentVariableNames_ProducesMatchingSignatures()
    {
        var expr1 = ParseExpression("taskId.HasValue && taskId.Value > 0 && !string.Equals(stageId, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase)");
        var expr2 = ParseExpression("currentId.HasValue && currentId.Value > 0 && !string.Equals(currentStage, StandardStageIds.Ideation, StringComparison.OrdinalIgnoreCase)");

        var result1 = PredicateNormalizer.Normalize(expr1);
        var result2 = PredicateNormalizer.Normalize(expr2);

        Assert.Equal(result1.Signature, result2.Signature);
        Assert.Equal(result1.Hash, result2.Hash);
    }

    [Fact]
    public void Normalize_DifferentConstants_ProduceDifferentSignatures()
    {
        // Different enum values must NOT produce the same signature
        var onlineExpr = ParseExpression("slot.State == TargetLivenessState.Online");
        var offlineExpr = ParseExpression("slot.State == TargetLivenessState.Offline");

        var onlineResult = PredicateNormalizer.Normalize(onlineExpr, explicitReceiver: "slot");
        var offlineResult = PredicateNormalizer.Normalize(offlineExpr, explicitReceiver: "slot");

        Assert.NotEqual(onlineResult.Signature, offlineResult.Signature);
        Assert.NotEqual(onlineResult.Hash, offlineResult.Hash);
        Assert.Contains("TargetLivenessState.Online", onlineResult.Signature);
        Assert.Contains("TargetLivenessState.Offline", offlineResult.Signature);

        // Different string literals must NOT produce the same signature
        var adminExpr = ParseExpression("role == \"admin\"");
        var guestExpr = ParseExpression("role == \"guest\"");
        Assert.NotEqual(PredicateNormalizer.Normalize(adminExpr).Signature, PredicateNormalizer.Normalize(guestExpr).Signature);

        // Different numeric literals must NOT produce the same signature
        var zeroExpr = ParseExpression("x > 0");
        var hundredExpr = ParseExpression("x > 100");
        Assert.NotEqual(PredicateNormalizer.Normalize(zeroExpr).Signature, PredicateNormalizer.Normalize(hundredExpr).Signature);
    }

    [Fact]
    public void Normalize_InstanceHelperAndCallSiteWithReceiver_ProducesMatchingSignatures()
    {
        // Helper declared in class Slot:
        // public bool IsOnlineWithinTtl(DateTimeOffset now, TimeSpan ttl) => State == TargetLivenessState.Online && now - CachedAt <= ttl;
        var helperExpr = ParseExpression("State == TargetLivenessState.Online && now - CachedAt <= ttl");
        var helperResult = PredicateNormalizer.Normalize(
            helperExpr,
            knownParameters: ["now", "ttl"],
            isInstanceMemberContext: true);

        // Call site:
        // if (slot.State == TargetLivenessState.Online && DateTimeOffset.UtcNow - slot.CachedAt <= _onlineCacheTtl)
        var callSiteExpr = ParseExpression("slot.State == TargetLivenessState.Online && DateTimeOffset.UtcNow - slot.CachedAt <= _onlineCacheTtl");
        var callSiteResult = PredicateNormalizer.Normalize(callSiteExpr, explicitReceiver: "slot");

        Assert.Equal(helperResult.Signature, callSiteResult.Signature);
        Assert.Equal(helperResult.Hash, callSiteResult.Hash);
        Assert.Equal("slot", callSiteResult.ReceiverExpression);
        Assert.Contains("$this.State == TargetLivenessState.Online", callSiteResult.Signature);
        Assert.Contains("$this.CachedAt", callSiteResult.Signature);
    }

    [Fact]
    public void Normalize_DeMorganNegatedCondition_MatchesPositiveHelper()
    {
        // Helper declared in class Slot:
        // public bool IsOnlineWithinTtl(DateTimeOffset now, TimeSpan ttl) => State == TargetLivenessState.Online && now - CachedAt <= ttl;
        var helperExpr = ParseExpression("State == TargetLivenessState.Online && now - CachedAt <= ttl");
        var helperResult = PredicateNormalizer.Normalize(
            helperExpr,
            knownParameters: ["now", "ttl"],
            isInstanceMemberContext: true);

        // Call site with De Morgan negation:
        // if (slot.State != TargetLivenessState.Online || DateTimeOffset.UtcNow - slot.CachedAt > _onlineCacheTtl)
        var negatedCallSiteExpr = ParseExpression("slot.State != TargetLivenessState.Online || DateTimeOffset.UtcNow - slot.CachedAt > _onlineCacheTtl");
        var invertedResult = PredicateNormalizer.NormalizeNegated(negatedCallSiteExpr, explicitReceiver: "slot");

        Assert.Equal(helperResult.Signature, invertedResult.Signature);
        Assert.Equal(helperResult.Hash, invertedResult.Hash);
        Assert.True(invertedResult.IsNegated);
    }

    [Fact]
    public void Normalize_ExplicitLogicalNot_MarksAsNegated()
    {
        var expr = ParseExpression("!(taskId.HasValue && taskId.Value > 0)");
        var result = PredicateNormalizer.Normalize(expr);

        Assert.True(result.IsNegated);
        Assert.Contains("$0.HasValue", result.Signature);
    }

    [Fact]
    public void InvertCondition_InvertsRelationalOperatorsCorrectly()
    {
        var expr = ParseExpression("a == b && c > d && e <= f");
        var inverted = PredicateNormalizer.InvertCondition(expr);

        var invertedStr = inverted.NormalizeWhitespace("", "").ToFullString();
        Assert.Contains("a != b", invertedStr);
        Assert.Contains("||", invertedStr);
        Assert.Contains("c <= d", invertedStr);
        Assert.Contains("e > f", invertedStr);
    }

    [Fact]
    public void Normalize_PascalCasePropertyChain_DoesNotFoldToConst()
    {
        var expr = ParseExpression("order.Customer.Priority == 1");
        var result = PredicateNormalizer.Normalize(expr);

        Assert.Contains("$0.Customer.Priority == 1", result.Signature);
        Assert.Single(result.ArgumentExpressions);
        Assert.Equal("order", result.ArgumentExpressions[0]);
    }

    [Fact]
    public void InvertCondition_CompoundOrInsideAnd_ParenthesizesCorrectly()
    {
        var expr = ParseExpression("a || (b && c)");
        var inverted = PredicateNormalizer.InvertCondition(expr);

        var invertedStr = inverted.NormalizeWhitespace("", "").ToFullString();
        Assert.Equal("!a && (!b || !c)", invertedStr);
    }

    [Fact]
    public void InvertCondition_NegatedAnd_InvertsToInnerAnd()
    {
        var expr = ParseExpression("!(a && b)");
        var inverted = PredicateNormalizer.InvertCondition(expr);

        var invertedStr = inverted.NormalizeWhitespace("", "").ToFullString();
        Assert.Equal("a && b", invertedStr);
    }

    [Fact]
    public void InvertCondition_NegatedOr_InvertsToInnerOr()
    {
        var expr = ParseExpression("!(a || b)");
        var inverted = PredicateNormalizer.InvertCondition(expr);

        var invertedStr = inverted.NormalizeWhitespace("", "").ToFullString();
        Assert.Equal("a || b", invertedStr);
    }

    [Fact]
    public void NormalizeNegated_NegatedCompoundCondition_ProducesDualSignature()
    {
        var directExpr = ParseExpression("a && b");
        var negatedExpr = ParseExpression("!(a && b)");

        var directResult = PredicateNormalizer.Normalize(directExpr);
        var negatedInvertedResult = PredicateNormalizer.NormalizeNegated(negatedExpr);

        Assert.Equal(directResult.Signature, negatedInvertedResult.Signature);
        Assert.True(negatedInvertedResult.IsNegated);
    }
}
