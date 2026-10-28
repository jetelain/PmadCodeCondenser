using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Filtering;

namespace Pmad.CodeCondenser.Core.Patterns;

/// <summary>
/// Rewrites syntax trees by applying condensation patterns.
/// </summary>
public sealed class PatternRewriter : IPatternRewriter
{
    private readonly ICondensationPatternRegistry _registry;

    /// <summary>
    /// Initializes a new instance of <see cref="PatternRewriter"/> with the specified or default registry.
    /// </summary>
    public PatternRewriter(ICondensationPatternRegistry? registry = null)
    {
        _registry = registry ?? CondensationPatternRegistry.CreateDefault();
    }

    /// <inheritdoc />
    public SyntaxTree Rewrite(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings,
        Version? targetDotNetVersion = null,
        IReadOnlySet<string>? ruleIdsToApply = null)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.EnablePatternCondensation)
        {
            return syntaxTree;
        }

        var root = syntaxTree.GetRoot();
        if (settings.ExcludeGeneratedCode && GeneratedCodeDetector.HasGeneratedCodeAttribute(root))
        {
            return syntaxTree;
        }

        var effectiveVersion = targetDotNetVersion ?? settings.GetParsedTargetDotNetVersion();
        var rules = _registry.GetEnabledRules(settings, effectiveVersion)
            .Where(r => r.CanAutoFix && (ruleIdsToApply == null || ruleIdsToApply.Contains(r.RuleId)))
            .ToList();

        if (rules.Count == 0)
        {
            return syntaxTree;
        }

        var context = new CondensationContext
        {
            SyntaxTree = syntaxTree,
            FilePath = filePath,
            Settings = settings,
            TargetDotNetVersion = effectiveVersion
        };

        var rewriterVisitor = new InternalRewriter(rules, context);
        var newRoot = rewriterVisitor.Visit(root);

        if (newRoot is not CSharpSyntaxNode csharpNode || ReferenceEquals(root, newRoot))
        {
            return syntaxTree;
        }

        return CSharpSyntaxTree.Create(
            csharpNode,
            syntaxTree.Options as CSharpParseOptions,
            syntaxTree.FilePath,
            syntaxTree.Encoding);
    }

    private sealed class InternalRewriter : CSharpSyntaxRewriter
    {
        private readonly CondensationContext _context;
        private readonly Dictionary<SyntaxKind, List<ICondensationPatternRule>> _kindRules = new();
        private readonly List<ICondensationPatternRule> _generalRules = new();

        public InternalRewriter(IReadOnlyList<ICondensationPatternRule> rules, CondensationContext context)
        {
            _context = context;

            foreach (var rule in rules)
            {
                if (rule.ApplicableKinds != null && rule.ApplicableKinds.Count > 0)
                {
                    foreach (var kind in rule.ApplicableKinds)
                    {
                        if (!_kindRules.TryGetValue(kind, out var list))
                        {
                            list = new List<ICondensationPatternRule>();
                            _kindRules[kind] = list;
                        }

                        list.Add(rule);
                    }
                }
                else
                {
                    _generalRules.Add(rule);
                }
            }
        }

        public override SyntaxNode? Visit(SyntaxNode? node)
        {
            if (node == null)
            {
                return null;
            }

            var kind = node.Kind();
            List<ICondensationPatternRule>? applicable = null;

            if (_kindRules.TryGetValue(kind, out var specificList))
            {
                applicable = specificList;
            }

            if (applicable != null)
            {
                foreach (var rule in applicable)
                {
                    if (rule.TryMatch(node, _context, out var match))
                    {
                        var rewritten = rule.Rewrite(node, match);
                        return base.Visit(rewritten);
                    }
                }
            }

            if (_generalRules.Count > 0)
            {
                foreach (var rule in _generalRules)
                {
                    if (rule.TryMatch(node, _context, out var match))
                    {
                        var rewritten = rule.Rewrite(node, match);
                        return base.Visit(rewritten);
                    }
                }
            }

            return base.Visit(node);
        }
    }
}
