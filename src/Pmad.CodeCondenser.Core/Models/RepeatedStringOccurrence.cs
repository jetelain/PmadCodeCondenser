namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Represents a single occurrence of an inline string literal in source code.
/// </summary>
public sealed record RepeatedStringOccurrence
{
    /// <summary>
    /// File path where the string literal was located (relative or normalized).
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// The string literal content.
    /// </summary>
    public required string StringValue { get; init; }

    /// <summary>
    /// 1-based start line of the string literal.
    /// </summary>
    public required int StartLine { get; init; }

    /// <summary>
    /// 1-based end line of the string literal.
    /// </summary>
    public required int EndLine { get; init; }

    /// <summary>
    /// 1-based start character/column of the string literal.
    /// </summary>
    public required int StartColumn { get; init; }

    /// <summary>
    /// 1-based end character/column of the string literal.
    /// </summary>
    public required int EndColumn { get; init; }

    /// <summary>
    /// Name of the enclosing member (method, property, constructor) if any.
    /// </summary>
    public string? EnclosingMember { get; init; }

    /// <summary>
    /// Project identifier for boundary analysis.
    /// </summary>
    public string? ProjectId { get; init; }

    /// <summary>
    /// Category of the file containing this occurrence (Production or Test).
    /// </summary>
    public FileCategory Category { get; init; } = FileCategory.Production;

    /// <summary>
    /// Indicates whether this occurrence is the initializer of an existing constant declaration.
    /// </summary>
    public bool IsConstDeclaration { get; init; }
}
