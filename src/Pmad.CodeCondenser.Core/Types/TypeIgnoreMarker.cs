using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pmad.CodeCondenser.Core.Types;

/// <summary>
/// Detects markers used to suppress or ignore single-type-per-file enforcement.
/// </summary>
public static class TypeIgnoreMarker
{
    private static readonly string[] CommentMarkerPatterns =
    [
        "codecondenser:ignore-type-split",
        "codecondenser:ignore-single-type",
        "codecondenser:ignore-type",
        "codecondenser:ignore",
        "nocondense"
    ];

    private static readonly string[] AttributeNamePatterns =
    [
        "CodeCondenserIgnore",
        "SingleTypePerFileIgnore",
        "IgnoreTypeSplit"
    ];

    /// <summary>
    /// Checks if a member declaration (class, interface, struct, enum, record, delegate) is ignored.
    /// </summary>
    public static bool IsIgnored(MemberDeclarationSyntax member)
    {
        ArgumentNullException.ThrowIfNull(member);

        // 1. Check leading/trailing trivia on member
        if (HasIgnoreCommentTrivia(member.GetLeadingTrivia()) ||
            HasIgnoreCommentTrivia(member.GetTrailingTrivia()))
        {
            return true;
        }

        // 2. Check attributes
        if (HasIgnoreAttribute(member))
        {
            return true;
        }

        // 3. Check enclosing compilation unit / file level trivia
        if (member.SyntaxTree != null)
        {
            var root = member.SyntaxTree.GetRoot();
            if (HasIgnoreCommentTrivia(root.GetLeadingTrivia()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if an entire compilation unit or file is suppressed.
    /// </summary>
    public static bool IsFileIgnored(CompilationUnitSyntax root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (HasIgnoreCommentTrivia(root.GetLeadingTrivia()))
        {
            return true;
        }

        foreach (var attributeList in root.AttributeLists)
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

                if (IsSuppressMessageAttribute(attribute))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasIgnoreAttribute(MemberDeclarationSyntax member)
    {
        SyntaxList<AttributeListSyntax> attributeLists = member switch
        {
            BaseTypeDeclarationSyntax b => b.AttributeLists,
            DelegateDeclarationSyntax d => d.AttributeLists,
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

                if (IsSuppressMessageAttribute(attribute))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsSuppressMessageAttribute(AttributeSyntax attribute)
    {
        var attrName = attribute.Name.ToString();
        if (attrName.EndsWith("SuppressMessage", StringComparison.OrdinalIgnoreCase) ||
            attrName.EndsWith("SuppressMessageAttribute", StringComparison.OrdinalIgnoreCase))
        {
            if (attribute.ArgumentList != null)
            {
                var args = attribute.ArgumentList.Arguments;
                if (args.Count >= 2)
                {
                    bool isCodeCondenser = args.Any(a => a.ToString().Contains("CodeCondenser", StringComparison.OrdinalIgnoreCase));
                    bool isTypeRule = args.Any(a =>
                    {
                        var text = a.ToString();
                        return text.Contains("SingleType", StringComparison.OrdinalIgnoreCase) ||
                               text.Contains("TypeSplit", StringComparison.OrdinalIgnoreCase) ||
                               text.Contains("CC007", StringComparison.OrdinalIgnoreCase) ||
                               text.Contains("CC0007", StringComparison.OrdinalIgnoreCase);
                    });

                    if (isCodeCondenser && isTypeRule)
                    {
                        return true;
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
                var commentText = trivia.ToFullString();
                foreach (var pattern in CommentMarkerPatterns)
                {
                    if (commentText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
