using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Comments;

/// <summary>
/// Syntax rewriter that replaces verbose comments with summarized versions while preserving
/// leading indentation, trailing newlines, and surrounding trivia.
/// </summary>
public sealed class VerboseCommentRewriter : CSharpSyntaxRewriter, ICommentRewriter
{
    private readonly IReadOnlyDictionary<string, string> _replacements;
    private readonly List<string> _appliedTriviaTexts = new();

    /// <summary>
    /// Gets the list of rewritten trivia texts applied during syntax tree rewriting.
    /// </summary>
    public IReadOnlyList<string> AppliedTriviaTexts => _appliedTriviaTexts;

    /// <summary>
    /// Initializes a new instance of <see cref="VerboseCommentRewriter"/> with empty replacements.
    /// </summary>
    public VerboseCommentRewriter() : base(visitIntoStructuredTrivia: false)
    {
        _replacements = new Dictionary<string, string>();
    }

    /// <summary>
    /// Initializes a new instance of <see cref="VerboseCommentRewriter"/> with the specified comment replacements.
    /// </summary>
    /// <param name="replacements">Mapping of comment hashes or original text to summarized comment text.</param>
    public VerboseCommentRewriter(IReadOnlyDictionary<string, string> replacements) : base(visitIntoStructuredTrivia: false)
    {
        _replacements = replacements ?? throw new ArgumentNullException(nameof(replacements));
    }

    /// <inheritdoc />
    public SyntaxTree Rewrite(SyntaxTree syntaxTree, IReadOnlyDictionary<string, string> commentReplacements)
    {
        return Rewrite(syntaxTree, commentReplacements, appliedTriviaTexts: null);
    }

    /// <summary>
    /// Rewrites a syntax tree replacing verbose comments, optionally recording the actual rewritten trivia texts.
    /// </summary>
    /// <param name="syntaxTree">The input syntax tree.</param>
    /// <param name="commentReplacements">The comment replacements dictionary.</param>
    /// <param name="appliedTriviaTexts">Optional collection to receive rewritten trivia texts.</param>
    /// <returns>The rewritten syntax tree.</returns>
    public SyntaxTree Rewrite(
        SyntaxTree syntaxTree,
        IReadOnlyDictionary<string, string> commentReplacements,
        ICollection<string>? appliedTriviaTexts)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        ArgumentNullException.ThrowIfNull(commentReplacements);

        if (commentReplacements.Count == 0)
        {
            return syntaxTree;
        }

        var rewriter = new VerboseCommentRewriter(commentReplacements);
        var root = syntaxTree.GetRoot();
        var newRoot = rewriter.Visit(root);

        if (appliedTriviaTexts != null)
        {
            foreach (var text in rewriter._appliedTriviaTexts)
            {
                appliedTriviaTexts.Add(text);
            }
        }

