using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Patterns.Rules;

/// <summary>
/// Detects if (string.IsNullOrWhiteSpace(arg)) throw new ArgumentException(...) and suggests ArgumentException.ThrowIfNullOrWhiteSpace(arg).
/// Also detects if (string.IsNullOrEmpty(arg)) throw new ArgumentException(...) and suggests ArgumentException.ThrowIfNullOrEmpty(arg).
/// </summary>
public sealed class ThrowIfNullOrWhiteSpaceRule : CondensationPatternRuleBase<IfStatementSyntax>
{
    private static readonly HashSet<SyntaxKind> ApplicableNodeKinds = [SyntaxKind.IfStatement];

    /// <inheritdoc />
    public override string RuleId => "PCC405";

    /// <inheritdoc />
    public override string Name => "Use ArgumentException Throw Helpers";

    /// <inheritdoc />
    public override string Description =>
        "Replaces verbose if (string.IsNullOrWhiteSpace(arg)) throw new ArgumentException(...) with ArgumentException.ThrowIfNullOrWhiteSpace(arg).";

    /// <inheritdoc />
    public override PatternCategory Category => PatternCategory.Guard;

    /// <inheritdoc />
    public override Version MinDotNetVersion => new(8, 0);

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

        // Cannot have an else clause
        if (ifStatement.Else != null)
        {
            return false;
        }

        // Condition must be string.IsNullOrWhiteSpace(arg) or string.IsNullOrEmpty(arg)
        if (ifStatement.Condition is not InvocationExpressionSyntax conditionInvocation ||
            conditionInvocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        string checkMethodName;
        if (conditionInvocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            checkMethodName = memberAccess.Name.Identifier.ValueText;
            var owner = memberAccess.Expression.ToString();
            if (!owner.Equals("string", StringComparison.Ordinal) &&
                !owner.Equals("String", StringComparison.Ordinal) &&
                !owner.Equals("System.String", StringComparison.Ordinal))
            {
                return false;
            }
        }
        else if (conditionInvocation.Expression is IdentifierNameSyntax identifier)
        {
            checkMethodName = identifier.Identifier.ValueText;
        }
        else
        {
            return false;
        }

        bool isWhiteSpace = checkMethodName.Equals("IsNullOrWhiteSpace", StringComparison.Ordinal);
        bool isNullOrEmpty = checkMethodName.Equals("IsNullOrEmpty", StringComparison.Ordinal);

        if (!isWhiteSpace && !isNullOrEmpty)
        {
            return false;
        }

        var conditionArg = conditionInvocation.ArgumentList.Arguments[0].Expression;
        if (conditionArg is not IdentifierNameSyntax argIdentifier)
        {
            return false;
        }

        var paramName = argIdentifier.Identifier.ValueText;

        // Statement must be throw new ArgumentException(...)
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
        if (!typeText.Equals("ArgumentException", StringComparison.Ordinal) &&
            !typeText.Equals("System.ArgumentException", StringComparison.Ordinal))
        {
            return false;
        }

        // Validate paramName argument if provided in ArgumentException(message, paramName)
        var args = creation.ArgumentList?.Arguments ?? default;
        if (args.Count >= 2)
        {
            var secondArg = args[1].Expression;
            if (!MatchesParamName(secondArg, paramName))
            {
                return false;
            }
        }
        else if (args.Count == 1)
        {
            var singleArg = args[0].Expression;
            if (ExtractNameofIdentifier(singleArg) is { } nameofId &&
                !nameofId.Equals(paramName, StringComparison.Ordinal))
            {
                return false;
            }
        }

        var helperMethodName = isWhiteSpace ? "ThrowIfNullOrWhiteSpace" : "ThrowIfNullOrEmpty";
        var suggestedCode = $"ArgumentException.{helperMethodName}({paramName});";

        var replacementNode = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("ArgumentException"),
                    SyntaxFactory.IdentifierName(helperMethodName)),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName(paramName))))));

        match = new CondensationMatch
        {
            Node = ifStatement,
            SuggestedCode = suggestedCode,
            ReplacementNode = replacementNode,
            Description = $"Replaces {checkMethodName} check with .NET 8 ArgumentException.{helperMethodName}."
        };

        return true;
    }

    /// <inheritdoc />
    protected override SyntaxNode RewriteNode(IfStatementSyntax node, CondensationMatch match)
    {
        return match.ReplacementNode ?? node;
    }

    private static string? ExtractNameofIdentifier(ExpressionSyntax expr)
    {
        if (expr is InvocationExpressionSyntax nameofInvocation &&
            nameofInvocation.Expression is IdentifierNameSyntax nameofId &&
            nameofId.Identifier.ValueText.Equals("nameof", StringComparison.Ordinal) &&
            nameofInvocation.ArgumentList.Arguments.Count == 1)
        {
            return nameofInvocation.ArgumentList.Arguments[0].Expression.ToString();
        }

        return null;
    }

    private static bool MatchesParamName(ExpressionSyntax expr, string paramName)
    {
        var nameofId = ExtractNameofIdentifier(expr);
        if (nameofId != null)
        {
            return nameofId.Equals(paramName, StringComparison.Ordinal);
        }

        if (expr is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText.Equals(paramName, StringComparison.Ordinal);
        }

        return false;
    }
}
