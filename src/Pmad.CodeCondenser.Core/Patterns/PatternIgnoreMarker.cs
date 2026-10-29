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
                    int startIdx = 0;
                    while (startIdx < text.Length)
                    {
                        var idx = text.IndexOf(marker, startIdx, StringComparison.OrdinalIgnoreCase);
                        if (idx < 0)
                        {
                            break;
                        }

                        var afterMarkerIdx = idx + marker.Length;
                        // Verify that the marker is not followed by an identifier character (e.g. '-' or alphanumeric)
                        if (afterMarkerIdx < text.Length)
                        {
                            var nextChar = text[afterMarkerIdx];
                            if (char.IsLetterOrDigit(nextChar) || nextChar == '-')
                            {
                                startIdx = afterMarkerIdx;
                                continue;
                            }
                        }

                        // Valid marker match found!
                        if (string.IsNullOrWhiteSpace(ruleId))
                        {
                            return true;
                        }

                        // Check what follows the marker
                        var remainder = text[afterMarkerIdx..].Trim();
                        if (remainder.StartsWith("*/", StringComparison.Ordinal))
                        {
                            remainder = string.Empty;
                        }

                        // If no colon, this is a general/unqualified ignore marker
                        if (!remainder.StartsWith(':'))
                        {
                            return true;
                        }

                        // Colon present: parse rule IDs e.g. ":PCC401,PCC402" or ": PCC401, PCC402"
                        var rulesPart = remainder[1..];
                        if (rulesPart.EndsWith("*/", StringComparison.Ordinal))
                        {
                            rulesPart = rulesPart[..^2];
                        }

                        var specifiedRules = rulesPart.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        if (specifiedRules.Length == 0)
                        {
                            return true;
                        }

                        if (specifiedRules.Any(r => r.Equals(ruleId, StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }

                        break;
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
                var simpleName = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
                foreach (var pattern in AttributeNamePatterns)
                {
                    if (simpleName.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                        simpleName.Equals(pattern + "Attribute", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                // Check SuppressMessageAttribute
                if (simpleName.Equals("SuppressMessage", StringComparison.OrdinalIgnoreCase) ||
                    simpleName.Equals("SuppressMessageAttribute", StringComparison.OrdinalIgnoreCase))
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
