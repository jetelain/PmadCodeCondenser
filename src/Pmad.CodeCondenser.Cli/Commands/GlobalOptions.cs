using System.CommandLine;
using Pmad.CodeCondenser.Cli.Formatting;
using Pmad.CodeCondenser.Core.Configuration;

namespace Pmad.CodeCondenser.Cli.Commands;

/// <summary>
/// Defines and binds CLI options shared across subcommands in codecondenser.
/// </summary>
public sealed class GlobalOptions
{
    // Configuration paths
    public Option<string?> ConfigOption { get; } = new("--config", "-c")
    {
        Description = "Path to custom codecondenser.json configuration file",
        Recursive = true
    };

    public Option<string?> GlobalConfigOption { get; } = new("--global-config", "-gc")
    {
        Description = "Path to custom global user profile configuration file",
        Recursive = true
    };

    public Option<bool> NoGlobalConfigOption { get; } = new("--no-global-config")
    {
        Description = "Disable loading of global user profile configuration",
        Recursive = true
    };

    // Output & Formatting
    public Option<OutputFormat> FormatOption { get; } = new("--format", "-f")
    {
        Description = "Output report format: console (default), sarif, or json",
        DefaultValueFactory = _ => OutputFormat.Console,
        Recursive = true
    };

    public Option<string?> OutputOption { get; } = new("--output", "-o")
    {
        Description = "Optional file path to write report output to (default: stdout)",
        Recursive = true
    };

    public Option<bool> NoColorOption { get; } = new("--no-color")
    {
        Description = "Disable ANSI color formatting in output",
        Recursive = true
    };

    // Path filtering & Exclusions
    public Option<string[]> IncludeOption { get; } = new("--include")
    {
        Description = "Add glob pattern of files/directories to include",
        AllowMultipleArgumentsPerToken = true,
        Recursive = true
    };

    public Option<string[]> ExcludeOption { get; } = new("--exclude")
    {
        Description = "Add glob pattern of files/directories to exclude",
        AllowMultipleArgumentsPerToken = true,
        Recursive = true
    };

    public Option<bool> ExcludeTestsOption { get; } = new("--exclude-tests", "--production-only")
    {
        Description = "Completely exclude test files from analysis",
        Recursive = true
    };

    public Option<bool> IncludeGeneratedOption { get; } = new("--include-generated")
    {
        Description = "Disable automatic exclusion of generated files",
        Recursive = true
    };

    public Option<bool> DryRunOption { get; } = new("--dry-run", "-n")
    {
        Description = "Preview actions without modifying files on disk",
        Recursive = true
    };

    // Incremental baseline & CI tolerances
    public Option<bool> IncrementalOption { get; } = new("--incremental", "-i")
    {
        Description = "Use snapshot cache to analyze only modified or new files",
        Recursive = true
    };

    public Option<int?> DuplicationToleranceOption { get; } = new("--duplication-tolerance", "-dt")
    {
        Description = "Maximum new clone clusters tolerated in incremental mode (default: 0)",
        Recursive = true
    };

    public Option<int?> CommentToleranceOption { get; } = new("--comment-tolerance", "-ct")
    {
        Description = "Maximum new verbose comments tolerated in incremental mode",
        Recursive = true
    };

    public Option<int?> StringToleranceOption { get; } = new("--string-tolerance", "-st", "--repeated-string-tolerance")
    {
        Description = "Maximum new repeated string groups tolerated in incremental mode",
        Recursive = true
    };

    public Option<bool> EnforceTestDuplicationOption { get; } = new("--enforce-test-duplication")
    {
        Description = "Treat test clones as CI policy violation in gate check",
        Recursive = true
    };

    // Clone & Sub-method detection thresholds
    public Option<bool> NoDuplicationOption { get; } = new("--no-duplication", "--disable-duplication", "--no-clone-detection")
    {
        Description = "Disable structural code clone detection",
        Recursive = true
    };

    public Option<bool> EnableDuplicationOption { get; } = new("--enable-duplication", "--enable-duplication-detection")
    {
        Description = "Explicitly enable structural code clone detection",
        Recursive = true
    };

    public Option<int?> ThresholdStatementsOption { get; } = new("--threshold-statements", "-ts")
    {
        Description = "Minimum method statement count for clone detection (default: 8)",
        Recursive = true
    };

    public Option<bool> SubMethodClonesOption { get; } = new("--sub-method-clones", "--submethod-clones", "--sub-method")
    {
        Description = "Enable sliding-window sub-method clone detection across method bodies",
        Recursive = true
    };

