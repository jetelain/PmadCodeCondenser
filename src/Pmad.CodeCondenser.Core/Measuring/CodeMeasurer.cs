using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Pmad.AI.Tokens;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Measuring;

/// <summary>
/// Measures source code and comments in terms of estimated LLM tokens using Roslyn AST trivia analysis
/// and <see cref="HeuristicTokenEstimator"/>.
/// </summary>
public sealed class CodeMeasurer : ICodeMeasurer
{
    /// <inheritdoc />
    public FileMeasurement MeasureFile(
        string filePath,
        FileCategory category,
        string sourceText,
        HeuristicTokenEstimator estimator)
    {
        ArgumentNullException.ThrowIfNull(estimator);
        sourceText ??= string.Empty;

        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return new FileMeasurement
            {
                FilePath = filePath,
                Category = category,
                CodeTokens = 0,
                CommentTokens = 0,
                TotalTokens = 0
            };
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, path: filePath);
        var root = syntaxTree.GetRoot();

        var commentSpans = new List<TextSpan>();
        var commentTexts = new List<string>();

        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: false))
        {
            if (IsComment(trivia))
            {
                commentSpans.Add(trivia.FullSpan);
                commentTexts.Add(trivia.ToFullString());
            }
        }



        int commentTokens = 0;
        if (commentTexts.Count > 0)
        {
            commentTokens = estimator.Estimate(commentTexts);
        }

        int codeTokens;
        if (commentSpans.Count == 0)
        {
            codeTokens = estimator.Estimate(sourceText.AsSpan());
        }
        else
        {
            commentSpans.Sort((a, b) => a.Start.CompareTo(b.Start));
            var sb = new StringBuilder(sourceText.Length);
            int lastIndex = 0;

            foreach (var span in commentSpans)
            {
                if (span.Start > lastIndex)
                {
                    sb.Append(sourceText, lastIndex, span.Start - lastIndex);
                }

                // If comment was inline between tokens without whitespace, e.g. int/*c*/x,
                // insert a space so tokens don't merge into intx
                if (sb.Length > 0 && !char.IsWhiteSpace(sb[^1]) && span.End < sourceText.Length && !char.IsWhiteSpace(sourceText[span.End]))
                {
                    sb.Append(' ');
                }

                lastIndex = span.End;
            }

            if (lastIndex < sourceText.Length)
            {
                sb.Append(sourceText, lastIndex, sourceText.Length - lastIndex);
            }

            var codeText = sb.ToString();
            codeTokens = codeText.AsSpan().IsWhiteSpace() ? 0 : estimator.Estimate(codeText.AsSpan());
        }

        int totalTokens = codeTokens + commentTokens;

        return new FileMeasurement
        {
            FilePath = filePath,
            Category = category,
            CodeTokens = codeTokens,
            CommentTokens = commentTokens,
            TotalTokens = totalTokens
        };
    }

    private static bool IsComment(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
        trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
        trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
        trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
}
