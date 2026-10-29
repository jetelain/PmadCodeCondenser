using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Patterns.Rules;

/// <summary>
/// Detects .Where(predicate).Any()/.Count()/.FirstOrDefault() chains and consolidates the predicate into the terminal method.
/// </summary>
public sealed class LinqWhereSimplificationRule : CondensationPatternRuleBase<InvocationExpressionSyntax>
{
    private static readonly HashSet<SyntaxKind> ApplicableNodeKinds = [SyntaxKind.InvocationExpression];

    private static readonly HashSet<string> TerminalMethods = new(StringComparer.Ordinal)
    {
        "Any", "Count", "FirstOrDefault", "First", "SingleOrDefault", "Single"
    };

    /// <inheritdoc />
    public override string RuleId => "PCC403";

    /// <inheritdoc />
    public override string Name => "Simplify LINQ Where Predicate";

    /// <inheritdoc />
    public override string Description =>
        "Replaces .Where(predicate).Any() or .Count() with .Any(predicate) or .Count(predicate).";

    /// <inheritdoc />
    public override PatternCategory Category => PatternCategory.Linq;

    /// <inheritdoc />
    public override Version? MinDotNetVersion => null;

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

        // Outer: .Any() / .Count() / .FirstOrDefault() with no arguments
        if (outerInvocation.ArgumentList.Arguments.Count != 0 ||
            outerInvocation.Expression is not MemberAccessExpressionSyntax outerMemberAccess ||
            !TerminalMethods.Contains(outerMemberAccess.Name.Identifier.ValueText))
        {
            return false;
        }

        var terminalName = outerMemberAccess.Name.Identifier.ValueText;

        // Inner: receiver.Where(predicate) with exactly 1 argument
        if (outerMemberAccess.Expression is not InvocationExpressionSyntax innerInvocation ||
            innerInvocation.Expression is not MemberAccessExpressionSyntax innerMemberAccess ||
            !innerMemberAccess.Name.Identifier.ValueText.Equals("Where", StringComparison.Ordinal) ||
            innerInvocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        var receiver = innerMemberAccess.Expression;
        var predicateArg = innerInvocation.ArgumentList.Arguments[0];

        // Terminal methods (Any, Count, First, etc.) only accept Func<T, bool>, not indexed Func<T, int, bool>
        if (predicateArg.Expression is ParenthesizedLambdaExpressionSyntax parenthesizedLambda &&
            parenthesizedLambda.ParameterList.Parameters.Count > 1)
        {
            return false;
        }

        if (predicateArg.Expression is AnonymousMethodExpressionSyntax anonMethod &&
            anonMethod.ParameterList != null &&
            anonMethod.ParameterList.Parameters.Count > 1)
        {
            return false;
        }

        var suggestedCode = $"{receiver}.{outerMemberAccess.Name}({predicateArg.Expression})";

        var replacementNode = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                receiver,
                outerMemberAccess.Name),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(predicateArg)));

        match = new CondensationMatch
        {
            Node = outerInvocation,
            SuggestedCode = suggestedCode,
            ReplacementNode = replacementNode,
            Description = $"Consolidates predicate into .{terminalName}(), eliminating the intermediate Where iterator."
        };

        return true;
    }

    /// <inheritdoc />
    protected override SyntaxNode RewriteNode(InvocationExpressionSyntax node, CondensationMatch match)
    {
        return match.ReplacementNode ?? node;
    }
}
