using Microsoft.CodeAnalysis;
using Pmad.CodeCondenser.Core.Configuration;

namespace Pmad.CodeCondenser.Core.Patterns;

/// <summary>
/// Provides context for analyzing and matching condensation pattern rules against syntax nodes.
/// </summary>
public sealed class CondensationContext
{
    /// <summary>
    /// The syntax tree being analyzed.
    /// </summary>
    public required SyntaxTree SyntaxTree { get; init; }

    /// <summary>
    /// File path of the source document.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Active configuration settings.
    /// </summary>
    public required CodeCondenserSettings Settings { get; init; }

    /// <summary>
    /// Optional target .NET framework version of the enclosing project (e.g., 6.0, 8.0, 10.0).
    /// </summary>
    public Version? TargetDotNetVersion { get; init; }

    /// <summary>
    /// Optional identifier of the project enclosing this file.
    /// </summary>
    public string? ProjectId { get; init; }
}
