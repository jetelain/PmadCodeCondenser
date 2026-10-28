using Microsoft.CodeAnalysis;

namespace Pmad.CodeCondenser.Core.Patterns;

/// <summary>
/// Contains details of a matched condensation pattern on a specific syntax node.
/// </summary>
public sealed class CondensationMatch
{
    /// <summary>
    /// The original syntax node identified for condensation.
    /// </summary>
    public required SyntaxNode Node { get; init; }

    /// <summary>
    /// Suggested replacement code text as it would appear in source code.
    /// </summary>
    public required string SuggestedCode { get; init; }

    /// <summary>
    /// Optional explanatory note or rationale for the condensation.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Optional pre-constructed replacement syntax node to avoid re-generating it during rewriting.
    /// </summary>
    public SyntaxNode? ReplacementNode { get; init; }

    /// <summary>
    /// Optional captured properties or sub-nodes for custom rewriting logic.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Properties { get; init; }
}
