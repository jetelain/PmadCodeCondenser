using Microsoft.CodeAnalysis;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Strings;

/// <summary>
/// Interface for detecting and grouping repeated inline string literals.
/// </summary>
public interface IRepeatedStringDetector
{
    /// <summary>
    /// Extracts qualifying string literal occurrences from a syntax tree.
    /// </summary>
    /// <param name="syntaxTree">The parsed C# syntax tree.</param>
    /// <param name="filePath">The file path of the syntax tree.</param>
    /// <param name="settings">Configuration settings.</param>
    /// <param name="projectId">Optional project identifier.</param>
    /// <param name="fileCategory">Category of the source file.</param>
    /// <returns>A list of string literal occurrences.</returns>
    IReadOnlyList<RepeatedStringOccurrence> ExtractStringLiterals(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings,
        string? projectId = null,
        FileCategory fileCategory = FileCategory.Production);

    /// <summary>
    /// Groups string literal occurrences by content and filters to those meeting the repetition criteria.
    /// </summary>
    /// <param name="occurrences">All extracted string occurrences across the codebase.</param>
    /// <param name="settings">Configuration settings.</param>
    /// <param name="fileClassifier">Optional classifier to determine file category.</param>
    /// <returns>A list of repeated string clusters.</returns>
    IReadOnlyList<RepeatedStringGroup> GroupRepeatedStrings(
        IEnumerable<RepeatedStringOccurrence> occurrences,
        CodeCondenserSettings settings,
        Func<string, FileCategory>? fileClassifier = null);
}
