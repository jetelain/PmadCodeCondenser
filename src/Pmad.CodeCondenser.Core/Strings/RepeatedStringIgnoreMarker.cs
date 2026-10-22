using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pmad.CodeCondenser.Core.Strings;

/// <summary>
/// Detects markers used to suppress or ignore repeated inline string detection.
/// </summary>
public static class RepeatedStringIgnoreMarker
{
    private static readonly string[] CommentMarkerPatterns =
    [
        "codecondenser:ignore-string",
        "codecondenser:ignore-strings",
        "codecondenser:ignore-repeated-string",
        "codecondenser:ignore",
        "nocondense"
    ];

    private static readonly string[] AttributeNamePatterns =
    [
        "CodeCondenserIgnore",
        "NoClone",
        "IgnoreString",
        "IgnoreRepeatedString"
    ];

    /// <summary>
    /// Determines whether the given string literal expression or its enclosing context has an ignore marker.
    /// </summary>
    /// <param name="expression">The string literal expression syntax.</param>
    /// <returns>True if the string literal should be ignored; otherwise, false.</returns>
    public static bool IsIgnored(ExpressionSyntax expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        // 1. Check leading and trailing trivia on the expression itself
        if (HasIgnoreCommentTrivia(expression.GetLeadingTrivia()) ||
            HasIgnoreCommentTrivia(expression.GetTrailingTrivia()))
        {
            return true;
        }

        // 2. Check enclosing statement leading and trailing trivia
        var statement = expression.FirstAncestorOrSelf<StatementSyntax>();
        if (statement != null &&
            (HasIgnoreCommentTrivia(statement.GetLeadingTrivia()) ||
             HasIgnoreCommentTrivia(statement.GetTrailingTrivia())))
        {
            return true;
        }

        // 3. Check enclosing member (method, property, field, etc.)
        var member = expression.FirstAncestorOrSelf<MemberDeclarationSyntax>();
        if (member != null)
        {
            if (HasIgnoreAttribute(member) ||
                HasIgnoreCommentTrivia(member.GetLeadingTrivia()) ||
                HasIgnoreCommentTrivia(member.GetTrailingTrivia()))
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
            MethodDeclarationSyntax m => m.AttributeLists,
            PropertyDeclarationSyntax p => p.AttributeLists,
            ClassDeclarationSyntax c => c.AttributeLists,
            RecordDeclarationSyntax r => r.AttributeLists,
            StructDeclarationSyntax s => s.AttributeLists,
            ConstructorDeclarationSyntax cd => cd.AttributeLists,
            FieldDeclarationSyntax f => f.AttributeLists,
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
                        foreach (var arg in attribute.ArgumentList.Arguments)
                        {
                            var exprText = arg.Expression.ToString();
                            if (exprText.Contains("codecondenser", StringComparison.OrdinalIgnoreCase) ||
                                exprText.Contains("string", StringComparison.OrdinalIgnoreCase) ||
                                exprText.Contains("repeated", StringComparison.OrdinalIgnoreCase) ||
                                exprText.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                                exprText.Contains("duplication", StringComparison.OrdinalIgnoreCase))
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

    private static bool HasIgnoreCommentTrivia(IEnumerable<SyntaxTrivia> triviaEnumerable)
    {
        foreach (var trivia in triviaEnumerable)
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
