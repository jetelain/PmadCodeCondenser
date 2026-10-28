using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Patterns.Rules;

/// <summary>
/// Detects ternary expressions checking for null (e.g., x != null ? x : fallback or x == null ? fallback : x)
/// and condenses them to null-coalescing expressions (x ?? fallback).
/// </summary>
public sealed class TernaryNullCoalescingRule : CondensationPatternRuleBase<ConditionalExpressionSyntax>
{
    private static readonly HashSet<SyntaxKind> ApplicableNodeKinds = [SyntaxKind.ConditionalExpression];

    /// <inheritdoc />
    public override string RuleId => "PCC409";

    /// <inheritdoc />
    public override string Name => "Use Null-Coalescing Operator";

    /// <inheritdoc />
    public override string Description =>
        "Replaces verbose ternary null checks (e.g., x != null ? x : fallback) with the null-coalescing operator '??'.";

    /// <inheritdoc />
    public override PatternCategory Category => PatternCategory.Language;

    /// <inheritdoc />
    public override Version? MinDotNetVersion => null;

    /// <inheritdoc />
    public override bool CanAutoFix => true;

    /// <inheritdoc />
    public override IReadOnlySet<SyntaxKind> ApplicableKinds => ApplicableNodeKinds;

    /// <inheritdoc />
    protected override bool TryMatchNode(
        ConditionalExpressionSyntax conditional,
        CondensationContext context,
        [NotNullWhen(true)] out CondensationMatch? match)
    {
        match = null;

        ExpressionSyntax? target = null;
        ExpressionSyntax? fallback = null;

        var condition = StripParens(conditional.Condition);
        var whenTrue = StripParens(conditional.WhenTrue);
        var whenFalse = StripParens(conditional.WhenFalse);

        // Form 1: x != null ? x : fallback
        if (TryExtractNotNullTarget(condition, out var notNullTarget) &&
            AreEquivalent(notNullTarget, whenTrue))
        {
            target = notNullTarget;
            fallback = whenFalse;
        }
        // Form 2: x == null ? fallback : x
        else if (TryExtractNullTarget(condition, out var nullTarget) &&
                 AreEquivalent(nullTarget, whenFalse))
        {
            target = nullTarget;
            fallback = whenTrue;
        }

        if (target == null || fallback == null)
        {
            return false;
        }

        var cleanTarget = StripParens(target).WithoutTrivia();
        var innerFallback = StripParens(fallback).WithoutTrivia();
        ExpressionSyntax cleanFallback = innerFallback is ConditionalExpressionSyntax or BinaryExpressionSyntax or AssignmentExpressionSyntax
            ? SyntaxFactory.ParenthesizedExpression(innerFallback)
            : innerFallback;

        var replacementNode = SyntaxFactory.BinaryExpression(
            SyntaxKind.CoalesceExpression,
            cleanTarget.WithTrailingTrivia(SyntaxFactory.Space),
            cleanFallback.WithLeadingTrivia(SyntaxFactory.Space))
            .WithLeadingTrivia(conditional.GetLeadingTrivia())
            .WithTrailingTrivia(conditional.GetTrailingTrivia());

        var suggestedCode = $"{cleanTarget} ?? {cleanFallback}";

        match = new CondensationMatch
        {
            Node = conditional,
            SuggestedCode = suggestedCode,
            ReplacementNode = replacementNode,
            Description = "Replaces verbose ternary null check with the null-coalescing operator '??'."
        };

        return true;
    }

    /// <inheritdoc />
    protected override SyntaxNode RewriteNode(ConditionalExpressionSyntax node, CondensationMatch match)
    {
        return match.ReplacementNode ?? node;
    }

    private static bool AreEquivalent(ExpressionSyntax a, ExpressionSyntax b)
    {
        return StripParens(a).ToString().Equals(StripParens(b).ToString(), StringComparison.Ordinal);
    }

    private static ExpressionSyntax StripParens(ExpressionSyntax expr)
    {
        while (expr is ParenthesizedExpressionSyntax parenthesized)
        {
            expr = parenthesized.Expression;
        }

        return expr;
    }

    private static bool TryExtractNotNullTarget(ExpressionSyntax expr, [NotNullWhen(true)] out ExpressionSyntax? target)
    {
        target = null;

        if (expr is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.NotEqualsExpression))
        {
            if (binary.Right.IsKind(SyntaxKind.NullLiteralExpression))
            {
                target = binary.Left;
                return true;
            }

            if (binary.Left.IsKind(SyntaxKind.NullLiteralExpression))
            {
                target = binary.Right;
                return true;
            }
        }
        else if (expr is IsPatternExpressionSyntax isPattern &&
                 isPattern.Pattern is UnaryPatternSyntax unary &&
                 unary.IsKind(SyntaxKind.NotPattern) &&
                 unary.Pattern is ConstantPatternSyntax constPattern &&
                 constPattern.Expression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            target = isPattern.Expression;
            return true;
        }

        return false;
    }

    private static bool TryExtractNullTarget(ExpressionSyntax expr, [NotNullWhen(true)] out ExpressionSyntax? target)
    {
        target = null;

        if (expr is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.EqualsExpression))
        {
            if (binary.Right.IsKind(SyntaxKind.NullLiteralExpression))
            {
                target = binary.Left;
                return true;
            }

            if (binary.Left.IsKind(SyntaxKind.NullLiteralExpression))
            {
                target = binary.Right;
                return true;
            }
        }
        else if (expr is IsPatternExpressionSyntax isPattern &&
                 isPattern.Pattern is ConstantPatternSyntax constPattern &&
                 constPattern.Expression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            target = isPattern.Expression;
            return true;
        }

        return false;
    }
}
