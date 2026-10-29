using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Patterns;

/// <summary>
/// Defines an extensible rule for detecting and condensing legacy or verbose code patterns.
/// </summary>
public interface ICondensationPatternRule
{
    /// <summary>
    /// Unique identifier for the rule (e.g., "PCC401").
    /// </summary>
    string RuleId { get; }

    /// <summary>
    /// Short human-readable title of the rule.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Detailed description of the pattern and recommended modern alternative.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Classification category for the rule.
    /// </summary>
    PatternCategory Category { get; }

    /// <summary>
    /// Minimum .NET target framework version required for this rule to be valid, or null if universally supported.
    /// </summary>
    Version? MinDotNetVersion { get; }

    /// <summary>
    /// Indicates whether automatic rewriting is supported and safe without manual intervention.
    /// </summary>
    bool CanAutoFix { get; }

    /// <summary>
    /// Optional set of syntax node kinds this rule inspects.
    /// Used by the detector for high-performance syntax walker dispatching.
    /// If null or empty, the rule is evaluated for all nodes.
    /// </summary>
    IReadOnlySet<SyntaxKind>? ApplicableKinds { get; }

    /// <summary>
    /// Evaluates whether the given syntax node matches the pattern under the provided context.
    /// </summary>
    bool TryMatch(SyntaxNode node, CondensationContext context, [NotNullWhen(true)] out CondensationMatch? match);

    /// <summary>
    /// Rewrites the matched syntax node into its condensed equivalent.
    /// </summary>
    SyntaxNode Rewrite(SyntaxNode node, CondensationMatch match);
}