    public Option<int?> ThresholdSubMethodStatementsOption { get; } = new("--threshold-submethod-statements")
    {
        Description = "Minimum statement count for sub-method clone detection (default: 10)",
        Recursive = true
    };

    // Repeated strings thresholds
    public Option<bool> NoStringsOption { get; } = new("--no-strings", "--disable-strings", "--no-repeated-strings")
    {
        Description = "Disable repeated inline string detection",
        Recursive = true
    };

    public Option<bool> EnableStringsOption { get; } = new("--enable-strings", "--enable-repeated-strings")
    {
        Description = "Explicitly enable repeated inline string detection",
        Recursive = true
    };

    public Option<int?> ThresholdRepeatedStringsOption { get; } = new("--threshold-strings", "-tstr", "--threshold-repeated-strings")
    {
        Description = "Minimum character length for repeated string detection (default: 100)",
        Recursive = true
    };

    public Option<int?> ThresholdStringOccurrencesOption { get; } = new("--string-occurrences", "-so", "--repeated-string-occurrences")
    {
        Description = "Minimum occurrences for repeated string detection (default: 2)",
        Recursive = true
    };

    public Option<int?> ThresholdExistingConstantsOption { get; } = new("--threshold-existing-constants", "-tec", "--threshold-existing-constant-strings", "--existing-constant-threshold")
    {
        Description = "Minimum character length to flag inline strings when an existing constant already exists (default: 20)",
        Recursive = true
    };

    // Type splitting & Single type per file thresholds
    public Option<bool> NoTypeSplitOption { get; } = new("--no-type-split", "--no-split-types", "--no-single-type-per-file")
    {
        Description = "Disable single-type-per-file enforcement and type splitting",
        Recursive = true
    };

    public Option<bool> EnableTypeSplitOption { get; } = new("--enable-type-split", "--enable-type-splitting", "--enable-single-type-per-file")
    {
        Description = "Explicitly enable single-type-per-file enforcement and type splitting",
        Recursive = true
    };

    public Option<bool> NoTypeRenameOption { get; } = new("--no-type-rename", "--disable-type-rename")
    {
        Description = "Disable renaming files whose primary type does not match the file name",
        Recursive = true
    };

    public Option<int?> TypeLineToleranceOption { get; } = new("--type-line-tolerance", "-tlt", "--threshold-type-lines")
    {
        Description = "Maximum lines of code (comments excluded) for an auxiliary type to be tolerated inline (default: 1)",
        Recursive = true
    };

    public Option<int?> TypeToleranceOption { get; } = new("--type-tolerance", "-tt", "--single-type-tolerance")
    {
        Description = "Maximum new type file violations tolerated in incremental mode",
        Recursive = true
    };

    // Lines of code limits thresholds
    public Option<bool> NoLocOption { get; } = new("--no-loc", "--disable-loc", "--no-lines-of-code")
    {
        Description = "Disable lines of code limits detection",
        Recursive = true
    };

    public Option<bool> EnableLocOption { get; } = new("--enable-loc", "--enable-lines-of-code")
    {
        Description = "Explicitly enable lines of code limits detection",
        Recursive = true
    };

    public Option<int?> ThresholdMethodLinesOption { get; } = new("--threshold-method-lines", "-tml", "--method-lines-threshold")
    {
        Description = "Maximum lines of code threshold for methods (default: 150)",
        Recursive = true
    };

    public Option<int?> ThresholdClassLinesOption { get; } = new("--threshold-class-lines", "-tcl", "--class-lines-threshold")
    {
        Description = "Maximum lines of code threshold for classes and records (default: 1500)",
        Recursive = true
    };

    public Option<int?> LocToleranceOption { get; } = new("--loc-tolerance", "-lt", "--lines-of-code-tolerance")
    {
        Description = "Maximum new lines of code violations tolerated in incremental mode",
        Recursive = true
    };

    // Predicate / Boolean Condition clones thresholds
    public Option<bool> NoPredicatesOption { get; } = new("--no-predicates", "--disable-predicates", "--no-predicate-clones")
    {
        Description = "Disable boolean condition / predicate clone detection",
        Recursive = true
    };

    public Option<bool> EnablePredicatesOption { get; } = new("--enable-predicates", "--enable-predicate-clones")
    {
        Description = "Explicitly enable boolean condition / predicate clone detection",
        Recursive = true
    };

    public Option<int?> ThresholdPredicateLengthOption { get; } = new("--threshold-predicates", "-tpred", "--predicate-length-threshold", "--threshold-predicate-length")
    {
        Description = "Minimum character length for boolean predicate clone detection (default: 100)",
        Recursive = true
    };

