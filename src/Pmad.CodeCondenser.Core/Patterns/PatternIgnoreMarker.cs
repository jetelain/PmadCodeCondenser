using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pmad.CodeCondenser.Core.Patterns;

/// <summary>
/// Detects markers used to suppress or ignore condensation pattern detection.
/// </summary>
public static class PatternIgnoreMarker
{
    private static readonly string[] CommentMarkerPatterns =
    [
        "codecondenser:ignore-pattern",
        "codecondenser:ignore-condensation",
        "codecondenser:ignore-modernization",
        "codecondenser:ignore",
        "nocondense"
    ];

    private static readonly string[] AttributeNamePatterns =
    [
        "CodeCondenserIgnore",
        "IgnorePattern",
        "NoCondense",
        "SuppressCondensation"
    ];

    /// <summary>
    /// Determines whether the given node or its enclosing declaration has an ignore marker or suppression.
    /// </summary>
    public static bool IsIgnored(SyntaxNode node, string? ruleId = null)
    {
        ArgumentNullException.ThrowIfNull(node);

        // 1. Check leading and trailing trivia on the node itself
        if (HasIgnoreCommentTrivia(node.GetLeadingTrivia(), ruleId) ||
            HasIgnoreCommentTrivia(node.GetTrailingTrivia(), ruleId))
        {
            return true;
        }

        // 2. Check preceding sibling or statement trivia (e.g. comment on preceding line)
        var parentStatement = node.FirstAncestorOrSelf<StatementSyntax>();
        if (parentStatement != null && parentStatement != node)
        {
            if (HasIgnoreCommentTrivia(parentStatement.GetLeadingTrivia(), ruleId))
            {
                return true;
            }
        }

        // 3. Check enclosing method/class declarations for attributes or comments
        var current = node.Parent;
        while (current != null)
        {
            if (current is MemberDeclarationSyntax memberDecl)
            {
                if (HasIgnoreAttribute(memberDecl, ruleId) || HasIgnoreCommentTrivia(memberDecl.GetLeadingTrivia(), ruleId))
                {
                    return true;
                }
            }

            current = current.Parent;
        }

        return false;
    }

    private static bool HasIgnoreCommentTrivia(SyntaxTriviaList triviaList, string? ruleId)
    {
        foreach (var trivia in triviaList)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                var text = trivia.ToFullString();
                foreach (var marker in CommentMarkerPatterns)
                {
                    var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        if (string.IsNullOrWhiteSpace(ruleId))
                        {
                            return true;
                        }

                        // Check if specific ruleId is specified or if general ignore
                        var remainder = text[(idx + marker.Length)..].Trim();
                        if (remainder.Length == 0 || remainder.StartsWith(':') && remainder.Contains(ruleId, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        if (remainder.Length == 0 || !remainder.StartsWith(':'))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    private static bool HasIgnoreAttribute(MemberDeclarationSyntax declaration, string? ruleId)
    {
        foreach (var attrList in declaration.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                foreach (var pattern in AttributeNamePatterns)
                {
                    if (name.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                        name.Equals(pattern + "Attribute", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                // Check SuppressMessageAttribute
                if (name.Equals("SuppressMessage", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("SuppressMessageAttribute", StringComparison.OrdinalIgnoreCase))
                {
                    if (attr.ArgumentList != null && !string.IsNullOrWhiteSpace(ruleId))
                    {
                        var args = attr.ArgumentList.Arguments;
                        if (args.Any(a => a.ToString().Contains(ruleId, StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}
