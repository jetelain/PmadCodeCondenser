using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Patterns.Rules;

/// <summary>
/// Detects OrderBy(...).First() / OrderByDescending(...).FirstOrDefault() and suggests MinBy / MaxBy.
/// </summary>
public sealed class MinByMaxByRule : CondensationPatternRuleBase<InvocationExpressionSyntax>
{
    private static readonly HashSet<SyntaxKind> ApplicableNodeKinds = [SyntaxKind.InvocationExpression];

    private static readonly HashSet<string> TerminalMethods = new(StringComparer.Ordinal)
    {
        "First", "FirstOrDefault", "Last", "LastOrDefault"
    };

    /// <inheritdoc />
    public override string RuleId => "PCC407";

    /// <inheritdoc />
    public override string Name => "Use MinBy or MaxBy";

    /// <inheritdoc />
    public override string Description =>
        "Replaces OrderBy(...).First() or OrderByDescending(...).FirstOrDefault() with MinBy(...) or MaxBy(...).";

    /// <inheritdoc />
    public override PatternCategory Category => PatternCategory.Linq;

    /// <inheritdoc />
    public override Version MinDotNetVersion => new(6, 0);

    /// <inheritdoc />
    public override bool CanAutoFix => true;

    /// <inheritdoc />
    public override IReadOnlySet<SyntaxKind> ApplicableKinds => ApplicableNodeKinds;

    /// <inheritdoc />
    protected override bool TryMatchNode(
        InvocationExpressionSyntax outerInvocation,
        CondensationContext context,
        [NotNullWhen(true)] out CondensationMatch? match)
    {
        match = null;

        // Outer: .First(), .FirstOrDefault(), .Last(), .LastOrDefault() with 0 arguments
        if (outerInvocation.ArgumentList.Arguments.Count != 0 ||
            outerInvocation.Expression is not MemberAccessExpressionSyntax outerMemberAccess ||
            !TerminalMethods.Contains(outerMemberAccess.Name.Identifier.ValueText))
        {
            return false;
        }

        var terminalName = outerMemberAccess.Name.Identifier.ValueText;

        // Inner: receiver.OrderBy(...) or receiver.OrderByDescending(...) with 1 or 2 arguments
        if (outerMemberAccess.Expression is not InvocationExpressionSyntax innerInvocation ||
            innerInvocation.Expression is not MemberAccessExpressionSyntax innerMemberAccess)
        {
            return false;
        }

        var innerMethodName = innerMemberAccess.Name.Identifier.ValueText;
        bool isAscending = innerMethodName.Equals("OrderBy", StringComparison.Ordinal);
        bool isDescending = innerMethodName.Equals("OrderByDescending", StringComparison.Ordinal);

        if (!isAscending && !isDescending)
        {
            return false;
        }

        if (innerInvocation.ArgumentList.Arguments.Count is < 1 or > 2)
        {
            return false;
        }

        bool isFirst = terminalName.StartsWith("First", StringComparison.Ordinal);
        bool isMin = (isAscending && isFirst) || (isDescending && !isFirst);
        var targetMethodName = isMin ? "MinBy" : "MaxBy";

        var receiver = innerMemberAccess.Expression;
        var argsText = string.Join(", ", innerInvocation.ArgumentList.Arguments.Select(a => a.ToString()));
        var suggestedCode = $"{receiver}.{targetMethodName}({argsText})";

        var replacementNode = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                receiver,
                SyntaxFactory.IdentifierName(targetMethodName)),
            innerInvocation.ArgumentList);

        match = new CondensationMatch
        {
            Node = outerInvocation,
            SuggestedCode = suggestedCode,
            ReplacementNode = replacementNode,
            Description = $"Replaces .{innerMethodName}().{terminalName}() with .{targetMethodName}(), avoiding full collection sorting."
        };

        return true;
    }

    /// <inheritdoc />
    protected override SyntaxNode RewriteNode(InvocationExpressionSyntax node, CondensationMatch match)
    {
        return match.ReplacementNode ?? node;
    }
}
