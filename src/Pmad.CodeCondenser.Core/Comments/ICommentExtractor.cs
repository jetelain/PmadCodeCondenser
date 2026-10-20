using Microsoft.CodeAnalysis;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Comments;

/// <summary>
/// Extracts verbose comments from Roslyn syntax trees.
/// </summary>
public interface ICommentExtractor
{
    IReadOnlyList<VerboseComment> Extract(SyntaxTree syntaxTree, string filePath, CodeCondenserSettings settings);
}
