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
                }
                else if (trivia.Span.Length < _settings.CommentLengthThreshold)
                {
                    continue;
                }

                var text = trivia.ToString();
                if (CommentIgnoreMarker.IsIgnored(text) ||
                    CommentIgnoreMarker.IsPrecedingTriviaIgnored(triviaList, i))
                {
                    continue;
                }

                var trimmed = text.Trim();
                var hash = VerboseComment.ComputeSha256(trimmed);
                var fullTrimmed = trivia.ToFullString().Trim();
                var fullHash = VerboseComment.ComputeSha256(fullTrimmed);

                if (_settings.IgnoredCommentHashes.Contains(hash) ||
                    _settings.IgnoredCommentHashes.Contains(fullHash))
                {
                    continue;
                }

                if (kind == CommentKind.XmlDocumentation)
                {
                    var xmlText = XmlDocumentationParser.ExtractXmlText(text).Trim();
                    var xmlHash = VerboseComment.ComputeSha256(xmlText);
                    if (_settings.IgnoredCommentHashes.Contains(xmlHash))
                    {
                        continue;
                    }
                }

                bool isVerbose = kind == CommentKind.XmlDocumentation
                    ? XmlDocumentationParser.IsVerbose(trivia, text, _settings)
                    : text.Length >= _settings.CommentLengthThreshold;

                if (isVerbose)
                {
                    string leadingWhitespace = string.Empty;
                    if (i > 0 && triviaList[i - 1].IsKind(SyntaxKind.WhitespaceTrivia))
                    {
                        leadingWhitespace = triviaList[i - 1].ToString();
                    }

                    int startLine = 1;
                    int endLine = 1;
                    if (tree != null)
                    {
                        var lineSpan = tree.GetLineSpan(trivia.Span);
                        startLine = lineSpan.StartLinePosition.Line + 1;
                        endLine = lineSpan.EndLinePosition.Line + 1;
                    }

                    _comments.Add(new VerboseComment(
                        FilePath: _filePath,
                        StartLine: startLine,
                        EndLine: endLine,
                        SpanStart: trivia.Span.Start,
                        SpanLength: trivia.Span.Length,
                        Kind: kind,
                        OriginalText: text,
                        LeadingWhitespace: leadingWhitespace));
                }
            }
        }
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
