namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Represents a method or class whose lines of code (LOC) exceed the configured threshold.
/// </summary>
public sealed record LinesOfCodeViolation
{
    /// <summary>
    /// Relative or absolute file path containing the declaration.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Identifier or signature of the violating declaration (e.g., "ClassName" or "ClassName.MethodName").
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// Syntactic kind of the violating declaration (Method or Class).
    /// </summary>
    public required LinesOfCodeKind Kind { get; init; }

    /// <summary>
    /// Computed pure lines of code (excluding comments and blank lines).
    /// </summary>
    public required int LinesOfCode { get; init; }

    /// <summary>
    /// Configured threshold that was exceeded.
    /// </summary>
    public required int Threshold { get; init; }

    /// <summary>
    /// 1-indexed starting line number of the declaration.
    /// </summary>
    public required int StartLine { get; init; }

    /// <summary>
    /// 1-indexed ending line number of the declaration.
    /// </summary>
    public required int EndLine { get; init; }

    /// <summary>
    /// Category of the containing file (Production or Test).
    /// </summary>
    public required FileCategory Category { get; init; }

    /// <summary>
    /// Gets a unique key identifying this violation across snapshot baselines.
    /// </summary>
    public string Key => $"{FilePath}::{Kind}::{Identifier}";
}
