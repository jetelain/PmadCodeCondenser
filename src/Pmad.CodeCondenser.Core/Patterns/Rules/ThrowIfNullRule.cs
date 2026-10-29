using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Patterns.Rules;

/// <summary>
/// Detects if (arg == null) throw new ArgumentNullException(...) and suggests ArgumentNullException.ThrowIfNull(arg).
/// </summary>
public sealed class ThrowIfNullRule : CondensationPatternRuleBase<IfStatementSyntax>
{
    private static readonly HashSet<SyntaxKind> ApplicableNodeKinds = [SyntaxKind.IfStatement];

    /// <inheritdoc />
    public override string RuleId => "PCC404";

    /// <inheritdoc />
    public override string Name => "Use ArgumentNullException.ThrowIfNull";

    /// <inheritdoc />
    public override string Description =>
        "Replaces verbose if (arg == null) throw new ArgumentNullException(...) with ArgumentNullException.ThrowIfNull(arg).";

    /// <inheritdoc />
    public override PatternCategory Category => PatternCategory.Guard;

    /// <inheritdoc />
    public override Version MinDotNetVersion => new(6, 0);

    /// <inheritdoc />
    public override bool CanAutoFix => true;

    /// <inheritdoc />
    public override IReadOnlySet<SyntaxKind> ApplicableKinds => ApplicableNodeKinds;

    /// <inheritdoc />
    protected override bool TryMatchNode(
        IfStatementSyntax ifStatement,
        CondensationContext context,
        [NotNullWhen(true)] out CondensationMatch? match)
    {
        match = null;

        // Cannot have an else clause and cannot be an else-if branch
        if (ifStatement.Else != null || ifStatement.Parent is ElseClauseSyntax)
        {
            return false;
        }

        // Condition must be arg == null, null == arg, or arg is null
        ExpressionSyntax? argExpression = null;
        if (ifStatement.Condition is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.EqualsExpression))
        {
            if (binary.Right.IsKind(SyntaxKind.NullLiteralExpression))
            {
                argExpression = binary.Left;
            }
            else if (binary.Left.IsKind(SyntaxKind.NullLiteralExpression))
            {
                argExpression = binary.Right;
            }
        }
        else if (ifStatement.Condition is IsPatternExpressionSyntax isPattern &&
                 isPattern.Pattern is ConstantPatternSyntax constPattern &&
                 constPattern.Expression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            argExpression = isPattern.Expression;
        }

        if (argExpression is not IdentifierNameSyntax argIdentifier)
        {
            return false;
        }

        var paramName = argIdentifier.Identifier.ValueText;

        // Extract throw statement from direct statement or single-statement block
        ThrowStatementSyntax? throwStatement = null;
        if (ifStatement.Statement is ThrowStatementSyntax directThrow)
        {
            throwStatement = directThrow;
        }
        else if (ifStatement.Statement is BlockSyntax block && block.Statements.Count == 1 &&
                 block.Statements[0] is ThrowStatementSyntax blockThrow)
        {
            throwStatement = blockThrow;
        }

        if (throwStatement?.Expression is not ObjectCreationExpressionSyntax creation)
        {
            return false;
        }

        var typeText = creation.Type.ToString();
        if (!typeText.Equals("ArgumentNullException", StringComparison.Ordinal) &&
            !typeText.Equals("System.ArgumentNullException", StringComparison.Ordinal))
        {
            return false;
        }

        // Validate argument to ArgumentNullException constructor
        var args = creation.ArgumentList?.Arguments ?? default;
        if (args.Count > 1)
        {
            // Custom message or inner exception is specified; ThrowIfNull does not support custom messages
            return false;
        }

        if (args.Count == 1)
        {
            var firstArg = args[0].Expression;
            if (firstArg is InvocationExpressionSyntax nameofInvocation &&
                nameofInvocation.Expression is IdentifierNameSyntax nameofId &&
                nameofId.Identifier.ValueText.Equals("nameof", StringComparison.Ordinal) &&
                nameofInvocation.ArgumentList.Arguments.Count == 1)
            {
                var nameofArg = nameofInvocation.ArgumentList.Arguments[0].Expression;
                if (!nameofArg.ToString().Equals(paramName, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            else if (firstArg is LiteralExpressionSyntax literal &&
                     literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                if (!literal.Token.ValueText.Equals(paramName, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        var suggestedCode = $"ArgumentNullException.ThrowIfNull({paramName});";

        var replacementNode = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("ArgumentNullException"),
                    SyntaxFactory.IdentifierName("ThrowIfNull")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName(paramName))))));

        match = new CondensationMatch
        {
            Node = ifStatement,
            SuggestedCode = suggestedCode,
            ReplacementNode = replacementNode,
            Description = "Standardizes argument validation into a single-line .NET 6+ throw helper."
        };

        return true;
    }

    /// <inheritdoc />
    protected override SyntaxNode RewriteNode(IfStatementSyntax node, CondensationMatch match)
    {
        return match.ReplacementNode ?? node;
    }
}
