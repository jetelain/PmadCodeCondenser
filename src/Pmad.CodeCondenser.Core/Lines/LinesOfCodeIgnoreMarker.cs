using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pmad.CodeCondenser.Core.Lines;

/// <summary>
/// Detects markers used to suppress or ignore lines of code (LOC) checks on methods and classes.
/// </summary>
public static class LinesOfCodeIgnoreMarker
{
    private static readonly string[] CommentMarkerPatterns =
    [
        "codecondenser:ignore-loc",
        "codecondenser:ignore-lines",
        "codecondenser:ignore-length",
        "codecondenser:ignore-method-length",
        "codecondenser:ignore-class-length",
        "codecondenser:ignore",
        "nocondense",
        "noloc"
    ];

    private static readonly string[] AttributeNamePatterns =
    [
        "CodeCondenserIgnore",
        "LinesOfCodeIgnore",
        "IgnoreLinesOfCode",
        "IgnoreMethodLength",
        "IgnoreClassLength"
    ];

    /// <summary>
    /// Checks if a member declaration (method, constructor, class, record) is ignored.
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

        // 2. Check comments inside method / constructor body if applicable
        if (member is BaseMethodDeclarationSyntax methodDecl)
        {
            if (methodDecl.Body != null && HasIgnoreCommentTrivia(methodDecl.Body.DescendantTrivia()))
            {
                return true;
            }
            if (methodDecl.ExpressionBody != null && HasIgnoreCommentTrivia(methodDecl.ExpressionBody.DescendantTrivia()))
            {
                return true;
            }
        }

        // 3. Check attributes
        if (HasIgnoreAttribute(member))
        {
            return true;
        }

        // 4. Check enclosing compilation unit / file level trivia
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

    private static bool HasIgnoreAttribute(MemberDeclarationSyntax member)
    {
        SyntaxList<AttributeListSyntax> attributeLists = member switch
        {
            BaseTypeDeclarationSyntax b => b.AttributeLists,
            BaseMethodDeclarationSyntax m => m.AttributeLists,
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
                    bool isLocRule = args.Any(a =>
                    {
                        var text = a.ToString();
                        return text.Contains("LinesOfCode", StringComparison.OrdinalIgnoreCase) ||
                               text.Contains("MethodLength", StringComparison.OrdinalIgnoreCase) ||
                               text.Contains("ClassLength", StringComparison.OrdinalIgnoreCase) ||
                               text.Contains("TypeLength", StringComparison.OrdinalIgnoreCase) ||
                               text.Contains("MemberLength", StringComparison.OrdinalIgnoreCase) ||
                               text.Contains("CodeLength", StringComparison.OrdinalIgnoreCase) ||
                               text.Contains("CC008", StringComparison.OrdinalIgnoreCase) ||
                               text.Contains("CC009", StringComparison.OrdinalIgnoreCase);
                    });

                    if (isCodeCondenser && isLocRule)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool HasIgnoreCommentTrivia(IEnumerable<SyntaxTrivia> triviaList)
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
