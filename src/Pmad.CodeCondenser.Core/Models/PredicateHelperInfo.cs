namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Metadata describing an existing boolean helper method or property in the codebase.
/// </summary>
public sealed record PredicateHelperInfo
{
    /// <summary>
    /// Identifier name of the helper method or property.
    /// </summary>
    public required string HelperName { get; init; }

    /// <summary>
    /// Name of the enclosing type (class, record, struct, interface).
    /// </summary>
    public required string DeclaringTypeName { get; init; }

    /// <summary>
    /// Indicates whether the helper is declared as static.
    /// </summary>
    public bool IsStatic { get; init; }

    /// <summary>
    /// Indicates whether the helper is a property instead of a method.
    /// </summary>
    public bool IsProperty { get; init; }

    /// <summary>
    /// Indicates whether the helper has private accessibility.
    /// </summary>
    public bool IsPrivate { get; init; }

    /// <summary>
    /// Ordered parameter names accepted by the helper method.
    /// </summary>
    public IReadOnlyList<string> Parameters { get; init; } = [];

    /// <summary>
    /// Ordered parameter type names accepted by the helper method.
    /// </summary>
    public IReadOnlyList<string> ParameterTypes { get; init; } = [];

    /// <summary>
    /// Accessibility keyword (public, internal, private, protected).
    /// </summary>
    public string Accessibility { get; init; } = "public";

    /// <summary>
    /// Return type of the helper (bool, Task&lt;bool&gt;, etc.).
    /// </summary>
    public string? ReturnType { get; init; }

    /// <summary>
    /// Enclosing namespace of the declaring type.
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// Indicates whether the helper declaration body was directly negated (!inner).
    /// </summary>
    public bool IsBodyNegated { get; init; }
}
