using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Pmad.CodeCondenser.Core.Comments;

/// <summary>
/// Syntax rewriter that replaces verbose comments with summarized versions while preserving
/// leading indentation, trailing newlines, and surrounding trivia.
/// </summary>
public sealed class VerboseCommentRewriter : CSharpSyntaxRewriter, ICommentRewriter
{
    private readonly IReadOnlyDictionary<string, string> _replacements;

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
        ArgumentNullException.ThrowIfNull(syntaxTree);
        ArgumentNullException.ThrowIfNull(commentReplacements);

        if (commentReplacements.Count == 0)
        {
            return syntaxTree;
        }

        var rewriter = new VerboseCommentRewriter(commentReplacements);
        var root = syntaxTree.GetRoot();
        var newRoot = rewriter.Visit(root);

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

        return CreateReplacementTrivia(trivia, replacement);
    }

    private static SyntaxTrivia CreateReplacementTrivia(SyntaxTrivia originalTrivia, string replacementText)
    {
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

        var lines = replacementText.Replace("\r\n", "\n").Split('\n');
        var formattedLines = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) && i == lines.Length - 1)
            {
                continue;
            }

            if (!line.StartsWith("///"))
            {
                line = "/// " + line;
            }

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

        return string.Empty;
    }

    private static bool IsComment(SyntaxTrivia trivia)
    {
        return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
               trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
               trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
               trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
    }

    private static string ComputeSha256(string input)
    {
        int byteCount = Encoding.UTF8.GetByteCount(input);
        byte[]? rented = null;
        Span<byte> utf8Bytes = byteCount <= 512
            ? stackalloc byte[byteCount]
            : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(byteCount));

        try
        {
            int written = Encoding.UTF8.GetBytes(input, utf8Bytes);
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(utf8Bytes[..written], hash);
            return Convert.ToHexStringLower(hash);
        }
        finally
        {
            if (rented != null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}
