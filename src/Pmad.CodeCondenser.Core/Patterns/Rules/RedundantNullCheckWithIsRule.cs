using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Patterns.Rules;

/// <summary>
/// Detects redundant null checks before or after an 'is' pattern (e.g., x != null &amp;&amp; x is Type)
/// and condenses them to just the 'is' pattern.
/// </summary>
public sealed class RedundantNullCheckWithIsRule : CondensationPatternRuleBase<BinaryExpressionSyntax>
{
    private static readonly HashSet<SyntaxKind> ApplicableNodeKinds = [SyntaxKind.LogicalAndExpression];

    /// <inheritdoc />
    public override string RuleId => "PCC408";

    /// <inheritdoc />
    public override string Name => "Simplify Redundant Null Check Before Is Pattern";

    /// <inheritdoc />
    public override string Description =>
        "Replaces redundant x != null && x is Type with x is Type because 'is' patterns are inherently null-safe.";

    /// <inheritdoc />
    public override PatternCategory Category => PatternCategory.Predicate;

    /// <inheritdoc />
    public override Version? MinDotNetVersion => null;

    /// <inheritdoc />
    public override bool CanAutoFix => true;

    /// <inheritdoc />
    public override IReadOnlySet<SyntaxKind> ApplicableKinds => ApplicableNodeKinds;

    /// <inheritdoc />
    protected override bool TryMatchNode(
        BinaryExpressionSyntax binary,
        CondensationContext context,
        [NotNullWhen(true)] out CondensationMatch? match)
    {
        match = null;

        ExpressionSyntax? targetA = null;
        ExpressionSyntax? targetB = null;
        ExpressionSyntax? retainedExpression = null;

        // Case 1: Left is null check, Right is 'is' check
        if (TryExtractNotNullTarget(binary.Left, out targetA) &&
            TryExtractIsPatternTarget(binary.Right, out targetB))
        {
            retainedExpression = StripParens(binary.Right);
        }
        // Case 2: Left is 'is' check, Right is null check
        else if (TryExtractIsPatternTarget(binary.Left, out targetA) &&
                 TryExtractNotNullTarget(binary.Right, out targetB))
        {
            retainedExpression = StripParens(binary.Left);
        }

        if (targetA == null || targetB == null || retainedExpression == null)
        {
            return false;
        }

        if (!StripParens(targetA).ToString().Equals(StripParens(targetB).ToString(), StringComparison.Ordinal))
        {
            return false;
        }

        var replacementNode = retainedExpression
            .WithLeadingTrivia(binary.GetLeadingTrivia())
            .WithTrailingTrivia(binary.GetTrailingTrivia());

        match = new CondensationMatch
        {
            Node = binary,
            SuggestedCode = retainedExpression.ToString(),
            ReplacementNode = replacementNode,
            Description = "Removes redundant null check because the C# 'is' expression already checks for null."
        };

        return true;
    }

    /// <inheritdoc />
    protected override SyntaxNode RewriteNode(BinaryExpressionSyntax node, CondensationMatch match)
    {
        return match.ReplacementNode ?? node;
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
        expr = StripParens(expr);

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

    private static bool TryExtractIsPatternTarget(ExpressionSyntax expr, [NotNullWhen(true)] out ExpressionSyntax? target)
    {
        target = null;
        expr = StripParens(expr);

        if (expr is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.IsExpression))
        {
            target = binary.Left;
            return true;
        }

        if (expr is IsPatternExpressionSyntax isPattern)
        {
            var pattern = isPattern.Pattern;
            if (pattern is DeclarationPatternSyntax or TypePatternSyntax or RecursivePatternSyntax)
            {
                target = isPattern.Expression;
                return true;
            }
        }

        return false;
    }
}
