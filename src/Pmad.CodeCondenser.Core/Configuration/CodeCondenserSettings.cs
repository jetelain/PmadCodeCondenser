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
