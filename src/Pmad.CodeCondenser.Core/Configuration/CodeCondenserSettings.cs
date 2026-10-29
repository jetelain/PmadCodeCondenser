namespace Pmad.CodeCondenser.Core.Configuration;

/// <summary>
/// Configuration settings for Pmad.CodeCondenser analysis and rewriting.
/// </summary>
public sealed class CodeCondenserSettings
{
    /// <summary>
    /// Minimum character length of a comment to be flagged as verbose and considered for summarization.
    /// Default is 100 characters.
    /// </summary>
    public int CommentLengthThreshold { get; set; } = 100;

    /// <summary>
    /// Minimum character length of an XML documentation &lt;summary&gt; or &lt;remarks&gt; tag content to be flagged as verbose.
    /// If null, defaults to <see cref="CommentLengthThreshold"/>.
    /// </summary>
    public int? XmlSummaryLengthThreshold { get; set; }

    /// <summary>
    /// Optional override for the minimum character length of an XML documentation &lt;remarks&gt; tag content.
    /// If null, defaults to <see cref="XmlSummaryLengthThreshold"/> or <see cref="CommentLengthThreshold"/>.
    /// </summary>
    public int? XmlRemarksLengthThreshold { get; set; }

    /// <summary>
    /// Alias for <see cref="XmlSummaryLengthThreshold"/> to support configuration under both names.
    /// </summary>
    public int? XmlSummaryRemarksLengthThreshold
    {
        get => XmlSummaryLengthThreshold;
        set => XmlSummaryLengthThreshold = value;
    }

    /// <summary>
    /// Minimum character length of an XML documentation &lt;param&gt; tag content to be flagged as verbose.
    /// If null, defaults to <see cref="CommentLengthThreshold"/> / 2.
    /// </summary>
    public int? XmlParamLengthThreshold { get; set; }

    /// <summary>
    /// Gets the effective threshold for XML documentation &lt;summary&gt; tag content.
    /// Defaults to <see cref="CommentLengthThreshold"/> if <see cref="XmlSummaryLengthThreshold"/> is not set.
    /// </summary>
    public int GetEffectiveXmlSummaryThreshold() => XmlSummaryLengthThreshold ?? CommentLengthThreshold;

    /// <summary>
    /// Gets the effective threshold for XML documentation &lt;remarks&gt; tag content.
    /// Defaults to <see cref="XmlRemarksLengthThreshold"/> ?? <see cref="XmlSummaryLengthThreshold"/> ?? <see cref="CommentLengthThreshold"/>.
    /// </summary>
    public int GetEffectiveXmlRemarksThreshold() => XmlRemarksLengthThreshold ?? XmlSummaryLengthThreshold ?? CommentLengthThreshold;

    /// <summary>
    /// Gets the effective threshold for XML documentation &lt;param&gt; tag content.
    /// Defaults to <see cref="CommentLengthThreshold"/> / 2 if <see cref="XmlParamLengthThreshold"/> is not set.
    /// </summary>
    public int GetEffectiveXmlParamThreshold() => XmlParamLengthThreshold ?? Math.Max(1, CommentLengthThreshold / 2);

    /// <summary>
    /// Minimum number of statements a method must contain to be analyzed for clones.
    /// Eliminates false positives on trivial methods, getters, and small wrappers.
    /// Default is 8 statements.
    /// </summary>
    public int DuplicationStatementThreshold { get; set; } = 8;

    /// <summary>
    /// Minimum number of consecutive statements in a block required for sliding-window sub-method clone detection.
    /// Default is 10 statements.
    /// </summary>
    public int SubMethodStatementThreshold { get; set; } = 10;

    /// <summary>
    /// Alias for <see cref="SubMethodStatementThreshold"/>.
    /// </summary>
    public int? ThresholdSubMethodStatements
    {
        get => SubMethodStatementThreshold;
        set { if (value.HasValue) SubMethodStatementThreshold = value.Value; }
    }

    /// <summary>
    /// When true, enables sliding-window AST sequence detection within method bodies.
    /// Default is false.
    /// </summary>
    public bool EnableSubMethodCloneDetection { get; set; }

