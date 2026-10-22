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
    public bool EnableSubMethodCloneDetection { get; set; } = false;

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
    public int DuplicationTolerance { get; set; } = 0;

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
    public bool ExcludeTests { get; set; } = false;

    /// <summary>
    /// When false (default), test clones are reported for review but do not count toward CI tolerance failure.
    /// When true, test clones count toward CI gate failure.
    /// </summary>
    public bool EnforceTestDuplication { get; set; } = false;

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
            RepeatedStringMinOccurrences = RepeatedStringMinOccurrences,
            RepeatedStringTolerance = RepeatedStringTolerance,
            RepeatedStringExitCode = RepeatedStringExitCode,
            IgnoredStrings = new HashSet<string>(IgnoredStrings, StringComparer.Ordinal)
        };
    }
}
