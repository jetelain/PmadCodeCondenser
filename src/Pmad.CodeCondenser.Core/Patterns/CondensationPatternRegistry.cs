using System.Collections.Concurrent;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Patterns.Rules;

namespace Pmad.CodeCondenser.Core.Patterns;

/// <summary>
/// Default implementation of <see cref="ICondensationPatternRegistry"/> managing pattern rules.
/// </summary>
public sealed class CondensationPatternRegistry : ICondensationPatternRegistry
{
    private readonly ConcurrentDictionary<string, ICondensationPatternRule> _rules = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a default registry populated with all standard built-in condensation rules.
    /// </summary>
    public static CondensationPatternRegistry CreateDefault()
    {
        var registry = new CondensationPatternRegistry();
        registry.RegisterRange(
        [
            new ReplaceLineEndingsRule(),
            new IsNullOrWhiteSpaceRule(),
            new LinqWhereSimplificationRule(),
            new ThrowIfNullRule(),
            new ThrowIfNullOrWhiteSpaceRule(),
            new StringContainsComparisonRule(),
            new MinByMaxByRule(),
            new RedundantNullCheckWithIsRule(),
            new TernaryNullCoalescingRule()
        ]);
        return registry;
    }

    /// <inheritdoc />
    public void Register(ICondensationPatternRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules[rule.RuleId] = rule;
    }

    /// <inheritdoc />
    public void RegisterRange(IEnumerable<ICondensationPatternRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var rule in rules)
        {
            Register(rule);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ICondensationPatternRule> GetAllRules()
    {
        return _rules.Values.OrderBy(r => r.RuleId, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <inheritdoc />
    public ICondensationPatternRule? GetRule(string ruleId)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
        {
            return null;
        }

        return _rules.TryGetValue(ruleId, out var rule) ? rule : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<ICondensationPatternRule> GetEnabledRules(
        CodeCondenserSettings? settings = null,
        Version? targetDotNetVersion = null)
    {
        var all = GetAllRules();
        var result = new List<ICondensationPatternRule>(all.Count);

        var enabledRuleIds = settings?.CondensationRules != null && settings.CondensationRules.Count > 0
            ? new HashSet<string>(settings.CondensationRules, StringComparer.OrdinalIgnoreCase)
            : null;

        var disabledRuleIds = settings?.DisabledCondensationRules != null && settings.DisabledCondensationRules.Count > 0
            ? new HashSet<string>(settings.DisabledCondensationRules, StringComparer.OrdinalIgnoreCase)
            : null;

        foreach (var rule in all)
        {
            // If explicit whitelist is set, rule must be in whitelist
            if (enabledRuleIds != null && !enabledRuleIds.Contains(rule.RuleId))
            {
                continue;
            }

            // If blacklist is set, rule must not be in blacklist
            if (disabledRuleIds != null && disabledRuleIds.Contains(rule.RuleId))
            {
                continue;
            }

            // Check framework version compatibility
            if (targetDotNetVersion != null && rule.MinDotNetVersion != null && targetDotNetVersion < rule.MinDotNetVersion)
            {
                continue;
            }

            result.Add(rule);
        }

        return result;
    }
}
