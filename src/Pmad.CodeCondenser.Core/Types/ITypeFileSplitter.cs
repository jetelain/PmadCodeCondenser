using Microsoft.CodeAnalysis;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Types;

/// <summary>
/// Service contract for analyzing files for multiple types and splitting them into separate files.
/// </summary>
public interface ITypeFileSplitter
{
    /// <summary>
    /// Analyzes a C# syntax tree to identify top-level types, primary type, and recommended split/rename actions.
    /// </summary>
    TypeFileAnalysis AnalyzeFile(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings,
        FileCategory category = FileCategory.Production);

    /// <summary>
    /// Generates split files and updated primary file content based on the analysis.
    /// </summary>
    SplitResult SplitFile(
        SyntaxTree syntaxTree,
        string filePath,
        TypeFileAnalysis analysis);
}
