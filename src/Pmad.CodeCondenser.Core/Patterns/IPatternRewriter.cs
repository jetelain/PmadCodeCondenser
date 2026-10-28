using Microsoft.CodeAnalysis;
using Pmad.CodeCondenser.Core.Configuration;

namespace Pmad.CodeCondenser.Core.Patterns;

/// <summary>
/// Rewrites syntax trees by applying condensation patterns.
/// </summary>
public interface IPatternRewriter
{
    /// <summary>
    /// Rewrites the syntax tree by replacing matched pattern occurrences with their condensed equivalents.
    /// </summary>
    SyntaxTree Rewrite(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings,
        Version? targetDotNetVersion = null,
        IReadOnlySet<string>? ruleIdsToApply = null);
}