    public Option<int?> ThresholdExistingHelperPredicateLengthOption { get; } = new("--threshold-existing-helpers", "-thelp", "--threshold-existing-predicate-helpers")
    {
        Description = "Minimum character length to flag inline conditions when an existing helper already exists (default: 20)",
        Recursive = true
    };

    public Option<int?> PredicateToleranceOption { get; } = new("--predicate-tolerance", "-pt", "--predicate-clones-tolerance")
    {
        Description = "Maximum new predicate clone clusters tolerated in incremental mode",
        Recursive = true
    };

    public Option<int?> PredicateOccurrencesOption { get; } = new("--predicate-occurrences", "-po", "--predicate-min-occurrences")
    {
        Description = "Minimum occurrences for predicate clone detection (default: 2)",
        Recursive = true
    };

    // Comments & XML doc thresholds
    public Option<string?> SummaryOption { get; } = new("--summary", "--summary-mode")
    {
        Description = "Comment summary mode: summarize, report, or disabled",
        Recursive = true
    };

    public Option<bool> ReportOnlyOption { get; } = new("--report-only")
    {
        Description = "Report-only mode for comments without calling an LLM (alias for --summary report)",
        Recursive = true
    };

    public Option<bool> NoCommentsOption { get; } = new("--no-comments", "--disable-comments", "--no-comment-summarization")
    {
        Description = "Disable comment extraction and summarization",
        Recursive = true
    };

    public Option<bool> EnableCommentsOption { get; } = new("--enable-comments", "--enable-comment-summarization")
    {
        Description = "Explicitly enable comment summarization",
        Recursive = true
    };

    public Option<int?> ThresholdCommentsOption { get; } = new("--threshold-comments", "-tc")
    {
        Description = "Minimum character length for verbose comments (default: 100)",
        Recursive = true
    };

    public Option<int?> ThresholdXmlSummaryOption { get; } = new("--threshold-xml-summary", "-txs")
    {
        Description = "Minimum character length for XML <summary> and <remarks> tags",
        Recursive = true
    };

    public Option<int?> ThresholdXmlParamOption { get; } = new("--threshold-xml-param", "-txp")
    {
        Description = "Minimum character length for XML <param> tags",
        Recursive = true
    };

    public Option<string?> SummarizedCommentsOption { get; } = new("--summarized-comments", "-sc")
    {
        Description = "Path to summarized comments JSON file stored in git (default: .codecondenser/summarized-comments.json)",
        Recursive = true
    };

    public Option<int?> MaxLineLengthOption { get; } = new("--max-line-length", "-mll")
    {
        Description = "Maximum line length for formatted comments (default: 120)",
        Recursive = true
    };

    // LLM configuration
    public Option<string?> ModelOption { get; } = new("--model", "-m")
    {
        Description = "LLM model name override (e.g. gemma4:e4b)",
        Recursive = true
    };

    public Option<string?> EndpointOption { get; } = new("--endpoint", "-e")
    {
        Description = "Ollama endpoint URL override (e.g. http://localhost:11434)",
        Recursive = true
    };

    public Option<int?> ContextSizeOption { get; } = new("--context-size", "-cs")
    {
        Description = "Context window size in tokens override (e.g. 24576)",
        Recursive = true
    };

    public Option<int?> MaxRetriesOption { get; } = new("--max-retries", "-mr")
    {
        Description = "Maximum number of LLM summarization retries (default: 1)",
        Recursive = true
    };

    public Option<string?> TokenEstimatorOption { get; } = new("--estimator", "-te", "--token-estimator")
    {
        Description = "Token estimator preset override (default, qwen, or gemma)",
        Recursive = true
    };

