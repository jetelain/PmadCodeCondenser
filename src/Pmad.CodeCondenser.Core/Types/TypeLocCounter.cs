using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Pmad.CodeCondenser.Core.Types;

/// <summary>
/// Computes pure lines of code (LOC) for a syntax node by excluding all comments and blank lines.
/// </summary>
public static class TypeLocCounter
{
    /// <summary>
    /// Counts non-whitespace lines in the given node, excluding single-line, multi-line, and XML doc comments.
    /// </summary>
    public static int CountLinesOfCode(SyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var sourceText = node.ToFullString();
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return 0;
        }

        var baseOffset = node.FullSpan.Start;
        var commentSpans = new List<TextSpan>();

        foreach (var trivia in node.DescendantTrivia(descendIntoTrivia: false))
        {
            if (IsComment(trivia))
            {
                var relStart = trivia.FullSpan.Start - baseOffset;
                var relLen = trivia.FullSpan.Length;
                if (relStart >= 0 && relStart + relLen <= sourceText.Length)
                {
                    commentSpans.Add(new TextSpan(relStart, relLen));
                }
            }
        }

        string codeOnlyText;
        if (commentSpans.Count == 0)
        {
            codeOnlyText = sourceText;
        }
        else
        {
            commentSpans.Sort((a, b) => a.Start.CompareTo(b.Start));
            var sb = new System.Text.StringBuilder(sourceText.Length);
            int lastIndex = 0;
            foreach (var span in commentSpans)
            {
                if (span.Start > lastIndex)
                {
                    sb.Append(sourceText, lastIndex, span.Start - lastIndex);
                }
                lastIndex = span.End;
            }
            if (lastIndex < sourceText.Length)
            {
                sb.Append(sourceText, lastIndex, sourceText.Length - lastIndex);
            }
            codeOnlyText = sb.ToString();
        }

        int count = 0;
        using var reader = new StringReader(codeOnlyText);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsComment(SyntaxTrivia trivia)
    {
        return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
               trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
               trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
               trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
    }
}
