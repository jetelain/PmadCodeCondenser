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
    /// Groups extracted methods by normalized hash, filtering for groups containing two or more clones.
    /// </summary>
    IReadOnlyList<CodeCloneGroup> GroupClones(IEnumerable<MethodCloneInfo> methods);

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
}