        return CSharpSyntaxTree.Create(
            (CSharpSyntaxNode)newRoot,
            options: (CSharpParseOptions?)syntaxTree.Options,
            path: syntaxTree.FilePath,
            encoding: syntaxTree.Encoding);
    }

    /// <inheritdoc />
    public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
    {
        if (!IsComment(trivia))
        {
            return base.VisitTrivia(trivia);
        }

        var text = trivia.ToString();
        var trimmed = text.Trim();
        var hash = ComputeSha256(trimmed);

        if (!_replacements.TryGetValue(hash, out var replacement) &&
            !_replacements.TryGetValue(text, out replacement) &&
            !_replacements.TryGetValue(trimmed, out replacement))
        {
            return base.VisitTrivia(trivia);
        }

        if (string.IsNullOrWhiteSpace(replacement))
        {
            return base.VisitTrivia(trivia);
        }

        var replacementTrivia = CreateReplacementTrivia(trivia, replacement);
        var rewrittenText = replacementTrivia.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(rewrittenText))
        {
            _appliedTriviaTexts.Add(rewrittenText);
        }
        var fullRewrittenText = replacementTrivia.ToFullString().Trim();
        if (!string.IsNullOrWhiteSpace(fullRewrittenText) && !string.Equals(rewrittenText, fullRewrittenText, StringComparison.Ordinal))
        {
            _appliedTriviaTexts.Add(fullRewrittenText);
        }

        return replacementTrivia;
    }

    private static SyntaxTrivia CreateReplacementTrivia(SyntaxTrivia originalTrivia, string replacementText)
    {
        if (string.IsNullOrWhiteSpace(replacementText))
        {
            return originalTrivia;
        }

        var kind = originalTrivia.Kind();
        var lineEnding = originalTrivia.ToFullString().Contains("\r\n") ? "\r\n" : "\n";

        switch (kind)
        {
            case SyntaxKind.SingleLineCommentTrivia:
                return CreateSingleLineCommentTrivia(replacementText);

            case SyntaxKind.MultiLineCommentTrivia:
                return CreateMultiLineCommentTrivia(replacementText);

            case SyntaxKind.SingleLineDocumentationCommentTrivia:
            case SyntaxKind.MultiLineDocumentationCommentTrivia:
                return CreateXmlDocumentationTrivia(originalTrivia, replacementText, lineEnding);

            default:
                return originalTrivia;
        }
    }

    private static SyntaxTrivia CreateSingleLineCommentTrivia(string replacementText)
    {
        var trimmed = replacementText.Trim();

        if (trimmed.StartsWith("/*") && trimmed.EndsWith("*/"))
        {
            return SyntaxFactory.Comment(trimmed);
        }

        if (trimmed.Contains('\n') || trimmed.Contains('\r'))
        {
            var sanitized = trimmed.Replace("*/", "* /");
            return SyntaxFactory.Comment($"/* {sanitized} */");
        }

        if (trimmed.StartsWith("//"))
        {
            return SyntaxFactory.Comment(trimmed);
        }

        return SyntaxFactory.Comment($"// {trimmed}");
    }

    private static SyntaxTrivia CreateMultiLineCommentTrivia(string replacementText)
    {
        var trimmed = replacementText.Trim();

        if (trimmed.StartsWith("/*") && trimmed.EndsWith("*/"))
        {
            return SyntaxFactory.Comment(trimmed);
        }

        var sanitized = trimmed.Replace("*/", "* /");
        return SyntaxFactory.Comment($"/* {sanitized} */");
    }

    private static SyntaxTrivia CreateXmlDocumentationTrivia(SyntaxTrivia originalTrivia, string replacementText, string lineEnding)
    {
        string indentation = GetIndentation(originalTrivia);
        var lines = XmlDocumentationFormatter.FormatLines(replacementText, indentation, XmlDocumentationFormatter.DefaultMaxLineLength);

        var formattedLines = new List<string>(lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            // The first line will already have leading indentation from the token's preceding whitespace trivia.
            // Subsequent lines must have indentation prefixed inside the doc comment structure.
            if (i > 0 && !string.IsNullOrEmpty(indentation))
            {
                line = indentation + line;
            }

            formattedLines.Add(line);
        }

        var fullDocText = string.Join(lineEnding, formattedLines) + lineEnding;
        var parsedList = SyntaxFactory.ParseLeadingTrivia(fullDocText);

        var docTrivia = parsedList.FirstOrDefault(t =>
            t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
            t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

        if (docTrivia != default)
        {
            return docTrivia;
        }

        return SyntaxFactory.Comment($"/* {replacementText.Trim()} */");
    }

    private static string GetIndentation(SyntaxTrivia trivia)
    {
        var token = trivia.Token;
        if (token != default)
        {
            var leading = token.LeadingTrivia;
            int idx = leading.IndexOf(trivia);
            if (idx > 0 && leading[idx - 1].IsKind(SyntaxKind.WhitespaceTrivia))
            {
                return leading[idx - 1].ToString();
            }
        }

        // Fallback: check if original trivia text contains indentation on interior lines
        var text = trivia.ToFullString();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("///"))
            {
                int slashIdx = line.IndexOf("///", StringComparison.Ordinal);
                if (slashIdx > 0)
                {
                    var prefix = line[..slashIdx];
                    if (string.IsNullOrWhiteSpace(prefix))
                    {
                        return prefix;
                    }
                }
            }
        }

        return string.Empty;
    }

    private static bool IsComment(SyntaxTrivia trivia)
    {
        return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
               trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
               trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
               trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
    }

    private static string ComputeSha256(string input) => VerboseComment.ComputeSha256(input);
}
