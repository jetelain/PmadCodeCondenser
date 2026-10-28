using Microsoft.CodeAnalysis;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Predicates;

/// <summary>
/// Extracts candidate condition expressions and helper declarations from syntax trees.
/// </summary>
public interface IPredicateExtractor
{
    /// <summary>
    /// Extracts all candidate condition occurrences and helper declarations from a syntax tree.
    /// </summary>
    IReadOnlyList<PredicateOccurrence> ExtractPredicates(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings,
        string? projectId = null,
        FileCategory fileCategory = FileCategory.Production);
}
