using Pmad.CodeCondenser.Core.Configuration;

namespace Pmad.CodeCondenser.Core.Patterns;

/// <summary>
/// Registry containing available condensation pattern rules.
/// </summary>
public interface ICondensationPatternRegistry
{
    /// <summary>
    /// Registers a new pattern rule.
    /// </summary>
    void Register(ICondensationPatternRule rule);

    /// <summary>
    /// Registers multiple pattern rules.
    /// </summary>
    void RegisterRange(IEnumerable<ICondensationPatternRule> rules);

    /// <summary>
    /// Retrieves all registered rules.
    /// </summary>
    IReadOnlyList<ICondensationPatternRule> GetAllRules();

    /// <summary>
    /// Retrieves a registered rule by its unique ID, or null if not found.
    /// </summary>
    ICondensationPatternRule? GetRule(string ruleId);

    /// <summary>
    /// Filters and returns rules enabled by the given settings and compatible with the target framework.
    /// </summary>
    IReadOnlyList<ICondensationPatternRule> GetEnabledRules(CodeCondenserSettings? settings = null, Version? targetDotNetVersion = null);
}
