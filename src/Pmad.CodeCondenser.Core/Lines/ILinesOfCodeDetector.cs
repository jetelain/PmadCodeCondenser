using Microsoft.CodeAnalysis;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Lines;

/// <summary>
/// Defines the contract for detecting methods and classes that exceed configured lines of code (LOC) thresholds.
/// </summary>
public interface ILinesOfCodeDetector
{
    /// <summary>
    /// Analyzes a syntax tree and extracts all method and class declarations exceeding configured LOC thresholds.
    /// </summary>
    /// <param name="syntaxTree">The parsed C# syntax tree.</param>
    /// <param name="relativePath">Relative file path for reporting.</param>
    /// <param name="settings">The active configuration settings containing LOC thresholds.</param>
    /// <param name="category">The file category (Production or Test).</param>
    /// <returns>A read-only list of detected lines of code violations.</returns>
    IReadOnlyList<LinesOfCodeViolation> DetectViolations(
        SyntaxTree syntaxTree,
        string relativePath,
        CodeCondenserSettings settings,
        FileCategory category = FileCategory.Production);
}
