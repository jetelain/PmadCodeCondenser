using Microsoft.CodeAnalysis;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Predicates;

/// <summary>
/// Defines methods for extracting conditions and grouping predicate clones.
/// </summary>
public interface IPredicateCloneDetector
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

    /// <summary>
    /// Clusters occurrences into clone groups and matches inline conditions against existing helpers.
    /// </summary>
    IReadOnlyList<PredicateCloneGroup> GroupPredicates(
        IEnumerable<PredicateOccurrence> occurrences,
        CodeCondenserSettings settings,
        Func<string, FileCategory>? fileClassifier = null);
}
