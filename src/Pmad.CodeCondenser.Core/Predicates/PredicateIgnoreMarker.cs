using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pmad.CodeCondenser.Core.Predicates;

/// <summary>
/// Detects markers used to suppress or ignore predicate and condition clone detection.
/// </summary>
public static class PredicateIgnoreMarker
{
    private static readonly string[] CommentMarkerPatterns =
    [
        "codecondenser:ignore-predicate",
        "codecondenser:ignore-condition",
        "codecondenser:ignore-bool",
        "codecondenser:ignore-clones",
        "codecondenser:ignore-duplicate",
        "codecondenser:ignore",
        "nocondense",
        "nopredicate",
        "no-clone"
    ];

    private static readonly string[] AttributeNamePatterns =
    [
        "CodeCondenserIgnore",
        "NoClone",
        "IgnorePredicate",
        "PredicateIgnore",
        "IgnoreCondition",
        "IgnoreDuplication",
        "IgnoreClone",
        "PredicateClone",
        "SuppressDuplication"
    ];

    /// <summary>
    /// Determines whether the given condition expression or its enclosing context has an ignore marker.
    /// </summary>
    public static bool IsIgnored(SyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        // 1. Check leading and trailing trivia on the node itself
        if (HasIgnoreCommentTrivia(node.GetLeadingTrivia()) ||
            HasIgnoreCommentTrivia(node.GetTrailingTrivia()))
        {
            return true;
        }

        // 2. Check enclosing statement
        var statement = node.FirstAncestorOrSelf<StatementSyntax>();
        if (statement != null &&
            (HasIgnoreCommentTrivia(statement.GetLeadingTrivia()) ||
             HasIgnoreCommentTrivia(statement.GetTrailingTrivia())))
        {
            return true;
        }

        // 3. Check enclosing members and types (method, property, class, record, struct)
        var member = node.FirstAncestorOrSelf<MemberDeclarationSyntax>();
        while (member != null)
        {
            if (HasIgnoreAttribute(member) ||
                HasIgnoreCommentTrivia(member.GetLeadingTrivia()) ||
                HasIgnoreCommentTrivia(member.GetTrailingTrivia()))
            {
                return true;
            }

            member = member.Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>();
        }

        return false;
    }

    private static bool HasIgnoreAttribute(MemberDeclarationSyntax member)
    {
        SyntaxList<AttributeListSyntax> attributeLists = member switch
        {
            MethodDeclarationSyntax m => m.AttributeLists,
            PropertyDeclarationSyntax p => p.AttributeLists,
            ClassDeclarationSyntax c => c.AttributeLists,
            RecordDeclarationSyntax r => r.AttributeLists,
            StructDeclarationSyntax s => s.AttributeLists,
            _ => default
        };

        foreach (var attributeList in attributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var attrName = attribute.Name.ToString();

                foreach (var pattern in AttributeNamePatterns)
                {
                    if (attrName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                if (attrName.EndsWith("SuppressMessage", StringComparison.OrdinalIgnoreCase) ||
                    attrName.EndsWith("SuppressMessageAttribute", StringComparison.OrdinalIgnoreCase))
                {
                    if (attribute.ArgumentList != null)
                    {
                        var args = attribute.ArgumentList.Arguments;
                        if (args.Count >= 2)
                        {
                            var category = args[0].Expression.ToString().Trim('"', '@');
                            var checkId = args[1].Expression.ToString().Trim('"', '@');

                            if (category.Contains("CodeCondenser", StringComparison.OrdinalIgnoreCase) &&
                                (checkId.Contains("Predicate", StringComparison.OrdinalIgnoreCase) ||
                                 checkId.Contains("Condition", StringComparison.OrdinalIgnoreCase) ||
                                 checkId.Contains("Duplication", StringComparison.OrdinalIgnoreCase) ||
                                 checkId.Contains("CC010", StringComparison.OrdinalIgnoreCase) ||
                                 checkId.Contains("CC011", StringComparison.OrdinalIgnoreCase)))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }

    private static bool HasIgnoreCommentTrivia(SyntaxTriviaList triviaList)
    {
        foreach (var trivia in triviaList)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                var text = trivia.ToString();
                foreach (var pattern in CommentMarkerPatterns)
                {
                    if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
