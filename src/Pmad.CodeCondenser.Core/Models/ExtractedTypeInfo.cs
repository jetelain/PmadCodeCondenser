namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Information about a type declaration detected within a source file.
/// </summary>
public sealed class ExtractedTypeInfo
{
    /// <summary>
    /// The unqualified name of the type (e.g. "MyClass", "IMyInterface").
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// The kind of type declaration (e.g. "class", "interface", "struct", "record", "enum", "delegate").
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// The generic arity (number of generic type parameters). 0 for non-generic types.
    /// </summary>
    public int Arity { get; init; }

    /// <summary>
    /// Pure lines of code for this type declaration (excluding comments and blank lines).
    /// </summary>
    public int LinesOfCode { get; init; }

    /// <summary>
    /// 1-based start line number of the type in the source file.
    /// </summary>
    public int StartLine { get; init; }

    /// <summary>
    /// 1-based end line number of the type in the source file.
    /// </summary>
    public int EndLine { get; init; }

    /// <summary>
    /// The suggested file name for this type (e.g. "MyClass.cs").
    /// </summary>
    public required string TargetFileName { get; init; }

    /// <summary>
    /// True if this type is considered the primary type for the source file.
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    /// True if this type is within the line-of-code tolerance and allowed to remain in the file.
    /// </summary>
    public bool IsTolerated { get; init; }

    /// <summary>
    /// True if the type is explicitly suppressed via attribute or comment marker.
    /// </summary>
    public bool IsSuppressed { get; init; }
}
