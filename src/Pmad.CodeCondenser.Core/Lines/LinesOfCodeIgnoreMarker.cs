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
    /// Checks if a syntax node (class, record, method, constructor, property accessor, local function) is ignored.
    /// </summary>
    public static bool IsIgnored(SyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        // 1. Check leading/trailing trivia on node
        if (HasIgnoreCommentTrivia(node.GetLeadingTrivia()) ||
            HasIgnoreCommentTrivia(node.GetTrailingTrivia()))
        {
            return true;
        }

        // 2. Check comments inside method / constructor / accessor / local function body
        var (body, expressionBody) = GetBodyAndExpression(node);
        if (body != null && HasIgnoreCommentTrivia(body.DescendantTrivia()))
        {
            return true;
        }
        if (expressionBody != null && HasIgnoreCommentTrivia(expressionBody.DescendantTrivia()))
        {
            return true;
        }

        // 3. Check attributes on node
        if (HasIgnoreAttribute(GetAttributeLists(node)))
        {
            return true;
        }

        // 4. For accessors, check parent property/indexer/event declaration
        if (node is AccessorDeclarationSyntax accessor)
        {
            var parentMember = accessor.Parent?.Parent;
            if (parentMember != null)
            {
                if (HasIgnoreCommentTrivia(parentMember.GetLeadingTrivia()) ||
                    HasIgnoreCommentTrivia(parentMember.GetTrailingTrivia()) ||
                    HasIgnoreAttribute(GetAttributeLists(parentMember)))
                {
                    return true;
                }
            }
        }

        // 5. For local functions, check enclosing method/member
        if (node is LocalFunctionStatementSyntax)
        {
            var current = node.Parent;
            while (current != null && current is not TypeDeclarationSyntax)
            {
                if (current is BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax or AccessorDeclarationSyntax)
                {
                    if (HasIgnoreCommentTrivia(current.GetLeadingTrivia()) ||
                        HasIgnoreCommentTrivia(current.GetTrailingTrivia()) ||
                        HasIgnoreAttribute(GetAttributeLists(current)))
                    {
                        return true;
                    }
                }
                current = current.Parent;
            }
        }

        // 6. Check enclosing compilation unit / file level trivia
        if (node.SyntaxTree != null)
        {
            var root = node.SyntaxTree.GetRoot();
            if (HasIgnoreCommentTrivia(root.GetLeadingTrivia()))
            {
                return true;
            }
        }

        return false;
    }

    private static (BlockSyntax? Body, ArrowExpressionClauseSyntax? ExpressionBody) GetBodyAndExpression(SyntaxNode node)
    {
        return node switch
        {
            BaseMethodDeclarationSyntax m => (m.Body, m.ExpressionBody),
            LocalFunctionStatementSyntax l => (l.Body, l.ExpressionBody),
            AccessorDeclarationSyntax a => (a.Body, a.ExpressionBody),
            _ => (null, null)
        };
    }

    private static SyntaxList<AttributeListSyntax> GetAttributeLists(SyntaxNode node)
    {
        return node switch
        {
            BaseTypeDeclarationSyntax b => b.AttributeLists,
            BaseMethodDeclarationSyntax m => m.AttributeLists,
            BasePropertyDeclarationSyntax p => p.AttributeLists,
            AccessorDeclarationSyntax a => a.AttributeLists,
            LocalFunctionStatementSyntax l => l.AttributeLists,
            _ => default
        };
    }

    private static bool HasIgnoreAttribute(SyntaxList<AttributeListSyntax> attributeLists)
    {
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
