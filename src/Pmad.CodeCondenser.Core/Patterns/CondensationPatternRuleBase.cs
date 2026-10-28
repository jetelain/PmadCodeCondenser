using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Patterns;

/// <summary>
/// Convenient strongly-typed base class for condensation pattern rules operating on a specific syntax node type.
/// </summary>
/// <typeparam name="TNode">The specific <see cref="SyntaxNode"/> type to match.</typeparam>
public abstract class CondensationPatternRuleBase<TNode> : ICondensationPatternRule where TNode : SyntaxNode
{
    /// <inheritdoc />
    public abstract string RuleId { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public abstract PatternCategory Category { get; }

    /// <inheritdoc />
    public virtual Version? MinDotNetVersion => null;

    /// <inheritdoc />
    public virtual bool CanAutoFix => true;

    /// <inheritdoc />
    public virtual IReadOnlySet<SyntaxKind>? ApplicableKinds => null;

    /// <inheritdoc />
    public bool TryMatch(SyntaxNode node, CondensationContext context, [NotNullWhen(true)] out CondensationMatch? match)
    {
        match = null;
        if (node is not TNode typedNode)
        {
            return false;
        }

        // Verify minimum framework version if known
        if (MinDotNetVersion != null && context.TargetDotNetVersion != null)
        {
            if (context.TargetDotNetVersion < MinDotNetVersion)
            {
                return false;
            }
        }

        // Verify suppression markers
        if (PatternIgnoreMarker.IsIgnored(node, RuleId))
        {
            return false;
        }

        return TryMatchNode(typedNode, context, out match);
    }

    /// <inheritdoc />
    public SyntaxNode Rewrite(SyntaxNode node, CondensationMatch match)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(match);

        if (node is not TNode typedNode)
        {
            return node;
        }

        if (match.ReplacementNode != null)
        {
            return match.ReplacementNode
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }

        var rewritten = RewriteNode(typedNode, match);
        return rewritten
            .WithLeadingTrivia(node.GetLeadingTrivia())
            .WithTrailingTrivia(node.GetTrailingTrivia());
    }

    /// <summary>
    /// Evaluates whether the typed syntax node matches this condensation rule.
    /// </summary>
    protected abstract bool TryMatchNode(TNode node, CondensationContext context, [NotNullWhen(true)] out CondensationMatch? match);

    /// <summary>
    /// Constructs the rewritten replacement node for the matched node.
    /// </summary>
    protected abstract SyntaxNode RewriteNode(TNode node, CondensationMatch match);
}
