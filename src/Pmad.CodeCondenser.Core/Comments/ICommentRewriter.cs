using Microsoft.CodeAnalysis;

namespace Pmad.CodeCondenser.Core.Comments;

/// <summary>
/// Rewrites a Roslyn syntax tree by replacing original comment trivia with summarized comments.
/// </summary>
public interface ICommentRewriter
{
    SyntaxTree Rewrite(SyntaxTree syntaxTree, IReadOnlyDictionary<string, string> commentReplacements);

    SyntaxTree Rewrite(
        SyntaxTree syntaxTree,
        IReadOnlyDictionary<string, string> commentReplacements,
        ICollection<string>? appliedTriviaTexts) =>
        Rewrite(syntaxTree, commentReplacements);
}