    /// <summary>
    /// Alias for <see cref="EnableSubMethodCloneDetection"/>.
    /// </summary>
    public bool? SubMethodClones
    {
        get => EnableSubMethodCloneDetection;
        set { if (value.HasValue) EnableSubMethodCloneDetection = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="EnableSubMethodCloneDetection"/>.
    /// </summary>
    public bool? EnableSubMethodClones
    {
        get => EnableSubMethodCloneDetection;
        set { if (value.HasValue) EnableSubMethodCloneDetection = value.Value; }
    }

    /// <summary>
    /// Relative or absolute path to the sub-method sequence cache file for incremental analysis.
    /// Default is ".codecondenser/submethod-cache.json".
    /// </summary>
    public string SubMethodCacheFilePath { get; set; } = ".codecondenser/submethod-cache.json";

    /// <summary>
    /// When true, enables detection of repeated inline string literals.
    /// Default is true.
    /// </summary>
    public bool EnableRepeatedStringDetection { get; set; } = true;

    /// <summary>
    /// Alias for <see cref="EnableRepeatedStringDetection"/>.
    /// </summary>
    public bool? EnableRepeatedStrings
    {
        get => EnableRepeatedStringDetection;
        set { if (value.HasValue) EnableRepeatedStringDetection = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="EnableRepeatedStringDetection"/>.
    /// </summary>
    public bool? RepeatedStrings
    {
        get => EnableRepeatedStringDetection;
        set { if (value.HasValue) EnableRepeatedStringDetection = value.Value; }
    }

    /// <summary>
    /// Minimum character length of a string literal to be considered for repeated inline string analysis.
    /// Default is 100 characters.
    /// </summary>
    public int RepeatedStringLengthThreshold { get; set; } = 100;

    /// <summary>
    /// Alias for <see cref="RepeatedStringLengthThreshold"/>.
    /// </summary>
    public int? ThresholdRepeatedStrings
    {
        get => RepeatedStringLengthThreshold;
        set { if (value.HasValue) RepeatedStringLengthThreshold = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="RepeatedStringLengthThreshold"/>.
    /// </summary>
    public int? ThresholdStringLength
    {
        get => RepeatedStringLengthThreshold;
        set { if (value.HasValue) RepeatedStringLengthThreshold = value.Value; }
    }

    /// <summary>
    /// Minimum character length of a string literal to be flagged when an existing constant with the same value already exists.
    /// Default is 20 characters (lower than <see cref="RepeatedStringLengthThreshold"/> because the declaration cost is already paid).
    /// </summary>
    public int ExistingConstantStringLengthThreshold { get; set; } = 20;

    /// <summary>
    /// Alias for <see cref="ExistingConstantStringLengthThreshold"/>.
    /// </summary>
    public int? ExistingConstantLengthThreshold
    {
        get => ExistingConstantStringLengthThreshold;
        set { if (value.HasValue) ExistingConstantStringLengthThreshold = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="ExistingConstantStringLengthThreshold"/>.
    /// </summary>
    public int? ThresholdExistingConstantLength
    {
        get => ExistingConstantStringLengthThreshold;
        set { if (value.HasValue) ExistingConstantStringLengthThreshold = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="ExistingConstantStringLengthThreshold"/>.
    /// </summary>
    public int? ThresholdExistingConstants
    {
        get => ExistingConstantStringLengthThreshold;
        set { if (value.HasValue) ExistingConstantStringLengthThreshold = value.Value; }
    }

    /// <summary>
    /// Minimum number of occurrences across the codebase for a string literal to be flagged as repeated.
    /// Default is 2.
    /// </summary>
    public int RepeatedStringMinOccurrences { get; set; } = 2;

    /// <summary>
    /// Alias for <see cref="RepeatedStringMinOccurrences"/>.
    /// </summary>
    public int? ThresholdStringOccurrences
    {
        get => RepeatedStringMinOccurrences;
        set { if (value.HasValue) RepeatedStringMinOccurrences = value.Value; }
    }

    /// <summary>
    /// Maximum number of new repeated string clusters tolerated in incremental mode before failing CI.
    /// Default is null (does not fail CI on repeated strings unless configured).
    /// </summary>
    public int? RepeatedStringTolerance { get; set; }

    /// <summary>
    /// Alias for <see cref="RepeatedStringTolerance"/>.
    /// </summary>
    public int? StringTolerance
    {
        get => RepeatedStringTolerance;
        set => RepeatedStringTolerance = value;
    }

    /// <summary>
    /// Exit code returned when additional repeated strings exceed <see cref="RepeatedStringTolerance"/> in incremental mode.
    /// Default is 2.
    /// </summary>
    public int RepeatedStringExitCode { get; set; } = 2;

    /// <summary>
    /// Set of string literal values that are explicitly ignored from repeated string detection.
    /// </summary>
    public ISet<string> IgnoredStrings { get; set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// When true, enables detection of duplicate conditions and matching against existing boolean helpers.
    /// Default is true.
    /// </summary>
    public bool EnablePredicateCloneDetection { get; set; } = true;

    /// <summary>
    /// Alias for <see cref="EnablePredicateCloneDetection"/>.
    /// </summary>
    public bool? EnablePredicates
    {
        get => EnablePredicateCloneDetection;
        set { if (value.HasValue) EnablePredicateCloneDetection = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="EnablePredicateCloneDetection"/>.
    /// </summary>
    public bool? PredicateClones
    {
        get => EnablePredicateCloneDetection;
        set { if (value.HasValue) EnablePredicateCloneDetection = value.Value; }
    }

    /// <summary>
    /// Minimum character length of a condition expression to be considered for predicate clone analysis.
    /// Default is 100 characters.
    /// </summary>
    public int PredicateLengthThreshold { get; set; } = 100;

    /// <summary>
    /// Alias for <see cref="PredicateLengthThreshold"/>.
    /// </summary>
    public int? ThresholdPredicateLength
    {
        get => PredicateLengthThreshold;
        set { if (value.HasValue) PredicateLengthThreshold = value.Value; }
    }

    /// <summary>
    /// Minimum character length of a condition expression to be flagged when an existing helper method/property matches.
    /// Default is 20 characters.
    /// </summary>
    public int ExistingHelperPredicateLengthThreshold { get; set; } = 20;

    /// <summary>
    /// Alias for <see cref="ExistingHelperPredicateLengthThreshold"/>.
    /// </summary>
    public int? ThresholdExistingHelperPredicateLength
    {
        get => ExistingHelperPredicateLengthThreshold;
        set { if (value.HasValue) ExistingHelperPredicateLengthThreshold = value.Value; }
    }

    /// <summary>
    /// Minimum number of occurrences across the codebase for a condition to be flagged as a duplicate predicate clone.
    /// Default is 2.
    /// </summary>
    public int PredicateMinOccurrences { get; set; } = 2;

    /// <summary>
    /// Alias for <see cref="PredicateMinOccurrences"/>.
    /// </summary>
    public int? ThresholdPredicateOccurrences
    {
        get => PredicateMinOccurrences;
        set { if (value.HasValue) PredicateMinOccurrences = value.Value; }
    }

    /// <summary>
    /// Maximum number of new predicate clone clusters tolerated in incremental mode before failing CI.
    /// Default is null (does not fail CI on predicate clones unless configured).
    /// </summary>
    public int? PredicateTolerance { get; set; }

    /// <summary>
    /// Alias for <see cref="PredicateTolerance"/>.
    /// </summary>
    public int? ConditionTolerance
    {
        get => PredicateTolerance;
        set => PredicateTolerance = value;
    }

    /// <summary>
    /// Exit code returned when additional predicate clones exceed <see cref="PredicateTolerance"/> in incremental mode.
    /// Default is 2.
    /// </summary>
    public int PredicateExitCode { get; set; } = 2;

    /// <summary>
    /// When true, enables enforcement of having a single top-level type per C# file.
    /// Default is true.
    /// </summary>
    public bool EnableTypeSplitting { get; set; } = true;

    /// <summary>
    /// Alias for <see cref="EnableTypeSplitting"/>.
    /// </summary>
    public bool? EnableSingleTypePerFile
    {
        get => EnableTypeSplitting;
        set { if (value.HasValue) EnableTypeSplitting = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="EnableTypeSplitting"/>.
    /// </summary>
    public bool? TypeSplitting
    {
        get => EnableTypeSplitting;
        set { if (value.HasValue) EnableTypeSplitting = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="EnableTypeSplitting"/>.
    /// </summary>
    public bool? SingleTypePerFile
    {
        get => EnableTypeSplitting;
        set { if (value.HasValue) EnableTypeSplitting = value.Value; }
    }

    /// <summary>
    /// When true, flags files for renaming if the primary type does not match the file name.
    /// Default is true.
    /// </summary>
    public bool EnableTypeRenaming { get; set; } = true;

    /// <summary>
    /// Alias for <see cref="EnableTypeRenaming"/>.
    /// </summary>
    public bool? EnableTypeRename
    {
        get => EnableTypeRenaming;
        set { if (value.HasValue) EnableTypeRenaming = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="EnableTypeRenaming"/>.
    /// </summary>
    public bool? TypeRenaming
    {
        get => EnableTypeRenaming;
        set { if (value.HasValue) EnableTypeRenaming = value.Value; }
    }

    /// <summary>
    /// Maximum lines of code (comments and blank lines excluded) for an auxiliary type to be tolerated inline.
    /// Default is 1.
    /// </summary>
    public int TypeLineTolerance { get; set; } = 1;

    /// <summary>
    /// Alias for <see cref="TypeLineTolerance"/>.
    /// </summary>
    public int? ThresholdTypeLines
    {
        get => TypeLineTolerance;
        set { if (value.HasValue) TypeLineTolerance = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="TypeLineTolerance"/>.
    /// </summary>
    public int? TypeLinesOfCodeTolerance
    {
        get => TypeLineTolerance;
        set { if (value.HasValue) TypeLineTolerance = value.Value; }
    }

    /// <summary>
    /// Maximum number of new type file violations tolerated in incremental mode before failing CI.
    /// Default is null (does not fail CI on type file violations unless configured).
    /// </summary>
    public int? TypeTolerance { get; set; }

    /// <summary>
    /// Alias for <see cref="TypeTolerance"/>.
    /// </summary>
    public int? SingleTypeTolerance
    {
        get => TypeTolerance;
        set => TypeTolerance = value;
    }

    /// <summary>
    /// Exit code returned when additional type file violations exceed <see cref="TypeTolerance"/> in incremental mode.
    /// Default is 2.
    /// </summary>
    public int TypeExitCode { get; set; } = 2;

    /// <summary>
    /// When true, enables detection of methods and classes exceeding configured lines of code (LOC) thresholds.
    /// Default is true.
    /// </summary>
    public bool EnableLinesOfCodeDetection { get; set; } = true;

    /// <summary>
    /// Alias for <see cref="EnableLinesOfCodeDetection"/>.
    /// </summary>
    public bool? LinesOfCodeDetection
    {
        get => EnableLinesOfCodeDetection;
        set { if (value.HasValue) EnableLinesOfCodeDetection = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="EnableLinesOfCodeDetection"/>.
    /// </summary>
    public bool? EnableLocDetection
    {
        get => EnableLinesOfCodeDetection;
        set { if (value.HasValue) EnableLinesOfCodeDetection = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="EnableLinesOfCodeDetection"/>.
    /// </summary>
    public bool? LocDetection
    {
        get => EnableLinesOfCodeDetection;
        set { if (value.HasValue) EnableLinesOfCodeDetection = value.Value; }
    }

    /// <summary>
    /// Maximum lines of code (comments and blank lines excluded) permitted in a method before being flagged.
    /// Default is 150.
    /// </summary>
    public int MethodLinesOfCodeThreshold { get; set; } = 150;

    /// <summary>
    /// Alias for <see cref="MethodLinesOfCodeThreshold"/>.
    /// </summary>
    public int? ThresholdMethodLines
    {
        get => MethodLinesOfCodeThreshold;
        set { if (value.HasValue) MethodLinesOfCodeThreshold = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="MethodLinesOfCodeThreshold"/>.
    /// </summary>
    public int? MethodLocThreshold
    {
        get => MethodLinesOfCodeThreshold;
        set { if (value.HasValue) MethodLinesOfCodeThreshold = value.Value; }
    }

    /// <summary>
    /// Maximum lines of code (comments and blank lines excluded) permitted in a class before being flagged.
    /// Default is 1500.
    /// </summary>
    public int ClassLinesOfCodeThreshold { get; set; } = 1500;

    /// <summary>
    /// Alias for <see cref="ClassLinesOfCodeThreshold"/>.
    /// </summary>
    public int? ThresholdClassLines
    {
        get => ClassLinesOfCodeThreshold;
        set { if (value.HasValue) ClassLinesOfCodeThreshold = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="ClassLinesOfCodeThreshold"/>.
    /// </summary>
    public int? ClassLocThreshold
    {
        get => ClassLinesOfCodeThreshold;
        set { if (value.HasValue) ClassLinesOfCodeThreshold = value.Value; }
    }

    /// <summary>
    /// Maximum number of new lines of code violations tolerated in incremental mode before failing CI.
    /// Default is null (does not fail CI on lines of code violations unless configured).
    /// </summary>
    public int? LinesOfCodeTolerance { get; set; }

    /// <summary>
    /// Alias for <see cref="LinesOfCodeTolerance"/>.
    /// </summary>
    public int? LocTolerance
    {
        get => LinesOfCodeTolerance;
        set => LinesOfCodeTolerance = value;
    }

    /// <summary>
    /// Alias for <see cref="LinesOfCodeTolerance"/>.
    /// </summary>
    public int? LinesTolerance
    {
        get => LinesOfCodeTolerance;
        set => LinesOfCodeTolerance = value;
    }

    /// <summary>
    /// Exit code returned when additional lines of code violations exceed <see cref="LinesOfCodeTolerance"/> in incremental mode.
    /// Default is 2.
    /// </summary>
    public int LinesOfCodeExitCode { get; set; } = 2;

    /// <summary>
    /// Alias for <see cref="LinesOfCodeExitCode"/>.
    /// </summary>
    public int? LocExitCode
    {
        get => LinesOfCodeExitCode;
        set { if (value.HasValue) LinesOfCodeExitCode = value.Value; }
    }

    /// <summary>
    /// Maximum number of items to display across all categories, prioritized by largest estimated benefit.
    /// Default is null (all detected items are displayed).
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Alias for <see cref="Limit"/>.
    /// </summary>
    public int? MaxDisplayedItems
    {
        get => Limit;
        set => Limit = value;
    }

    /// <summary>
    /// When true, enables detection of legacy code patterns that can be condensed using modern framework methods or language idioms.
    /// Default is true.
    /// </summary>
    public bool EnablePatternCondensation { get; set; } = true;

    /// <summary>
    /// Alias for <see cref="EnablePatternCondensation"/>.
    /// </summary>
    public bool? EnableCondensationPatterns
    {
        get => EnablePatternCondensation;
        set { if (value.HasValue) EnablePatternCondensation = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="EnablePatternCondensation"/>.
    /// </summary>
    public bool? CondensationPatterns
    {
        get => EnablePatternCondensation;
        set { if (value.HasValue) EnablePatternCondensation = value.Value; }
    }

    /// <summary>
    /// Optional whitelist of pattern rule IDs to enable (e.g., ["PCC401", "PCC402"]).
    /// When null or empty, all registered rules are evaluated.
    /// </summary>
    public List<string>? CondensationRules { get; set; }

    /// <summary>
    /// Optional blacklist of pattern rule IDs to disable.
    /// </summary>
    public List<string>? DisabledCondensationRules { get; set; }

    /// <summary>
    /// Optional target framework version override (e.g. "10.0", "net8.0", "6.0").
    /// If null, automatically inferred from project file.
    /// </summary>
    public string? TargetDotNetVersion { get; set; }

    /// <summary>
    /// Parses the <see cref="TargetDotNetVersion"/> into a <see cref="Version"/> object, or null if unspecified or invalid.
    /// </summary>
    public Version? GetParsedTargetDotNetVersion()
    {
        return Patterns.ProjectTargetFrameworkResolver.ParseTfmVersion(TargetDotNetVersion);
    }

    /// When true, includes XML documentation comments (///) in addition to single-line (//) and multi-line (/* */) comments.
    /// Default is true.
    /// </summary>
    public bool IncludeXmlDocumentation { get; set; } = true;

    /// <summary>
    /// Concurrency limit for asynchronous LLM requests.
    /// Default is 4.
    /// </summary>
    public int MaxParallelLlmRequests { get; set; } = 4;

    /// <summary>
    /// Maximum number of retries when an LLM summary is longer than before,
    /// or when reduction is above target and above 50% of the initial comment length.
    /// Default is 1 retry (up to 2 total attempts).
    /// </summary>
    public int MaxSummarizationRetries { get; set; } = 1;

    /// <summary>
    /// Alias for <see cref="MaxSummarizationRetries"/>.
    /// </summary>
    public int? SummarizationRetries
    {
        get => MaxSummarizationRetries;
        set { if (value.HasValue) MaxSummarizationRetries = value.Value; }
    }

    /// <summary>
    /// Active LLM provider (e.g. "Ollama").
    /// Default is "Ollama".
    /// </summary>
    public string Provider { get; set; } = "Ollama";

    /// <summary>
    /// Configuration section for Ollama provider.
    /// </summary>
    public OllamaSettings Ollama { get; set; } = new();

    /// <summary>
    /// Token estimator preset or model family to use when measuring code in LLM tokens (e.g. "default", "qwen", "gemma").
    /// Distinct from <see cref="OllamaSettings.Model"/> which is used for comment summarization.
    /// Default is "default".
    /// </summary>
    public string TokenEstimator { get; set; } = "default";

    /// <summary>
    /// Alias for <see cref="TokenEstimator"/>.
    /// </summary>
    public string? Estimator
    {
        get => TokenEstimator;
        set { if (!string.IsNullOrWhiteSpace(value)) TokenEstimator = value; }
    }

    /// <summary>
    /// When true, indicates that an LLM model, endpoint, or provider was explicitly configured.
    /// </summary>
    public bool IsLlmConfigured { get; set; }

    /// <summary>
    /// When true, indicates that the LLM was configured specifically by the global user profile configuration,
    /// rather than project-level configuration or command-line flags.
    /// </summary>
    public bool IsLlmConfiguredGlobally { get; set; }

    private SummaryMode? _summaryMode;

    /// <summary>
    /// Mode for comment summarization and reporting: Disabled, Summarize, or Report.
    /// When not explicitly set, defaults to Summarize if an LLM is configured, or Report if no LLM is configured.
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public SummaryMode? SummaryMode
    {
        get => _summaryMode;
        set
        {
            _summaryMode = value;
            if (value == Configuration.SummaryMode.Disabled)
            {
                EnableCommentSummarization = false;
            }
            else if (value.HasValue)
            {
                EnableCommentSummarization = true;
            }
        }
    }

    /// <summary>
    /// Alias for <see cref="SummaryMode"/>.
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public SummaryMode? Summary
    {
        get => SummaryMode;
        set => SummaryMode = value;
    }

    /// <summary>
    /// Resolves the effective <see cref="SummaryMode"/> based on explicit settings and whether an LLM is configured.
    /// </summary>
    public SummaryMode GetEffectiveSummaryMode()
    {
        if (SummaryMode.HasValue)
        {
            return SummaryMode.Value;
        }

        if (!EnableCommentSummarization)
        {
            return Configuration.SummaryMode.Disabled;
        }

        if (IsLlmConfigured && !IsLlmConfiguredGlobally)
        {
            return Configuration.SummaryMode.Summarize;
        }

        return Configuration.SummaryMode.Report;
    }

    /// <summary>
    /// When true, enables verbose comment extraction and LLM summarization.
    /// Default is true.
    /// </summary>
    public bool EnableCommentSummarization { get; set; } = true;

    /// <summary>
    /// Alias for <see cref="EnableCommentSummarization"/>.
    /// </summary>
    public bool? EnableComments
    {
        get => EnableCommentSummarization;
        set { if (value.HasValue) EnableCommentSummarization = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="EnableCommentSummarization"/>.
    /// </summary>
    public bool? CommentSummarization
    {
        get => EnableCommentSummarization;
        set { if (value.HasValue) EnableCommentSummarization = value.Value; }
    }

    /// <summary>
    /// When true, enables structural code clone (duplication) detection.
    /// Default is true.
    /// </summary>
    public bool EnableDuplicationDetection { get; set; } = true;

    /// <summary>
    /// Alias for <see cref="EnableDuplicationDetection"/>.
    /// </summary>
    public bool? EnableCodeDuplication
    {
        get => EnableDuplicationDetection;
        set { if (value.HasValue) EnableDuplicationDetection = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="EnableDuplicationDetection"/>.
    /// </summary>
    public bool? EnableCodeDuplicationDetection
    {
        get => EnableDuplicationDetection;
        set { if (value.HasValue) EnableDuplicationDetection = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="EnableDuplicationDetection"/>.
    /// </summary>
    public bool? EnableCloneDetection
    {
        get => EnableDuplicationDetection;
        set { if (value.HasValue) EnableDuplicationDetection = value.Value; }
    }

    /// <summary>
    /// Alias for <see cref="EnableDuplicationDetection"/>.
    /// </summary>
    public bool? CodeDuplicationDetection
    {
        get => EnableDuplicationDetection;
        set { if (value.HasValue) EnableDuplicationDetection = value.Value; }
    }

    /// <summary>
    /// Maximum number of new or modified code clone clusters tolerated in incremental mode before returning an error exit code.
    /// Default is 0 (zero tolerance).
    /// </summary>
    public int DuplicationTolerance { get; set; }

    /// <summary>
    /// Alias for <see cref="DuplicationTolerance"/>.
    /// </summary>
    public int? IncrementalDuplicationTolerance
    {
        get => DuplicationTolerance;
        set { if (value.HasValue) DuplicationTolerance = value.Value; }
    }

    /// <summary>
    /// Exit code returned when additional code duplication exceeds <see cref="DuplicationTolerance"/> in incremental mode.
    /// Default is 2.
    /// </summary>
    public int DuplicationExitCode { get; set; } = 2;

    /// <summary>
    /// Maximum number of new verbose comments tolerated in incremental mode before failing CI / returning an exit code.
    /// Default is null (does not fail CI on verbose comments unless configured).
    /// </summary>
    public int? CommentTolerance { get; set; }

    /// <summary>
    /// Alias for <see cref="CommentTolerance"/>.
    /// </summary>
    public int? VerboseCommentTolerance
    {
        get => CommentTolerance;
        set => CommentTolerance = value;
    }

    /// <summary>
    /// Exit code returned when additional verbose comments exceed <see cref="CommentTolerance"/> in incremental mode.
    /// Default is 2.
    /// </summary>
    public int CommentExitCode { get; set; } = 2;

    /// <summary>
    /// Relative or absolute path to the snapshot cache file for incremental analysis and WYSIWYG summaries.
    /// Default is ".codecondenser/snapshot.json".
    /// </summary>
    public string SnapshotFilePath { get; set; } = ".codecondenser/snapshot.json";

    /// <summary>
    /// Relative or absolute path to the JSON file stored in git that records comments that have already been summarized.
    /// Default is ".codecondenser/summarized-comments.json".
    /// </summary>
    public string SummarizedCommentsFilePath { get; set; } = ".codecondenser/summarized-comments.json";

    /// <summary>
    /// Maximum line length in columns for comments (XML doc comments are wrapped to stay within this limit).
    /// Default is 120 columns.
    /// </summary>
    public int MaxCommentLineLength { get; set; } = 120;

    /// <summary>
    /// Glob patterns of files and directories to include in analysis.
    /// If empty, all C# files under the target directory are considered.
    /// Default is empty.
    /// </summary>
    public IList<string> Include { get; set; } = new List<string>();

    /// <summary>
    /// User-defined glob patterns of files and directories to exclude from analysis.
    /// Default is empty.
    /// </summary>
    public IList<string> Exclude { get; set; } = new List<string>();

    /// <summary>
    /// When true, automatically ignores common build, tooling, and artifact directories (bin, obj, .git, .vs, .codecondenser, node_modules, testresults).
    /// Default is true.
    /// </summary>
    public bool UseDefaultExcludes { get; set; } = true;

    /// <summary>
    /// When true, completely skips scanning and reporting test files.
    /// Default is false.
    /// </summary>
    public bool ExcludeTests { get; set; }

    /// <summary>
    /// When false (default), test clones are reported for review but do not count toward CI tolerance failure.
    /// When true, test clones count toward CI gate failure.
    /// </summary>
    public bool EnforceTestDuplication { get; set; }

    /// <summary>
    /// Glob patterns used to categorize files as unit/integration tests for isolated reporting and guidance.
    /// </summary>
    public IList<string> TestPaths { get; set; } = new List<string>
    {
        "tests/**",
        "test/**",
        "**/*.Tests/**",
        "**/*.Test/**",
        "**/*Test.cs",
        "**/*Tests.cs"
    };

    /// <summary>
    /// When true, automatically ignores auto-generated files and types.
    /// Default is true.
    /// </summary>
    public bool ExcludeGeneratedCode { get; set; } = true;

    /// <summary>
    /// Glob patterns matching compiler or tooling generated code.
    /// </summary>
    public IList<string> GeneratedCodePatterns { get; set; } = new List<string>
    {
        "**/*.Designer.cs",
        "**/*.g.cs",
        "**/*.generated.cs",
        "**/*ModelSnapshot.cs",
        "**/Migrations/**"
    };

    /// <summary>
    /// Set of comment content hashes that have already been summarized or marked to ignore.
    /// </summary>
    public ISet<string> IgnoredCommentHashes { get; set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Creates a clone of the current settings, including a copy of <see cref="IgnoredCommentHashes"/>.
    /// </summary>
    public CodeCondenserSettings Clone()
    {
        return new CodeCondenserSettings
        {
            CommentLengthThreshold = CommentLengthThreshold,
            XmlSummaryLengthThreshold = XmlSummaryLengthThreshold,
            XmlRemarksLengthThreshold = XmlRemarksLengthThreshold,
            XmlParamLengthThreshold = XmlParamLengthThreshold,
            DuplicationStatementThreshold = DuplicationStatementThreshold,
            SubMethodStatementThreshold = SubMethodStatementThreshold,
            EnableSubMethodCloneDetection = EnableSubMethodCloneDetection,
            SubMethodCacheFilePath = SubMethodCacheFilePath,
            IncludeXmlDocumentation = IncludeXmlDocumentation,
            MaxParallelLlmRequests = MaxParallelLlmRequests,
            MaxSummarizationRetries = MaxSummarizationRetries,
            Provider = Provider,
            Ollama = new OllamaSettings
            {
                Endpoint = Ollama.Endpoint,
                Model = Ollama.Model,
                ContextSize = Ollama.ContextSize
            },
            IsLlmConfigured = IsLlmConfigured,
            IsLlmConfiguredGlobally = IsLlmConfiguredGlobally,
            SummaryMode = SummaryMode,
            EnableCommentSummarization = EnableCommentSummarization,
            EnableDuplicationDetection = EnableDuplicationDetection,
            DuplicationTolerance = DuplicationTolerance,
            DuplicationExitCode = DuplicationExitCode,
            CommentTolerance = CommentTolerance,
            CommentExitCode = CommentExitCode,
            SnapshotFilePath = SnapshotFilePath,
            SummarizedCommentsFilePath = SummarizedCommentsFilePath,
            MaxCommentLineLength = MaxCommentLineLength,
            Include = new List<string>(Include),
            Exclude = new List<string>(Exclude),
            UseDefaultExcludes = UseDefaultExcludes,
            ExcludeTests = ExcludeTests,
            EnforceTestDuplication = EnforceTestDuplication,
            TestPaths = new List<string>(TestPaths),
            ExcludeGeneratedCode = ExcludeGeneratedCode,
            GeneratedCodePatterns = new List<string>(GeneratedCodePatterns),
            IgnoredCommentHashes = new HashSet<string>(IgnoredCommentHashes, StringComparer.Ordinal),
            TokenEstimator = TokenEstimator,
            EnableRepeatedStringDetection = EnableRepeatedStringDetection,
            RepeatedStringLengthThreshold = RepeatedStringLengthThreshold,
            ExistingConstantStringLengthThreshold = ExistingConstantStringLengthThreshold,
            RepeatedStringMinOccurrences = RepeatedStringMinOccurrences,
            RepeatedStringTolerance = RepeatedStringTolerance,
            RepeatedStringExitCode = RepeatedStringExitCode,
            IgnoredStrings = new HashSet<string>(IgnoredStrings, StringComparer.Ordinal),
            EnablePredicateCloneDetection = EnablePredicateCloneDetection,
            PredicateLengthThreshold = PredicateLengthThreshold,
            ExistingHelperPredicateLengthThreshold = ExistingHelperPredicateLengthThreshold,
            PredicateMinOccurrences = PredicateMinOccurrences,
            PredicateTolerance = PredicateTolerance,
            PredicateExitCode = PredicateExitCode,
            EnableTypeSplitting = EnableTypeSplitting,
            EnableTypeRenaming = EnableTypeRenaming,
            TypeLineTolerance = TypeLineTolerance,
            TypeTolerance = TypeTolerance,
            TypeExitCode = TypeExitCode,
            EnableLinesOfCodeDetection = EnableLinesOfCodeDetection,
            MethodLinesOfCodeThreshold = MethodLinesOfCodeThreshold,
            ClassLinesOfCodeThreshold = ClassLinesOfCodeThreshold,
            LinesOfCodeTolerance = LinesOfCodeTolerance,
            LinesOfCodeExitCode = LinesOfCodeExitCode,
            Limit = Limit,
            EnablePatternCondensation = EnablePatternCondensation,
            CondensationRules = CondensationRules != null ? new List<string>(CondensationRules) : null,
            DisabledCondensationRules = DisabledCondensationRules != null ? new List<string>(DisabledCondensationRules) : null,
            TargetDotNetVersion = TargetDotNetVersion
        };
    }
}
