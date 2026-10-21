namespace Pmad.CodeCondenser.Core.Clones;

/// <summary>
/// Represents a normalized statement sequence extracted from a discrete BlockSyntax scope within a method.
/// </summary>
public sealed class SubMethodBlock
{
    /// <summary>
    /// Relative or absolute path to the source file containing this block.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Name of the method containing this block scope.
    /// </summary>
    public required string MethodName { get; init; }

    /// <summary>
    /// Identifier distinguishing this block within the method (e.g. "body", "block:24").
    /// </summary>
    public required string BlockScopeId { get; init; }

    /// <summary>
    /// Project identifier for cross-project boundary detection.
    /// </summary>
    public required string ProjectId { get; init; }

    /// <summary>
    /// 1-based start line number of the containing method declaration.
    /// </summary>
    public int MethodStartLine { get; init; }

    /// <summary>
    /// 64-bit normalized statement hashes (computed via System.IO.Hashing.XxHash64).
    /// </summary>
    public required ulong[] StatementHashes { get; init; }

    /// <summary>
    /// 1-based start line number for each statement in the block.
    /// </summary>
    public required int[] StatementStartLines { get; init; }

    /// <summary>
    /// 1-based end line number for each statement in the block.
    /// </summary>
    public required int[] StatementEndLines { get; init; }

    /// <summary>
    /// Total number of filtered statements in this block.
    /// </summary>
    public int StatementCount => StatementHashes.Length;
}
