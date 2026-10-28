namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Represents a single occurrence of a candidate condition or predicate in source code.
/// </summary>
public sealed record PredicateOccurrence
{
    /// <summary>
    /// File path where the condition was located.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// 1-based start line of the condition expression.
    /// </summary>
    public required int StartLine { get; init; }

    /// <summary>
    /// 1-based end line of the condition expression.
    /// </summary>
    public required int EndLine { get; init; }

    /// <summary>
    /// 1-based start character/column of the condition expression.
    /// </summary>
    public required int StartColumn { get; init; }

    /// <summary>
    /// 1-based end character/column of the condition expression.
    /// </summary>
    public required int EndColumn { get; init; }

    /// <summary>
    /// Raw expression text as written in source code.
    /// </summary>
    public required string ExpressionText { get; init; }

    /// <summary>
    /// Canonical structural signature of the normalized predicate.
    /// </summary>
    public required string NormalizedSignature { get; init; }

    /// <summary>
    /// Canonical structural signature when binding an instance receiver ($this).
    /// </summary>
    public string? InstanceNormalizedSignature { get; init; }

    /// <summary>
    /// Canonical structural signature of the De Morgan negated form, if applicable.
    /// </summary>
    public string? NegatedNormalizedSignature { get; init; }

    /// <summary>
    /// Canonical structural signature of the De Morgan negated form with instance receiver binding.
    /// </summary>
    public string? NegatedInstanceNormalizedSignature { get; init; }

    /// <summary>
    /// 64-bit hash of <see cref="NormalizedSignature"/>.
    /// </summary>
    public required ulong NormalizedHash { get; init; }

    /// <summary>
    /// 64-bit hash of <see cref="InstanceNormalizedSignature"/>.
    /// </summary>
    public ulong InstanceNormalizedHash { get; init; }

    /// <summary>
    /// 64-bit hash of <see cref="NegatedNormalizedSignature"/>.
    /// </summary>
    public ulong NegatedNormalizedHash { get; init; }

    /// <summary>
    /// 64-bit hash of <see cref="NegatedInstanceNormalizedSignature"/>.
    /// </summary>
    public ulong NegatedInstanceNormalizedHash { get; init; }

    /// <summary>
    /// Name of the enclosing member (method, property, constructor) if any.
    /// </summary>
    public string? EnclosingMember { get; init; }

    /// <summary>
    /// Name of the enclosing type (class, record, struct) if any.
    /// </summary>
    public string? EnclosingTypeName { get; init; }

    /// <summary>
    /// Enclosing namespace name if any.
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// Project identifier for boundary analysis.
    /// </summary>
    public string? ProjectId { get; init; }

    /// <summary>
    /// Category of the file containing this occurrence (Production or Test).
    /// </summary>
    public FileCategory Category { get; init; } = FileCategory.Production;

    /// <summary>
    /// Indicates whether this occurrence is the body or return expression of an existing helper method/property.
    /// </summary>
    public bool IsHelperDeclaration { get; init; }

    /// <summary>
    /// Metadata about the existing helper declaration, when <see cref="IsHelperDeclaration"/> is true.
    /// </summary>
    public PredicateHelperInfo? HelperInfo { get; init; }

    /// <summary>
    /// Captured receiver expression at call site (e.g. "slot" in "slot.State == ...").
    /// </summary>
    public string? ReceiverExpression { get; init; }

    /// <summary>
    /// Captured argument expressions at call site in static parameter order.
    /// </summary>
    public IReadOnlyList<string> StaticArgumentExpressions { get; init; } = [];

    /// <summary>
    /// Captured argument expressions at call site in instance parameter order (receiver excluded).
    /// </summary>
    public IReadOnlyList<string> InstanceArgumentExpressions { get; init; } = [];

    /// <summary>
    /// Captured argument expressions at call site in parameter order.
    /// </summary>
    public IReadOnlyList<string> ArgumentExpressions { get; init; } = [];

    /// <summary>
    /// Indicates whether this occurrence matched via logical negation (e.g. De Morgan or !expr).
    /// </summary>
    public bool IsNegatedAtCallSite { get; init; }
}
