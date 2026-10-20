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
    /// Relative or absolute path to the snapshot cache file for incremental analysis and WYSIWYG summaries.
    /// Default is ".codecondenser/snapshot.json".
    /// </summary>
    public string SnapshotFilePath { get; set; } = ".codecondenser/snapshot.json";
}
