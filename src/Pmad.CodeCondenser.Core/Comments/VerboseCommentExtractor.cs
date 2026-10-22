using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Filtering;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Comments;

/// <summary>
/// Syntax walker that extracts verbose comments (single-line, multi-line, and XML documentation)
/// exceeding a specified character length threshold.
/// </summary>
public sealed class VerboseCommentExtractor : CSharpSyntaxWalker, ICommentExtractor
{
    private SyntaxTree? _syntaxTree;
    private string _filePath;
    private CodeCondenserSettings _settings;
    private readonly List<VerboseComment> _comments = new();

    /// <summary>
    /// Initializes a new instance of <see cref="VerboseCommentExtractor"/> with default settings.
    /// </summary>
    public VerboseCommentExtractor() : base(SyntaxWalkerDepth.Token)
    {
        _filePath = string.Empty;
        _settings = new CodeCondenserSettings();
    }

    /// <summary>
    /// Initializes a new instance of <see cref="VerboseCommentExtractor"/> for a specific syntax tree.
    /// </summary>
    /// <param name="syntaxTree">The syntax tree to extract comments from.</param>
    /// <param name="filePath">The file path associated with the syntax tree.</param>
    /// <param name="settings">The code condenser settings.</param>
    public VerboseCommentExtractor(SyntaxTree syntaxTree, string filePath, CodeCondenserSettings settings)
        : base(SyntaxWalkerDepth.Token)
    {
        _syntaxTree = syntaxTree ?? throw new ArgumentNullException(nameof(syntaxTree));
        _filePath = filePath ?? syntaxTree.FilePath ?? string.Empty;
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Gets the list of extracted verbose comments.
    /// </summary>
    public IReadOnlyList<VerboseComment> Comments => _comments;

    /// <inheritdoc />
    public IReadOnlyList<VerboseComment> Extract(SyntaxTree syntaxTree, string filePath, CodeCondenserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        ArgumentNullException.ThrowIfNull(settings);

        var path = filePath ?? syntaxTree.FilePath ?? string.Empty;
        var extractor = new VerboseCommentExtractor(syntaxTree, path, settings);
        extractor.Visit(syntaxTree.GetRoot());
        return extractor.Comments;
    }

    /// <inheritdoc />
    public override void VisitToken(SyntaxToken token)
    {
        if (!token.HasLeadingTrivia && !token.HasTrailingTrivia)
        {
            base.VisitToken(token);
            return;
        }

        if (_settings.ExcludeGeneratedCode && GeneratedCodeDetector.HasGeneratedCodeAttribute(token.Parent))
        {
            base.VisitToken(token);
            return;
        }

        if (token.HasLeadingTrivia)
        {
            ProcessTriviaList(token.LeadingTrivia, token.SyntaxTree);
        }

        base.VisitToken(token);

        if (token.HasTrailingTrivia)
        {
            ProcessTriviaList(token.TrailingTrivia, token.SyntaxTree);
        }
    }

    private void ProcessTriviaList(SyntaxTriviaList triviaList, SyntaxTree? tokenTree)
    {
        var tree = _syntaxTree ?? tokenTree;

        for (int i = 0; i < triviaList.Count; i++)
        {
            var trivia = triviaList[i];
            if (TryGetCommentKind(trivia, out var kind))
            {
                if (kind == CommentKind.XmlDocumentation)
                {
                    if (!_settings.IncludeXmlDocumentation)
                    {
                        continue;
                    }

                    var minXmlThreshold = Math.Min(
                        Math.Min(_settings.GetEffectiveXmlSummaryThreshold(), _settings.GetEffectiveXmlRemarksThreshold()),
                        _settings.GetEffectiveXmlParamThreshold());

                    if (trivia.Span.Length < minXmlThreshold)
                    {
                        continue;
                    }

                    var text = trivia.ToString();
                    if (CommentIgnoreMarker.IsIgnored(text) ||
                        CommentIgnoreMarker.IsPrecedingTriviaIgnored(triviaList, i))
                    {
                        continue;
                    }

                    if (CommentHashHelper.IsCommentIgnored(trivia, text, kind, _settings.IgnoredCommentHashes))
                    {
                        continue;
                    }

                    if (XmlDocumentationParser.IsVerbose(trivia, text, _settings))
                    {
                        AddComment(trivia, trivia, text, kind, i, triviaList, tree);
                    }
                }
                else if (kind == CommentKind.MultiLine)
                {
                    if (trivia.Span.Length < _settings.CommentLengthThreshold)
                    {
                        continue;
                    }

                    var text = trivia.ToString();
                    if (CommentIgnoreMarker.IsIgnored(text) ||
                        CommentIgnoreMarker.IsPrecedingTriviaIgnored(triviaList, i))
                    {
                        continue;
                    }

                    if (CommentHashHelper.IsCommentIgnored(trivia, text, kind, _settings.IgnoredCommentHashes))
                    {
                        continue;
                    }

                    if (text.Length >= _settings.CommentLengthThreshold)
                    {
                        AddComment(trivia, trivia, text, kind, i, triviaList, tree);
                    }
                }
                else if (kind == CommentKind.SingleLine)
                {
                    if (CommentIgnoreMarker.IsIgnored(trivia.ToString()))
                    {
                        continue;
                    }

                    int groupEndIndex = FindContiguousSingleLineCommentEndIndex(triviaList, i);

                    var firstTrivia = trivia;
                    var lastTrivia = triviaList[groupEndIndex];

                    string text;
                    int spanLength;
                    if (groupEndIndex == i)
                    {
                        text = firstTrivia.ToString();
                        spanLength = firstTrivia.Span.Length;
                    }
                    else
                    {
                        var sb = new StringBuilder();
                        for (int k = i; k <= groupEndIndex; k++)
                        {
                            sb.Append(triviaList[k].ToFullString());
                        }
                        text = sb.ToString();
                        spanLength = lastTrivia.Span.End - firstTrivia.Span.Start;
                    }

                    if (spanLength < _settings.CommentLengthThreshold)
                    {
                        i = groupEndIndex;
                        continue;
                    }

                    if (CommentIgnoreMarker.IsIgnored(text) ||
                        CommentIgnoreMarker.IsPrecedingTriviaIgnored(triviaList, i))
                    {
                        i = groupEndIndex;
                        continue;
                    }

                    if (CommentHashHelper.IsCommentIgnored(firstTrivia, text, kind, _settings.IgnoredCommentHashes))
                    {
                        i = groupEndIndex;
                        continue;
                    }

                    if (text.Length >= _settings.CommentLengthThreshold)
                    {
                        AddComment(firstTrivia, lastTrivia, text, kind, i, triviaList, tree);
                    }

                    i = groupEndIndex;
                }
            }
        }
    }

    private void AddComment(
        SyntaxTrivia firstTrivia,
        SyntaxTrivia lastTrivia,
        string text,
        CommentKind kind,
        int firstIndex,
        SyntaxTriviaList triviaList,
        SyntaxTree? tree)
    {
        string leadingWhitespace = string.Empty;
        if (firstIndex > 0 && triviaList[firstIndex - 1].IsKind(SyntaxKind.WhitespaceTrivia))
        {
            leadingWhitespace = triviaList[firstIndex - 1].ToString();
        }

        int startLine = 1;
        int endLine = 1;
        if (tree != null)
        {
            var startSpan = tree.GetLineSpan(firstTrivia.Span);
            var endSpan = tree.GetLineSpan(lastTrivia.Span);
            startLine = startSpan.StartLinePosition.Line + 1;
            endLine = endSpan.EndLinePosition.Line + 1;
        }

        _comments.Add(new VerboseComment(
            FilePath: _filePath,
            StartLine: startLine,
            EndLine: endLine,
            SpanStart: firstTrivia.Span.Start,
            SpanLength: lastTrivia.Span.End - firstTrivia.Span.Start,
            Kind: kind,
            OriginalText: text,
            LeadingWhitespace: leadingWhitespace));
    }

    /// <summary>
    /// Finds the ending index of a contiguous group of single-line comments in a trivia list,
    /// where consecutive single-line comments are separated only by a newline and optional indentation.
    /// </summary>
    internal static int FindContiguousSingleLineCommentEndIndex(SyntaxTriviaList triviaList, int startIndex)
    {
        int lastCommentIndex = startIndex;
        int current = startIndex;

        while (current + 1 < triviaList.Count)
        {
            int next = current + 1;
            if (!triviaList[next].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                break;
            }

            next++;

            if (next < triviaList.Count && triviaList[next].IsKind(SyntaxKind.WhitespaceTrivia))
            {
                next++;
            }

            if (next < triviaList.Count && triviaList[next].IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                lastCommentIndex = next;
                current = next;
            }
            else
            {
                break;
            }
        }

        return lastCommentIndex;
    }

    private static bool TryGetCommentKind(SyntaxTrivia trivia, out CommentKind kind)
    {
        switch (trivia.Kind())
        {
            case SyntaxKind.SingleLineCommentTrivia:
                kind = CommentKind.SingleLine;
                return true;

            case SyntaxKind.MultiLineCommentTrivia:
                kind = CommentKind.MultiLine;
                return true;

            case SyntaxKind.SingleLineDocumentationCommentTrivia:
            case SyntaxKind.MultiLineDocumentationCommentTrivia:
                kind = CommentKind.XmlDocumentation;
                return true;

            default:
                kind = default;
                return false;
        }
    }
}
