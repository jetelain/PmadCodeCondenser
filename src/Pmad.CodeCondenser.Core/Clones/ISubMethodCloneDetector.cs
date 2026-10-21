using Microsoft.CodeAnalysis;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Clones;

/// <summary>
/// Detects duplicate statement blocks across methods using sliding-window AST sequence analysis.
/// </summary>
public interface ISubMethodCloneDetector
{
    /// <summary>
    /// Extracts normalized statement sequences from eligible discrete BlockSyntax scopes in a syntax tree.
    /// </summary>
    IReadOnlyList<SubMethodBlock> ExtractBlocks(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings,
        string? projectId = null);

    /// <summary>
    /// Groups extracted blocks by sliding-window sequence hash, expanding contiguous matches and suppressing
    /// self-matches and whole-method subsumed duplicates.
    /// </summary>
    IReadOnlyList<SubMethodCloneGroup> DetectClones(
        IEnumerable<SubMethodBlock> blocks,
        int threshold,
        IReadOnlyList<CodeCloneGroup>? wholeMethodClones = null,
        Func<string, FileCategory>? fileClassifier = null);

    /// <summary>
    /// Extracts blocks and detects sub-method clones across multiple syntax trees.
    /// </summary>
    IReadOnlyList<SubMethodCloneGroup> DetectClones(
        IEnumerable<(SyntaxTree Tree, string FilePath)> syntaxTrees,
        CodeCondenserSettings settings,
        IReadOnlyList<CodeCloneGroup>? wholeMethodClones = null,
        Func<string, FileCategory>? fileClassifier = null);
}
