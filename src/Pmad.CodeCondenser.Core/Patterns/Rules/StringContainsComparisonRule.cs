using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Patterns.Rules;

/// <summary>
/// Detects IndexOf comparisons (e.g., IndexOf(...) >= 0 or IndexOf(...) != -1) and suggests Contains(...).
/// </summary>
public sealed class StringContainsComparisonRule : CondensationPatternRuleBase<BinaryExpressionSyntax>
{
    private static readonly HashSet<SyntaxKind> ApplicableNodeKinds =
    [
        SyntaxKind.GreaterThanOrEqualExpression,
        SyntaxKind.GreaterThanExpression,
        SyntaxKind.NotEqualsExpression,
        SyntaxKind.EqualsExpression,
        SyntaxKind.LessThanExpression,
        SyntaxKind.LessThanOrEqualExpression
    ];

    private static readonly Version Net6Version = new(6, 0);

    /// <inheritdoc />
    public override string RuleId => "PCC406";

    /// <inheritdoc />
    public override string Name => "Use String.Contains";

    /// <inheritdoc />
    public override string Description =>
        "Replaces IndexOf(...) >= 0 or IndexOf(...) != -1 with Contains(...).";

    /// <inheritdoc />
    public override PatternCategory Category => PatternCategory.Text;

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

        InvocationExpressionSyntax? invocation = null;
        bool? isPositive = null;

        if (binary.Left is InvocationExpressionSyntax leftInv && TryGetIntLiteral(binary.Right, out int rightVal))
        {
            invocation = leftInv;
            isPositive = EvaluateComparison(binary.Kind(), rightVal, isInvocationOnLeft: true);
        }
        else if (binary.Right is InvocationExpressionSyntax rightInv && TryGetIntLiteral(binary.Left, out int leftVal))
        {
            invocation = rightInv;
            isPositive = EvaluateComparison(binary.Kind(), leftVal, isInvocationOnLeft: false);
        }

        if (invocation == null || isPositive == null)
        {
            return false;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            !memberAccess.Name.Identifier.ValueText.Equals("IndexOf", StringComparison.Ordinal))
        {
            return false;
        }

        var argCount = invocation.ArgumentList.Arguments.Count;
        if (argCount != 1 && argCount != 2)
        {
            return false;
        }

        var firstArg = invocation.ArgumentList.Arguments[0].Expression;

        // If 1 argument and it is a character literal, string.Contains(char) requires .NET Standard 2.1 / .NET 6+
        if (argCount == 1 && firstArg.IsKind(SyntaxKind.CharacterLiteralExpression))
        {
            if (context.TargetDotNetVersion != null && context.TargetDotNetVersion < Net6Version)
            {
                return false;
            }
        }

        // If 2 arguments, second must be StringComparison
        if (argCount == 2)
        {
            var secondArg = invocation.ArgumentList.Arguments[1].Expression;
            if (!IsStringComparisonArgument(secondArg))
            {
                return false;
            }

            // string.Contains(value, comparison) requires .NET Standard 2.1 / .NET Core 2.1 / .NET 6+
            if (context.TargetDotNetVersion != null && context.TargetDotNetVersion < Net6Version)
            {
                return false;
            }
        }

        var receiver = memberAccess.Expression;
        var containsInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                receiver,
                SyntaxFactory.IdentifierName("Contains")),
            invocation.ArgumentList);

        ExpressionSyntax replacementNode = isPositive.Value
            ? containsInvocation
            : SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, containsInvocation);

        var argsText = string.Join(", ", invocation.ArgumentList.Arguments.Select(a => a.ToString()));
        var suggestedCode = isPositive.Value
            ? $"{receiver}.Contains({argsText})"
            : $"!{receiver}.Contains({argsText})";

        match = new CondensationMatch
        {
            Node = binary,
            SuggestedCode = suggestedCode,
            ReplacementNode = replacementNode,
            Description = "Replaces IndexOf comparison with more expressive and intention-revealing Contains()."
        };

        return true;
    }

    /// <inheritdoc />
    protected override SyntaxNode RewriteNode(BinaryExpressionSyntax node, CondensationMatch match)
    {
        return match.ReplacementNode ?? node;
    }

    private static bool TryGetIntLiteral(ExpressionSyntax expr, out int value)
    {
        value = 0;
        if (expr is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.NumericLiteralExpression))
        {
            if (literal.Token.Value is int i)
            {
                value = i;
                return true;
            }
        }
        else if (expr is PrefixUnaryExpressionSyntax prefix &&
                 prefix.IsKind(SyntaxKind.UnaryMinusExpression) &&
                 prefix.Operand is LiteralExpressionSyntax innerLiteral &&
                 innerLiteral.IsKind(SyntaxKind.NumericLiteralExpression))
        {
            if (innerLiteral.Token.Value is int i)
            {
                value = -i;
                return true;
            }
        }

        return false;
    }

    private static bool? EvaluateComparison(SyntaxKind kind, int targetValue, bool isInvocationOnLeft)
    {
        if (targetValue == 0)
        {
            return (kind, isInvocationOnLeft) switch
            {
                // IndexOf >= 0  or  0 <= IndexOf
                (SyntaxKind.GreaterThanOrEqualExpression, true) or (SyntaxKind.LessThanOrEqualExpression, false) => true,
                // IndexOf < 0  or  0 > IndexOf
                (SyntaxKind.LessThanExpression, true) or (SyntaxKind.GreaterThanExpression, false) => false,
                _ => null
            };
        }

        if (targetValue == -1)
        {
            return (kind, isInvocationOnLeft) switch
            {
                // IndexOf != -1  or  -1 != IndexOf
                (SyntaxKind.NotEqualsExpression, _) => true,
                // IndexOf > -1  or  -1 < IndexOf
                (SyntaxKind.GreaterThanExpression, true) or (SyntaxKind.LessThanExpression, false) => true,
                // IndexOf == -1  or  -1 == IndexOf
                (SyntaxKind.EqualsExpression, _) => false,
                // IndexOf <= -1  or  -1 >= IndexOf
                (SyntaxKind.LessThanOrEqualExpression, true) or (SyntaxKind.GreaterThanOrEqualExpression, false) => false,
                _ => null
            };
        }

        return null;
    }

    private static bool IsStringComparisonArgument(ExpressionSyntax expr)
    {
        var exprText = expr.ToString();
        return exprText.Contains("StringComparison", StringComparison.Ordinal) ||
               exprText.Contains("OrdinalIgnoreCase", StringComparison.Ordinal) ||
               exprText.Contains("CurrentCulture", StringComparison.Ordinal) ||
               exprText.Contains("InvariantCulture", StringComparison.Ordinal);
    }
}
