namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Categorizes condensation pattern rules and candidate replacements.
/// </summary>
public enum PatternCategory
{
    /// <summary>
    /// Text and string manipulation patterns (e.g. ReplaceLineEndings, Trim, Contains).
    /// </summary>
    Text,

    /// <summary>
    /// Predicate and boolean condition patterns (e.g. IsNullOrWhiteSpace, redundant null checks).
    /// </summary>
    Predicate,

    /// <summary>
    /// LINQ and collection simplification patterns (e.g. Where followed by Count/Any).
    /// </summary>
    Linq,

    /// <summary>
    /// Argument validation and guard clauses (e.g. ArgumentNullException.ThrowIfNull).
    /// </summary>
    Guard,

    /// <summary>
    /// Modern C# language idioms (e.g. pattern matching, null coalescing, collection expressions).
    /// </summary>
    Language
}
