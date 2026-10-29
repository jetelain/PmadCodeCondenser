using Microsoft.CodeAnalysis;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Patterns;

/// <summary>
/// Detects condensation pattern candidates across syntax trees.
/// </summary>
public interface IPatternDetector
{
    /// <summary>
    /// Analyzes a syntax tree and returns all detected condensation pattern candidates.
    /// </summary>
    IReadOnlyList<CondensationCandidate> DetectCandidates(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings,
        Version? targetDotNetVersion = null,
        string? projectId = null);
}