    public void AddTo(RootCommand root)
    {
        root.Add(ConfigOption);
        root.Add(GlobalConfigOption);
        root.Add(NoGlobalConfigOption);

        root.Add(FormatOption);
        root.Add(OutputOption);
        root.Add(NoColorOption);

        root.Add(IncludeOption);
        root.Add(ExcludeOption);
        root.Add(ExcludeTestsOption);
        root.Add(IncludeGeneratedOption);
        root.Add(DryRunOption);

        root.Add(IncrementalOption);
        root.Add(DuplicationToleranceOption);
        root.Add(CommentToleranceOption);
        root.Add(StringToleranceOption);
        root.Add(EnforceTestDuplicationOption);

        root.Add(NoDuplicationOption);
        root.Add(EnableDuplicationOption);
        root.Add(ThresholdStatementsOption);
        root.Add(SubMethodClonesOption);
        root.Add(ThresholdSubMethodStatementsOption);

        root.Add(NoStringsOption);
        root.Add(EnableStringsOption);
        root.Add(ThresholdRepeatedStringsOption);
        root.Add(ThresholdStringOccurrencesOption);
        root.Add(ThresholdExistingConstantsOption);

        root.Add(NoTypeSplitOption);
        root.Add(EnableTypeSplitOption);
        root.Add(NoTypeRenameOption);
        root.Add(TypeLineToleranceOption);
        root.Add(TypeToleranceOption);

        root.Add(NoLocOption);
        root.Add(EnableLocOption);
        root.Add(ThresholdMethodLinesOption);
        root.Add(ThresholdClassLinesOption);
        root.Add(LocToleranceOption);

        root.Add(NoPredicatesOption);
        root.Add(EnablePredicatesOption);
        root.Add(ThresholdPredicateLengthOption);
        root.Add(ThresholdExistingHelperPredicateLengthOption);
        root.Add(PredicateToleranceOption);
        root.Add(PredicateOccurrencesOption);

        root.Add(SummaryOption);
        root.Add(ReportOnlyOption);
        root.Add(NoCommentsOption);
        root.Add(EnableCommentsOption);
        root.Add(ThresholdCommentsOption);
        root.Add(ThresholdXmlSummaryOption);
        root.Add(ThresholdXmlParamOption);
        root.Add(SummarizedCommentsOption);
        root.Add(MaxLineLengthOption);

        root.Add(ModelOption);
        root.Add(EndpointOption);
        root.Add(ContextSizeOption);
        root.Add(MaxRetriesOption);
        root.Add(TokenEstimatorOption);
    }

