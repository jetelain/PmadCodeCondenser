using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Core.Patterns;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Patterns;

public class CondensationPatternRegistryTests
{
    [Fact]
    public void CreateDefault_ContainsStandardRules()
    {
        var registry = CondensationPatternRegistry.CreateDefault();
        var rules = registry.GetAllRules();

        Assert.Contains(rules, r => r.RuleId == "PCC401");
        Assert.Contains(rules, r => r.RuleId == "PCC402");
        Assert.Contains(rules, r => r.RuleId == "PCC403");
        Assert.Contains(rules, r => r.RuleId == "PCC404");
    }

    [Fact]
    public void Register_CustomRule_CanBeRetrievedAndUsed()
    {
        var registry = new CondensationPatternRegistry();
        var customRule = new DummyCustomRule();
        registry.Register(customRule);

        Assert.Same(customRule, registry.GetRule("CUSTOM01"));
        Assert.Contains(registry.GetAllRules(), r => r.RuleId == "CUSTOM01");
    }

    [Fact]
    public void GetEnabledRules_WithWhitelist_ReturnsOnlyWhitelisted()
    {
        var registry = CondensationPatternRegistry.CreateDefault();
        var settings = new CodeCondenserSettings
        {
            CondensationRules = ["PCC401"]
        };

        var enabled = registry.GetEnabledRules(settings);
        Assert.Single(enabled);
        Assert.Equal("PCC401", enabled[0].RuleId);
    }

    [Fact]
    public void GetEnabledRules_WithBlacklist_ExcludesBlacklisted()
    {
        var registry = CondensationPatternRegistry.CreateDefault();
        var settings = new CodeCondenserSettings
        {
            DisabledCondensationRules = ["PCC401", "PCC404"]
        };

        var enabled = registry.GetEnabledRules(settings);
        Assert.DoesNotContain(enabled, r => r.RuleId == "PCC401");
        Assert.DoesNotContain(enabled, r => r.RuleId == "PCC404");
        Assert.Contains(enabled, r => r.RuleId == "PCC402");
        Assert.Contains(enabled, r => r.RuleId == "PCC403");
    }

    [Fact]
    public void GetEnabledRules_WithOlderFramework_ExcludesNet6Rules()
    {
        var registry = CondensationPatternRegistry.CreateDefault();
        var enabled = registry.GetEnabledRules(targetDotNetVersion: new Version(5, 0));

        Assert.DoesNotContain(enabled, r => r.RuleId == "PCC401"); // PCC401 requires net6.0
        Assert.DoesNotContain(enabled, r => r.RuleId == "PCC404"); // PCC404 requires net6.0
        Assert.Contains(enabled, r => r.RuleId == "PCC402");        // net4.0+
        Assert.Contains(enabled, r => r.RuleId == "PCC403");        // any
    }

    private sealed class DummyCustomRule : CondensationPatternRuleBase<InvocationExpressionSyntax>
    {
        public override string RuleId => "CUSTOM01";
        public override string Name => "Custom Dummy Rule";
        public override string Description => "A custom extension rule.";
        public override PatternCategory Category => PatternCategory.Text;

        protected override bool TryMatchNode(InvocationExpressionSyntax node, CondensationContext context, [NotNullWhen(true)] out CondensationMatch? match)
        {
            match = null;
            return false;
        }

        protected override SyntaxNode RewriteNode(InvocationExpressionSyntax node, CondensationMatch match) => node;
    }
}
