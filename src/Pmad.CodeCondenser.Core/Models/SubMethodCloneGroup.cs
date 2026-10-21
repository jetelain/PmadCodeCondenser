namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Represents a cluster of duplicate statement sequences identified across methods via sliding-window AST analysis.
/// </summary>
/// <param name="NormalizedHash">Canonical hash identifying the identical normalized statement sequence.</param>
/// <param name="Occurrences">List of code locations where this statement sequence occurs.</param>
/// <param name="StatementCount">Number of consecutive statements in the matching block.</param>
/// <param name="Category">Categorization based on file paths (Production, Test, CrossBoundary).</param>
/// <param name="ImpactScore">Calculated impact score: StatementCount * (Occurrences.Count - 1).</param>
/// <param name="Recommendation">Refactoring guidance for this duplicate block.</param>
public sealed record SubMethodCloneGroup(
    string NormalizedHash,
    IReadOnlyList<SubMethodCloneOccurrence> Occurrences,
    int StatementCount,
    CloneCategory Category = CloneCategory.Production,
    int ImpactScore = 0,
    string? Recommendation = null);