    public CliOptions BindCliOptions(ParseResult pr, string commandName, string targetPath)
    {
        var options = new CliOptions
        {
            Command = commandName,
            TargetPath = string.IsNullOrWhiteSpace(targetPath) ? "." : targetPath,

            ConfigPath = pr.GetValue(ConfigOption),
            GlobalConfigPath = pr.GetValue(GlobalConfigOption),
            NoGlobalConfig = pr.GetValue(NoGlobalConfigOption),

            Format = pr.GetValue(FormatOption),
            OutputFilePath = pr.GetValue(OutputOption),
            NoColor = pr.GetValue(NoColorOption),
            DryRun = pr.GetValue(DryRunOption),

            ExcludeTests = pr.GetValue(ExcludeTestsOption),
            IncludeGenerated = pr.GetValue(IncludeGeneratedOption),

            Incremental = pr.GetValue(IncrementalOption),
            DuplicationTolerance = pr.GetValue(DuplicationToleranceOption),
            CommentTolerance = pr.GetValue(CommentToleranceOption),
            RepeatedStringTolerance = pr.GetValue(StringToleranceOption),
            EnforceTestDuplication = pr.GetValue(EnforceTestDuplicationOption),

            ThresholdStatements = pr.GetValue(ThresholdStatementsOption),
            SubMethodClones = pr.GetValue(SubMethodClonesOption),
            ThresholdSubMethodStatements = pr.GetValue(ThresholdSubMethodStatementsOption),

            ThresholdRepeatedStrings = pr.GetValue(ThresholdRepeatedStringsOption),
            ThresholdStringOccurrences = pr.GetValue(ThresholdStringOccurrencesOption),
            ThresholdExistingConstants = pr.GetValue(ThresholdExistingConstantsOption),

            TypeLineTolerance = pr.GetValue(TypeLineToleranceOption),
            TypeTolerance = pr.GetValue(TypeToleranceOption),

            MethodLinesOfCodeThreshold = pr.GetValue(ThresholdMethodLinesOption),
            ClassLinesOfCodeThreshold = pr.GetValue(ThresholdClassLinesOption),
            LinesOfCodeTolerance = pr.GetValue(LocToleranceOption),

            PredicateTolerance = pr.GetValue(PredicateToleranceOption),
            PredicateLengthThreshold = pr.GetValue(ThresholdPredicateLengthOption),
            ExistingHelperPredicateLengthThreshold = pr.GetValue(ThresholdExistingHelperPredicateLengthOption),
            PredicateMinOccurrences = pr.GetValue(PredicateOccurrencesOption),

            ThresholdComments = pr.GetValue(ThresholdCommentsOption),
            ThresholdXmlSummary = pr.GetValue(ThresholdXmlSummaryOption),
            ThresholdXmlParam = pr.GetValue(ThresholdXmlParamOption),
            SummarizedCommentsPath = pr.GetValue(SummarizedCommentsOption),
            MaxLineLength = pr.GetValue(MaxLineLengthOption),

            Model = pr.GetValue(ModelOption),
            Endpoint = pr.GetValue(EndpointOption),
            ContextSize = pr.GetValue(ContextSizeOption),
            MaxSummarizationRetries = pr.GetValue(MaxRetriesOption),
            TokenEstimator = pr.GetValue(TokenEstimatorOption)
        };

        var inc = pr.GetValue(IncludeOption);
        if (inc is { Length: > 0 })
        {
            foreach (var item in inc)
            {
                options.Include.Add(item);
            }
        }

        var exc = pr.GetValue(ExcludeOption);
        if (exc is { Length: > 0 })
        {
            foreach (var item in exc)
            {
                options.Exclude.Add(item);
            }
        }

        // Feature enable/disable flags
        if (pr.GetResult(NoDuplicationOption) is { Implicit: false } && pr.GetValue(NoDuplicationOption))
        {
            options.EnableDuplicationDetection = false;
        }
        else if (pr.GetResult(EnableDuplicationOption) is { Implicit: false } && pr.GetValue(EnableDuplicationOption))
        {
            options.EnableDuplicationDetection = true;
        }

        if (pr.GetResult(NoStringsOption) is { Implicit: false } && pr.GetValue(NoStringsOption))
        {
            options.EnableRepeatedStrings = false;
        }
        else if (pr.GetResult(EnableStringsOption) is { Implicit: false } && pr.GetValue(EnableStringsOption))
        {
            options.EnableRepeatedStrings = true;
        }

        if (pr.GetResult(NoTypeSplitOption) is { Implicit: false } && pr.GetValue(NoTypeSplitOption))
        {
            options.EnableTypeSplitting = false;
        }
        else if (pr.GetResult(EnableTypeSplitOption) is { Implicit: false } && pr.GetValue(EnableTypeSplitOption))
        {
            options.EnableTypeSplitting = true;
        }

        if (pr.GetResult(NoTypeRenameOption) is { Implicit: false } && pr.GetValue(NoTypeRenameOption))
        {
            options.EnableTypeRenaming = false;
        }

        if (pr.GetResult(NoLocOption) is { Implicit: false } && pr.GetValue(NoLocOption))
        {
            options.EnableLinesOfCodeDetection = false;
        }
        else if (pr.GetResult(EnableLocOption) is { Implicit: false } && pr.GetValue(EnableLocOption))
        {
            options.EnableLinesOfCodeDetection = true;
        }

        if (pr.GetResult(NoPredicatesOption) is { Implicit: false } && pr.GetValue(NoPredicatesOption))
        {
            options.EnablePredicateCloneDetection = false;
        }
        else if (pr.GetResult(EnablePredicatesOption) is { Implicit: false } && pr.GetValue(EnablePredicatesOption))
        {
            options.EnablePredicateCloneDetection = true;
        }

        var summary = pr.GetValue(SummaryOption);
        if (!string.IsNullOrWhiteSpace(summary))
        {
            if (summary.Equals("disabled", StringComparison.OrdinalIgnoreCase) ||
                summary.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                summary.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                summary.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                options.SummaryMode = SummaryMode.Disabled;
                options.EnableCommentSummarization = false;
            }
            else if (summary.Equals("report", StringComparison.OrdinalIgnoreCase) ||
                     summary.Equals("report-only", StringComparison.OrdinalIgnoreCase))
            {
                options.SummaryMode = SummaryMode.Report;
            }
            else if (summary.Equals("summarize", StringComparison.OrdinalIgnoreCase) ||
                     summary.Equals("condense", StringComparison.OrdinalIgnoreCase) ||
                     summary.Equals("llm", StringComparison.OrdinalIgnoreCase) ||
                     summary.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                options.SummaryMode = SummaryMode.Summarize;
                options.EnableCommentSummarization = true;
            }
        }
        else if (pr.GetResult(ReportOnlyOption) is { Implicit: false } && pr.GetValue(ReportOnlyOption))
        {
            options.SummaryMode = SummaryMode.Report;
        }

        if (pr.GetResult(NoCommentsOption) is { Implicit: false } && pr.GetValue(NoCommentsOption))
        {
            options.EnableCommentSummarization = false;
            options.SummaryMode = SummaryMode.Disabled;
        }
        else if (pr.GetResult(EnableCommentsOption) is { Implicit: false } && pr.GetValue(EnableCommentsOption))
        {
            options.EnableCommentSummarization = true;
            options.SummaryMode = SummaryMode.Summarize;
        }

        return options;
    }
}
