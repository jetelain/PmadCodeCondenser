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
            IncludeXmlDocumentation = IncludeXmlDocumentation,
            MaxParallelLlmRequests = MaxParallelLlmRequests,
            Provider = Provider,
            Ollama = new OllamaSettings
            {
                Endpoint = Ollama.Endpoint,
                Model = Ollama.Model,
                ContextSize = Ollama.ContextSize
            },
            EnableCommentSummarization = EnableCommentSummarization,
            EnableDuplicationDetection = EnableDuplicationDetection,
            DuplicationTolerance = DuplicationTolerance,
            DuplicationExitCode = DuplicationExitCode,
            SnapshotFilePath = SnapshotFilePath,
            SummarizedCommentsFilePath = SummarizedCommentsFilePath,
            MaxCommentLineLength = MaxCommentLineLength,
            IgnoredCommentHashes = new HashSet<string>(IgnoredCommentHashes, StringComparer.Ordinal)
        };
    }
}
