using Microsoft.CodeAnalysis;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Clones;

/// <summary>
/// Detects structural code clones across C# syntax trees.
/// </summary>
public interface ICloneDetector
{
    /// <summary>
    /// Extracts methods from a syntax tree meeting the statement count threshold and computes their normalized hashes.
    /// </summary>
    IReadOnlyList<MethodCloneInfo> ExtractMethods(SyntaxTree syntaxTree, string filePath, CodeCondenserSettings settings);

    /// <summary>
    /// Extracts methods from a syntax tree meeting the statement count threshold and computes their normalized hashes,
    /// with an optional pre-resolved project identifier.
    /// </summary>
    IReadOnlyList<MethodCloneInfo> ExtractMethods(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings,
        string? projectId)
        => ExtractMethods(syntaxTree, filePath, settings);

    /// <summary>
    /// Groups extracted methods by normalized hash, filtering for groups containing two or more clones.
    /// </summary>
    IReadOnlyList<CodeCloneGroup> GroupClones(IEnumerable<MethodCloneInfo> methods);

    /// <summary>
    /// Groups extracted methods by normalized hash with optional file classification into categories.
    /// </summary>
    IReadOnlyList<CodeCloneGroup> GroupClones(IEnumerable<MethodCloneInfo> methods, Func<string, FileCategory>? fileClassifier)
        => GroupClones(methods);

    /// <summary>
    /// Scans multiple syntax trees and groups duplicates across the entire set.
    /// </summary>
    IReadOnlyList<CodeCloneGroup> DetectClones(IEnumerable<(SyntaxTree Tree, string FilePath)> syntaxTrees, CodeCondenserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(syntaxTrees);
        ArgumentNullException.ThrowIfNull(settings);

        var allMethods = new List<MethodCloneInfo>();
        foreach (var (tree, path) in syntaxTrees)
        {
            allMethods.AddRange(ExtractMethods(tree, path, settings));
        }

        return GroupClones(allMethods);
    }

    /// <summary>
    /// Extracts discrete block scopes for sub-method sliding-window clone detection.
    /// </summary>
    IReadOnlyList<SubMethodBlock> ExtractSubMethodBlocks(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings,
        string? projectId = null)
        => [];

    /// <summary>
    /// Groups extracted blocks by sliding-window sequence hash, expanding contiguous matches.
    /// </summary>
    IReadOnlyList<SubMethodCloneGroup> GroupSubMethodClones(
        IEnumerable<SubMethodBlock> blocks,
        int threshold,
        IReadOnlyList<CodeCloneGroup>? wholeMethodClones = null,
        Func<string, FileCategory>? fileClassifier = null)
        => [];

    /// <summary>
    /// Scans multiple syntax trees for sub-method clones.
    /// </summary>
    IReadOnlyList<SubMethodCloneGroup> DetectSubMethodClones(
        IEnumerable<(SyntaxTree Tree, string FilePath)> syntaxTrees,
        CodeCondenserSettings settings,
        IReadOnlyList<CodeCloneGroup>? wholeMethodClones = null,
        Func<string, FileCategory>? fileClassifier = null)
        => [];
}
