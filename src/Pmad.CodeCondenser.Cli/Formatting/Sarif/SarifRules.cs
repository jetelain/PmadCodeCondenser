namespace Pmad.CodeCondenser.Cli.Formatting.Sarif;

/// <summary>
/// SARIF reporting rule descriptors for Pmad.CodeCondenser diagnostics.
/// </summary>
public static class SarifRules
{
    public const string ProductionCodeDuplicationRuleId = "CC001";
    public const string TestCodeDuplicationRuleId = "CC002";
    public const string VerboseCommentRuleId = "CC003";
    public const string CrossBoundaryDuplicationRuleId = "CC004";
    public const string SubMethodCodeDuplicationRuleId = "CC005";
    public const string RepeatedInlineStringRuleId = "CC006";
    public const string SingleTypePerFileRuleId = "CC007";

    public static IReadOnlyList<SarifReportingDescriptor> AllRules { get; } =
    [
        new SarifReportingDescriptor
        {
            Id = ProductionCodeDuplicationRuleId,
            Name = "ProductionCodeDuplication",
            ShortDescription = new SarifMultiformatMessageString
            {
                Text = "Structural code clone detected across production methods."
            },
            Help = new SarifMultiformatMessageString
            {
                Text = "Extract common logic into a shared helper method. To suppress, add '// [codecondenser:ignore-duplicate]' above the method.",
                Markdown = "Extract common logic into a shared helper method.\n\nTo suppress, add `// [codecondenser:ignore-duplicate]` on the line directly preceding the method declaration."
            },
            DefaultConfiguration = new SarifReportingConfiguration
            {
                Level = "warning"
            }
        },
        new SarifReportingDescriptor
        {
            Id = TestCodeDuplicationRuleId,
            Name = "TestCodeDuplication",
            ShortDescription = new SarifMultiformatMessageString
            {
                Text = "Structural code clone detected in test suite."
            },
            Help = new SarifMultiformatMessageString
            {
                Text = "Consider parameterized test theories ([Theory] + [InlineData]) or test builder fixtures.",
                Markdown = "Consider parameterized test theories (`[Theory]` + `[InlineData]`) or test builder fixtures instead of production helpers."
            },
            DefaultConfiguration = new SarifReportingConfiguration
            {
                Level = "note"
            }
        },
        new SarifReportingDescriptor
        {
            Id = VerboseCommentRuleId,
            Name = "VerboseComment",
            ShortDescription = new SarifMultiformatMessageString
            {
                Text = "Overly verbose comment exceeding configured length threshold."
            },
            DefaultConfiguration = new SarifReportingConfiguration
            {
                Level = "note"
            }
        },
        new SarifReportingDescriptor
        {
            Id = CrossBoundaryDuplicationRuleId,
            Name = "CrossBoundaryDuplication",
            ShortDescription = new SarifMultiformatMessageString
            {
                Text = "Structural code clone detected spanning production and test suites."
            },
            DefaultConfiguration = new SarifReportingConfiguration
            {
                Level = "warning"
            }
        },
        new SarifReportingDescriptor
        {
            Id = SubMethodCodeDuplicationRuleId,
            Name = "SubMethodCodeDuplication",
            ShortDescription = new SarifMultiformatMessageString
            {
                Text = "Duplicate statement block detected within method bodies using sliding-window analysis."
            },
            Help = new SarifMultiformatMessageString
            {
                Text = "Extract duplicate statement sequence into a shared local function or private helper.",
                Markdown = "Extract duplicate statement sequence into a shared local function or private helper method."
            },
            DefaultConfiguration = new SarifReportingConfiguration
            {
                Level = "warning"
            }
        },
        new SarifReportingDescriptor
        {
            Id = RepeatedInlineStringRuleId,
            Name = "RepeatedInlineString",
            ShortDescription = new SarifMultiformatMessageString
            {
                Text = "Repeated inline string literal detected that can be extracted to a shared constant."
            },
            Help = new SarifMultiformatMessageString
            {
                Text = "Extract repeated string literal into a shared constant (e.g. 'internal const string Name = \"...\";').",
                Markdown = "Extract repeated string literal into a shared constant (e.g. `internal const string Name = \"...\";`).\n\nTo suppress, add `// codecondenser:ignore-string` or `[SuppressMessage(\"CodeCondenser\", \"RepeatedString\")]`."
            },
            DefaultConfiguration = new SarifReportingConfiguration
            {
                Level = "warning"
            }
        },
        new SarifReportingDescriptor
        {
            Id = SingleTypePerFileRuleId,
            Name = "SingleTypePerFile",
            ShortDescription = new SarifMultiformatMessageString
            {
                Text = "Each top-level type should have its own C# file matching its name."
            },
            Help = new SarifMultiformatMessageString
            {
                Text = "Split multiple types into dedicated files or rename the file to match the primary type.",
                Markdown = "Extract auxiliary types into separate `.cs` files matching their names, or rename the file if its primary type does not match.\n\nTo suppress, add `// codecondenser:ignore-type-split` or `[SuppressMessage(\"CodeCondenser\", \"SingleTypePerFile\")]`."
            },
            DefaultConfiguration = new SarifReportingConfiguration
            {
                Level = "warning"
            }
        }
    ];
}
