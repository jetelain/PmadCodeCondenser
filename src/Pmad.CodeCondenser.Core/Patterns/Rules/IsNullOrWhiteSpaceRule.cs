using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Patterns.Rules;

/// <summary>
/// Detects string.IsNullOrEmpty(str.Trim()) and suggests string.IsNullOrWhiteSpace(str).
/// </summary>
public sealed class IsNullOrWhiteSpaceRule : CondensationPatternRuleBase<InvocationExpressionSyntax>
{
    private static readonly HashSet<SyntaxKind> ApplicableNodeKinds = [SyntaxKind.InvocationExpression];

    /// <inheritdoc />
    public override string RuleId => "PCC402";

    /// <inheritdoc />
    public override string Name => "Use IsNullOrWhiteSpace";

    /// <inheritdoc />
    public override string Description =>
        "Replaces string.IsNullOrEmpty(str.Trim()) with string.IsNullOrWhiteSpace(str), avoiding trimmed string allocation.";

    /// <inheritdoc />
    public override PatternCategory Category => PatternCategory.Predicate;

    /// <inheritdoc />
    public override Version? MinDotNetVersion => new(4, 0);

    /// <inheritdoc />
    public override bool CanAutoFix => true;

    /// <inheritdoc />
    public override IReadOnlySet<SyntaxKind> ApplicableKinds => ApplicableNodeKinds;

    /// <inheritdoc />
    protected override bool TryMatchNode(
        InvocationExpressionSyntax invocation,
        CondensationContext context,
        [NotNullWhen(true)] out CondensationMatch? match)
    {
        match = null;

        if (invocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        // Check if method is IsNullOrEmpty (string.IsNullOrEmpty or String.IsNullOrEmpty or IsNullOrEmpty)
        string? methodOwner = null;
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            if (!memberAccess.Name.Identifier.ValueText.Equals("IsNullOrEmpty", StringComparison.Ordinal))
            {
                return false;
            }

            var ownerText = memberAccess.Expression.ToString();
            if (!ownerText.Equals("string", StringComparison.Ordinal) &&
                !ownerText.Equals("String", StringComparison.Ordinal) &&
                !ownerText.Equals("System.String", StringComparison.Ordinal))
            {
                return false;
            }

            methodOwner = ownerText;
        }
        else if (invocation.Expression is IdentifierNameSyntax identifier)
        {
            if (!identifier.Identifier.ValueText.Equals("IsNullOrEmpty", StringComparison.Ordinal))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        var arg = invocation.ArgumentList.Arguments[0].Expression;
        ExpressionSyntax? targetString = null;

        // Pattern 1: s.Trim()
        if (arg is InvocationExpressionSyntax trimInvocation &&
            trimInvocation.ArgumentList.Arguments.Count == 0 &&
            trimInvocation.Expression is MemberAccessExpressionSyntax trimAccess &&
            trimAccess.Name.Identifier.ValueText.Equals("Trim", StringComparison.Ordinal))
        {
            targetString = trimAccess.Expression;
        }
        // Pattern 2: s?.Trim()
        else if (arg is ConditionalAccessExpressionSyntax conditionalAccess &&
                 conditionalAccess.WhenNotNull is InvocationExpressionSyntax conditionalTrim &&
                 conditionalTrim.ArgumentList.Arguments.Count == 0 &&
                 conditionalTrim.Expression is MemberBindingExpressionSyntax memberBinding &&
                 memberBinding.Name.Identifier.ValueText.Equals("Trim", StringComparison.Ordinal))
        {
            targetString = conditionalAccess.Expression;
        }

        if (targetString == null)
        {
            return false;
        }

        var prefix = methodOwner ?? "string";
        var suggestedCode = $"{prefix}.IsNullOrWhiteSpace({targetString})";

        ExpressionSyntax newExpression = methodOwner != null
            ? SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                ((MemberAccessExpressionSyntax)invocation.Expression).Expression,
                SyntaxFactory.IdentifierName("IsNullOrWhiteSpace"))
            : SyntaxFactory.IdentifierName("IsNullOrWhiteSpace");

        var replacementNode = SyntaxFactory.InvocationExpression(
            newExpression,
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(targetString))));

        match = new CondensationMatch
        {
            Node = invocation,
            SuggestedCode = suggestedCode,
            ReplacementNode = replacementNode,
            Description = "Directly checks for null or whitespace without allocating a temporary trimmed string."
        };

        return true;
    }

    /// <inheritdoc />
    protected override SyntaxNode RewriteNode(InvocationExpressionSyntax node, CondensationMatch match)
    {
        return match.ReplacementNode ?? node;
    }
}
