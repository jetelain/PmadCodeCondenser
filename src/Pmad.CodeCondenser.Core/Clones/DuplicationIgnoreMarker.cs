using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pmad.CodeCondenser.Core.Clones;

/// <summary>
/// Detects markers used to suppress or ignore code duplication (structural clone) detection on methods.
/// </summary>
public static class DuplicationIgnoreMarker
{
    private static readonly string[] CommentMarkerPatterns =
    [
        "codecondenser:ignore-duplicate",
        "codecondenser:ignore-duplication",
        "codecondenser:ignore-clone",
        "codecondenser:ignore",
        "no-clone",
        "noclone",
        "nocondense"
    ];

    private static readonly string[] AttributeNamePatterns =
    [
        "CodeCondenserIgnore",
        "NoClone",
        "IgnoreDuplication",
        "IgnoreClone",
        "SuppressDuplication"
    ];

    /// <summary>
    /// Determines whether a method declaration is marked to ignore duplication detection.
    /// Checks attributes, leading comment trivia, and method body comment trivia.
    /// </summary>
    /// <param name="method">The method declaration syntax.</param>
    /// <returns>True if the method is marked to ignore duplication detection; otherwise, false.</returns>
    public static bool IsIgnored(MethodDeclarationSyntax method)
    {
        ArgumentNullException.ThrowIfNull(method);

        // 1. Check attributes
        if (HasIgnoreAttribute(method))
        {
            return true;
        }

        // 2. Check leading trivia comments (above the method or above its attributes)
        if (HasIgnoreCommentTrivia(method.GetLeadingTrivia()))
        {
            return true;
        }

        // 3. Check comments inside method body
        if (method.Body != null && HasIgnoreCommentTrivia(method.Body.DescendantTrivia()))
        {
            return true;
        }

        if (method.ExpressionBody != null && HasIgnoreCommentTrivia(method.ExpressionBody.DescendantTrivia()))
        {
            return true;
        }

        return false;
    }

    private static bool HasIgnoreAttribute(MethodDeclarationSyntax method)
    {
        foreach (var attributeList in method.AttributeLists)
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
                        foreach (var arg in attribute.ArgumentList.Arguments)
                        {
                            var exprText = arg.Expression.ToString();
                            if (exprText.Contains("codecondenser", StringComparison.OrdinalIgnoreCase) ||
                                exprText.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                                exprText.Contains("duplication", StringComparison.OrdinalIgnoreCase) ||
                                exprText.Contains("clone", StringComparison.OrdinalIgnoreCase))
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

    private static bool HasIgnoreCommentTrivia(IEnumerable<Microsoft.CodeAnalysis.SyntaxTrivia> triviaEnumerable)
    {
        foreach (var trivia in triviaEnumerable)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
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
