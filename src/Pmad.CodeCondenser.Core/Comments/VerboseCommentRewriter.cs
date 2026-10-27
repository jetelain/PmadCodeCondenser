using System.Diagnostics.CodeAnalysis;
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
    public override SyntaxToken VisitToken(SyntaxToken token)
    {
        if (_replacements.Count == 0 || (!token.HasLeadingTrivia && !token.HasTrailingTrivia))
        {
            return base.VisitToken(token);
        }

        var leadingTrivia = token.HasLeadingTrivia ? RewriteTriviaList(token.LeadingTrivia) : token.LeadingTrivia;
        var trailingTrivia = token.HasTrailingTrivia ? RewriteTriviaList(token.TrailingTrivia) : token.TrailingTrivia;

        return token.WithLeadingTrivia(leadingTrivia).WithTrailingTrivia(trailingTrivia);
    }

    private SyntaxTriviaList RewriteTriviaList(SyntaxTriviaList triviaList)
    {
        if (triviaList.Count == 0 || _replacements.Count == 0)
        {
            return triviaList;
        }

        var newTrivia = new List<SyntaxTrivia>(triviaList.Count);

        for (int i = 0; i < triviaList.Count; i++)
        {
            var trivia = triviaList[i];

            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                int groupEndIndex = VerboseCommentExtractor.FindContiguousSingleLineCommentEndIndex(triviaList, i);

                if (groupEndIndex > i)
                {
                    var groupText = GetTriviaRangeText(triviaList, i, groupEndIndex);
                    var trimmed = groupText.Trim();
                    var hash = ComputeSha256(trimmed);

                    if (TryGetReplacement(hash, groupText, trimmed, out var replacement))
                    {
                        if (string.IsNullOrWhiteSpace(replacement))
                        {
                            for (int k = i; k <= groupEndIndex; k++)
                            {
                                newTrivia.Add(triviaList[k]);
                            }
                            i = groupEndIndex;
                            continue;
                        }

                        if (CommentVerbosityHelper.IsUselessComment(replacement))
                        {
                            _appliedTriviaTexts.Add(groupText);
                            _appliedTriviaTexts.Add(trimmed);
                            i = groupEndIndex;
                            continue;
                        }

                        var replacementTrivia = CreateSingleLineCommentTrivia(replacement);
                        RecordAppliedText(replacementTrivia);
                        newTrivia.Add(replacementTrivia);
                        i = groupEndIndex;
                        continue;
                    }
                }
            }

            if (IsComment(trivia))
            {
                var text = trivia.ToString();
                var trimmed = text.Trim();
                var hash = ComputeSha256(trimmed);

                if (TryGetReplacement(hash, text, trimmed, out var replacement))
                {
                    if (string.IsNullOrWhiteSpace(replacement))
                    {
                        newTrivia.Add(trivia);
                        continue;
                    }

                    if (CommentVerbosityHelper.IsUselessComment(replacement))
                    {
                        _appliedTriviaTexts.Add(text);
                        continue;
                    }

                    var replacementTrivia = CreateReplacementTrivia(trivia, replacement);
                    RecordAppliedText(replacementTrivia);
                    newTrivia.Add(replacementTrivia);
                    continue;
                }
            }

            newTrivia.Add(trivia);
        }

        return SyntaxFactory.TriviaList(newTrivia);
    }

    private bool TryGetReplacement(string hash, string text, string trimmed, [NotNullWhen(true)] out string? replacement)
    {
        if (_replacements.TryGetValue(hash, out replacement) ||
            _replacements.TryGetValue(text, out replacement) ||
            _replacements.TryGetValue(trimmed, out replacement))
        {
            return true;
        }

        if (trimmed.Contains('\r') || trimmed.Contains('\n'))
        {
            var lfTrimmed = trimmed.Replace("\r\n", "\n");
            var lfHash = ComputeSha256(lfTrimmed);
            if (_replacements.TryGetValue(lfHash, out replacement) ||
                _replacements.TryGetValue(lfTrimmed, out replacement))
            {
                return true;
            }

            var crlfTrimmed = lfTrimmed.Replace("\n", "\r\n");
            var crlfHash = ComputeSha256(crlfTrimmed);
            if (_replacements.TryGetValue(crlfHash, out replacement) ||
                _replacements.TryGetValue(crlfTrimmed, out replacement))
            {
                return true;
            }
        }

        replacement = null;
        return false;
    }

    private static string GetTriviaRangeText(SyntaxTriviaList triviaList, int startIndex, int endIndex)
    {
        if (startIndex == endIndex)
        {
            return triviaList[startIndex].ToString();
        }

        var sb = new StringBuilder();
        for (int k = startIndex; k <= endIndex; k++)
        {
            sb.Append(triviaList[k].ToFullString());
        }
        return sb.ToString();
    }

    private void RecordAppliedText(SyntaxTrivia replacementTrivia)
    {
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

        if (!TryGetReplacement(hash, text, trimmed, out var replacement) ||
            string.IsNullOrWhiteSpace(replacement))
        {
            return base.VisitTrivia(trivia);
        }

        if (CommentVerbosityHelper.IsUselessComment(replacement))
        {
            _appliedTriviaTexts.Add(text);
            return default;
        }

        var replacementTrivia = CreateReplacementTrivia(trivia, replacement);
        RecordAppliedText(replacementTrivia);

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

        if (trimmed.StartsWith("/*", StringComparison.Ordinal) && trimmed.EndsWith("*/", StringComparison.Ordinal))
        {
            return SyntaxFactory.Comment(trimmed);
        }

        if (trimmed.Contains('\n') || trimmed.Contains('\r'))
        {
            var sanitized = trimmed.Replace("*/", "* /");
            return SyntaxFactory.Comment($"/* {sanitized} */");
        }

        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return SyntaxFactory.Comment(trimmed);
        }

        return SyntaxFactory.Comment($"// {trimmed}");
    }

    private static SyntaxTrivia CreateMultiLineCommentTrivia(string replacementText)
    {
        var trimmed = replacementText.Trim();

        if (trimmed.StartsWith("/*", StringComparison.Ordinal) && trimmed.EndsWith("*/", StringComparison.Ordinal))
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
