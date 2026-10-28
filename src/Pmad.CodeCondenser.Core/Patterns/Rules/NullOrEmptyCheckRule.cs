using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Patterns.Rules;

/// <summary>
/// Detects verbose null-or-empty checks such as s == null || s.Length == 0 or s != null &amp;&amp; s.Length &gt; 0
/// and condenses them to string.IsNullOrEmpty(s) or !string.IsNullOrEmpty(s).
/// </summary>
public sealed class NullOrEmptyCheckRule : CondensationPatternRuleBase<BinaryExpressionSyntax>
{
    private static readonly HashSet<SyntaxKind> ApplicableNodeKinds =
    [
        SyntaxKind.LogicalOrExpression,
        SyntaxKind.LogicalAndExpression
    ];

    /// <inheritdoc />
    public override string RuleId => "PCC410";

    /// <inheritdoc />
    public override string Name => "Use String.IsNullOrEmpty";

    /// <inheritdoc />
    public override string Description =>
        "Replaces verbose null or empty checks (e.g., s == null || s.Length == 0) with string.IsNullOrEmpty(s).";

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

        bool isOr = binary.IsKind(SyntaxKind.LogicalOrExpression);
        bool isAnd = binary.IsKind(SyntaxKind.LogicalAndExpression);

        if (!isOr && !isAnd)
        {
            return false;
        }

        ExpressionSyntax? target = null;
        bool isPositive; // true: string.IsNullOrEmpty(s), false: !string.IsNullOrEmpty(s)

        if (isOr)
        {
            // s == null || s.Length == 0  or  s == null || s == ""
            if (!TryExtractNullTarget(binary.Left, out var nullTarget) ||
                !TryExtractEmptyTarget(binary.Right, out var emptyTarget) ||
                !AreEquivalent(nullTarget, emptyTarget))
            {
                return false;
            }

            target = nullTarget;
            isPositive = true;
        }
        else
        {
            // s != null && s.Length > 0  or  s != null && s != ""
            if (!TryExtractNotNullTarget(binary.Left, out var notNullTarget) ||
                !TryExtractNonEmptyTarget(binary.Right, out var nonEmptyTarget) ||
                !AreEquivalent(notNullTarget, nonEmptyTarget))
            {
                return false;
            }

            target = notNullTarget;
            isPositive = false;
        }

        var cleanTarget = StripParens(target).WithoutTrivia();

        var invocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                SyntaxFactory.IdentifierName("IsNullOrEmpty")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(cleanTarget))));

        ExpressionSyntax replacementNode = isPositive
            ? invocation
            : SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, invocation);

        replacementNode = replacementNode
            .WithLeadingTrivia(binary.GetLeadingTrivia())
            .WithTrailingTrivia(binary.GetTrailingTrivia());

        var suggestedCode = isPositive
            ? $"string.IsNullOrEmpty({cleanTarget})"
            : $"!string.IsNullOrEmpty({cleanTarget})";

        match = new CondensationMatch
        {
            Node = binary,
            SuggestedCode = suggestedCode,
            ReplacementNode = replacementNode,
            Description = "Replaces verbose null and length checks with standard string.IsNullOrEmpty(s)."
        };

        return true;
    }

    /// <inheritdoc />
    protected override SyntaxNode RewriteNode(BinaryExpressionSyntax node, CondensationMatch match)
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

    private static bool TryExtractNullTarget(ExpressionSyntax expr, [NotNullWhen(true)] out ExpressionSyntax? target)
    {
        target = null;
        expr = StripParens(expr);

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

    private static bool TryExtractEmptyTarget(ExpressionSyntax expr, [NotNullWhen(true)] out ExpressionSyntax? target)
    {
        target = null;
        expr = StripParens(expr);

        if (expr is not BinaryExpressionSyntax binary)
        {
            return false;
        }

        // s.Length == 0  or  0 == s.Length  or  s.Length <= 0
        if (binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.LessThanOrEqualExpression))
        {
            if (TryExtractLengthReceiver(binary.Left, out var receiver) && IsZeroLiteral(binary.Right))
            {
                target = receiver;
                return true;
            }

            if (TryExtractLengthReceiver(binary.Right, out receiver) && IsZeroLiteral(binary.Left))
            {
                target = receiver;
                return true;
            }
        }

        // s == ""  or  "" == s  or  s == string.Empty  or  string.Empty == s
        if (binary.IsKind(SyntaxKind.EqualsExpression))
        {
            if (IsEmptyStringLiteralOrField(binary.Right))
            {
                target = binary.Left;
                return true;
            }

            if (IsEmptyStringLiteralOrField(binary.Left))
            {
                target = binary.Right;
                return true;
            }
        }

        return false;
    }

    private static bool TryExtractNonEmptyTarget(ExpressionSyntax expr, [NotNullWhen(true)] out ExpressionSyntax? target)
    {
        target = null;
        expr = StripParens(expr);

        if (expr is not BinaryExpressionSyntax binary)
        {
            return false;
        }

        // s.Length > 0  or  0 < s.Length  or  s.Length != 0
        if (binary.IsKind(SyntaxKind.GreaterThanExpression))
        {
            if (TryExtractLengthReceiver(binary.Left, out var receiver) && IsZeroLiteral(binary.Right))
            {
                target = receiver;
                return true;
            }
        }
        else if (binary.IsKind(SyntaxKind.LessThanExpression))
        {
            if (TryExtractLengthReceiver(binary.Right, out var receiver) && IsZeroLiteral(binary.Left))
            {
                target = receiver;
                return true;
            }
        }
        else if (binary.IsKind(SyntaxKind.NotEqualsExpression))
        {
            if (TryExtractLengthReceiver(binary.Left, out var receiver) && IsZeroLiteral(binary.Right))
            {
                target = receiver;
                return true;
            }

            if (TryExtractLengthReceiver(binary.Right, out receiver) && IsZeroLiteral(binary.Left))
            {
                target = receiver;
                return true;
            }

            // s != ""  or  s != string.Empty
            if (IsEmptyStringLiteralOrField(binary.Right))
            {
                target = binary.Left;
                return true;
            }

            if (IsEmptyStringLiteralOrField(binary.Left))
            {
                target = binary.Right;
                return true;
            }
        }

        return false;
    }

    private static bool TryExtractLengthReceiver(ExpressionSyntax expr, [NotNullWhen(true)] out ExpressionSyntax? receiver)
    {
        receiver = null;
        if (expr is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.ValueText.Equals("Length", StringComparison.Ordinal))
        {
            receiver = memberAccess.Expression;
            return true;
        }

        return false;
    }

    private static bool IsZeroLiteral(ExpressionSyntax expr)
    {
        return expr is LiteralExpressionSyntax literal &&
               literal.IsKind(SyntaxKind.NumericLiteralExpression) &&
               literal.Token.Value is 0;
    }

    private static bool IsEmptyStringLiteralOrField(ExpressionSyntax expr)
    {
        if (expr is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression) &&
            literal.Token.ValueText.Length == 0)
        {
            return true;
        }

        if (expr is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.ValueText.Equals("Empty", StringComparison.Ordinal))
        {
            var owner = memberAccess.Expression.ToString();
            return owner.Equals("string", StringComparison.Ordinal) ||
                   owner.Equals("String", StringComparison.Ordinal) ||
                   owner.Equals("System.String", StringComparison.Ordinal);
        }

        return false;
    }
}
