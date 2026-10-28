using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Filtering;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Patterns;

/// <summary>
/// Default implementation of <see cref="IPatternDetector"/> walking syntax trees to identify condensation candidates.
/// </summary>
public sealed class PatternDetector : IPatternDetector
{
    private readonly ICondensationPatternRegistry _registry;

    /// <summary>
    /// Initializes a new instance of <see cref="PatternDetector"/> using the specified or default registry.
    /// </summary>
    public PatternDetector(ICondensationPatternRegistry? registry = null)
    {
        _registry = registry ?? CondensationPatternRegistry.CreateDefault();
    }

    /// <inheritdoc />
    public IReadOnlyList<CondensationCandidate> DetectCandidates(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings,
        Version? targetDotNetVersion = null,
        string? projectId = null)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.EnablePatternCondensation)
        {
            return [];
        }

        var root = syntaxTree.GetRoot();
        if (settings.ExcludeGeneratedCode && GeneratedCodeDetector.HasGeneratedCodeAttribute(root))
        {
            return [];
        }

        var effectiveVersion = targetDotNetVersion ?? settings.GetParsedTargetDotNetVersion();
        var rules = _registry.GetEnabledRules(settings, effectiveVersion);
        if (rules.Count == 0)
        {
            return [];
        }

        var context = new CondensationContext
        {
            SyntaxTree = syntaxTree,
            FilePath = filePath,
            Settings = settings,
            TargetDotNetVersion = effectiveVersion,
            ProjectId = projectId
        };

        var walker = new PatternWalker(rules, context);
        walker.Visit(root);
        return walker.Candidates;
    }

    private sealed class PatternWalker : CSharpSyntaxWalker
    {
        private readonly List<CondensationCandidate> _candidates = new();
        private readonly CondensationContext _context;
        private readonly Dictionary<SyntaxKind, List<ICondensationPatternRule>> _kindRules = new();
        private readonly List<ICondensationPatternRule> _generalRules = new();

        public IReadOnlyList<CondensationCandidate> Candidates => _candidates;

        public PatternWalker(IReadOnlyList<ICondensationPatternRule> rules, CondensationContext context)
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

        public override void DefaultVisit(SyntaxNode node)
        {
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
                        AddCandidate(rule, node, match);
                    }
                }
            }

            if (_generalRules.Count > 0)
            {
                foreach (var rule in _generalRules)
                {
                    if (rule.TryMatch(node, _context, out var match))
                    {
                        AddCandidate(rule, node, match);
                    }
                }
            }

            base.DefaultVisit(node);
        }

        private void AddCandidate(ICondensationPatternRule rule, SyntaxNode node, CondensationMatch match)
        {
            var lineSpan = node.GetLocation().GetLineSpan();
            var startLine = lineSpan.StartLinePosition.Line + 1;
            var endLine = lineSpan.EndLinePosition.Line + 1;
            var startCol = lineSpan.StartLinePosition.Character + 1;
            var endCol = lineSpan.EndLinePosition.Character + 1;

            _candidates.Add(new CondensationCandidate
            {
                RuleId = rule.RuleId,
                RuleName = rule.Name,
                Category = rule.Category,
                FilePath = _context.FilePath,
                StartLine = startLine,
                EndLine = endLine,
                StartColumn = startCol,
                EndColumn = endCol,
                OriginalCode = node.ToString(),
                SuggestedCode = match.SuggestedCode,
                CanAutoFix = rule.CanAutoFix
            });
        }
    }
}
