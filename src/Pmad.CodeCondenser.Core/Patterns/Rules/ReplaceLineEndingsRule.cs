using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Patterns.Rules;

/// <summary>
/// Detects chained .Replace("\r\n", replacement).Replace("\r", replacement) calls and suggests .ReplaceLineEndings(replacement).
/// </summary>
public sealed class ReplaceLineEndingsRule : CondensationPatternRuleBase<InvocationExpressionSyntax>
{
    private static readonly HashSet<SyntaxKind> ApplicableNodeKinds = [SyntaxKind.InvocationExpression];

    /// <inheritdoc />
    public override string RuleId => "PCC401";

    /// <inheritdoc />
    public override string Name => "Use ReplaceLineEndings";

    /// <inheritdoc />
    public override string Description =>
        "Replaces chained .Replace(\"\\r\\n\", ...).Replace(\"\\r\", ...) with .ReplaceLineEndings(...).";

    /// <inheritdoc />
    public override PatternCategory Category => PatternCategory.Text;

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

        // Outer: target.Replace("\r", replacement)
        if (outerInvocation.Expression is not MemberAccessExpressionSyntax outerMemberAccess ||
            !outerMemberAccess.Name.Identifier.ValueText.Equals("Replace", StringComparison.Ordinal) ||
            outerInvocation.ArgumentList.Arguments.Count != 2)
        {
            return false;
        }

        // Inner: receiver.Replace("\r\n", replacement)
        if (outerMemberAccess.Expression is not InvocationExpressionSyntax innerInvocation ||
            innerInvocation.Expression is not MemberAccessExpressionSyntax innerMemberAccess ||
            !innerMemberAccess.Name.Identifier.ValueText.Equals("Replace", StringComparison.Ordinal) ||
            innerInvocation.ArgumentList.Arguments.Count != 2)
        {
            return false;
        }

        var innerArg0 = innerInvocation.ArgumentList.Arguments[0].Expression;
        var innerArg1 = innerInvocation.ArgumentList.Arguments[1].Expression;
        var outerArg0 = outerInvocation.ArgumentList.Arguments[0].Expression;
        var outerArg1 = outerInvocation.ArgumentList.Arguments[1].Expression;

        // Check search strings: inner "\r\n" and outer "\r", or vice versa
        var innerSearch = GetLiteralString(innerArg0);
        var outerSearch = GetLiteralString(outerArg0);

        bool isWindowsThenMac = innerSearch == "\r\n" && outerSearch == "\r";
        bool isMacThenWindows = innerSearch == "\r" && outerSearch == "\r\n";

        if (!isWindowsThenMac && !isMacThenWindows)
        {
            return false;
        }

        // Both replacements must match
        var innerReplText = innerArg1.ToString();
        var outerReplText = outerArg1.ToString();

        if (!innerReplText.Equals(outerReplText, StringComparison.Ordinal))
        {
            return false;
        }

        var outerLiteral = GetLiteralString(outerArg1);
        bool isEnvNewline = outerReplText.Equals("Environment.NewLine", StringComparison.Ordinal) ||
                            outerReplText.Equals("System.Environment.NewLine", StringComparison.Ordinal);
        bool isStandardNewlineLiteral = outerLiteral is "\n" or "\r\n";

        if (!isEnvNewline && !isStandardNewlineLiteral)
        {
            return false;
        }

        var receiver = innerMemberAccess.Expression;
        var receiverText = receiver.ToString();

        // Determine replacement argument for ReplaceLineEndings
        ArgumentListSyntax newArgList;
        string suggestedCode;

        if (isEnvNewline)
        {
            newArgList = SyntaxFactory.ArgumentList();
            suggestedCode = $"{receiverText}.ReplaceLineEndings()";
        }
        else
        {
            newArgList = SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(outerArg1)));
            suggestedCode = $"{receiverText}.ReplaceLineEndings({outerReplText})";
        }

        var replacementNode = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                receiver,
                SyntaxFactory.IdentifierName("ReplaceLineEndings")),
            newArgList);

        match = new CondensationMatch
        {
            Node = outerInvocation,
            SuggestedCode = suggestedCode,
            ReplacementNode = replacementNode,
            Description = "Normalizes line endings in a single pass without intermediate string allocations."
        };

        return true;
    }

    /// <inheritdoc />
    protected override SyntaxNode RewriteNode(InvocationExpressionSyntax node, CondensationMatch match)
    {
        return match.ReplacementNode ?? node;
    }

    private static string? GetLiteralString(ExpressionSyntax expression)
    {
        if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }

        return null;
    }
}
