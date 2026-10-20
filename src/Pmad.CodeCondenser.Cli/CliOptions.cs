namespace Pmad.CodeCondenser.Cli;

/// <summary>
/// Parsed command line options for the pmad-codecondenser CLI tool.
/// </summary>
public sealed class CliOptions
{
    public const string CommandAnalyze = "analyze";
    public const string CommandApply = "apply";
    public const string CommandSnapshot = "snapshot";

    /// <summary>
    /// Active command verb: "analyze" (default), "apply", or "snapshot".
    /// </summary>
    public string Command { get; set; } = CommandAnalyze;

    /// <summary>
    /// Target directory or C# file path to analyze.
    /// Default is current directory (".").
    /// </summary>
    public string TargetPath { get; set; } = ".";

    /// <summary>
    /// When true, writes shortened comments directly to source files on disk.
    /// </summary>
    public bool Apply { get; set; }

    /// <summary>
    /// When true, uses snapshot baseline to analyze only new or modified files and report new clones/comments.
    /// </summary>
    public bool Incremental { get; set; }

    /// <summary>
    /// When true, refreshes the snapshot baseline with current file hashes and method hashes.
    /// </summary>
    public bool UpdateSnapshot { get; set; }

    /// <summary>
    /// Override minimum method statement count for clone detection.
    /// </summary>
    public int? ThresholdStatements { get; set; }

    /// <summary>
    /// Override minimum comment character length for verbosity detection.
    /// </summary>
    public int? ThresholdComments { get; set; }

    /// <summary>
    /// Override minimum character length for XML &lt;summary&gt; and &lt;remarks&gt; tags.
    /// </summary>
    public int? ThresholdXmlSummary { get; set; }

    /// <summary>
    /// Override minimum character length for XML &lt;param&gt; tags.
    /// </summary>
    public int? ThresholdXmlParam { get; set; }

    /// <summary>
    /// LLM model name override (e.g. "gemma4:e4b").
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Ollama endpoint URL override (e.g. "http://localhost:11434").
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Context window size override for LLM requests (e.g. 24576).
    /// </summary>
    public int? ContextSize { get; set; }

    /// <summary>
    /// Optional explicit path to codecondenser.json configuration file.
    /// </summary>
    public string? ConfigPath { get; set; }

    /// <summary>
    /// When true, disables ANSI color output in the console.
    /// </summary>
    public bool NoColor { get; set; }

    /// <summary>
    /// When specified, explicitly enables or disables comment summarization.
    /// </summary>
    public bool? EnableCommentSummarization { get; set; }

    /// <summary>
    /// When specified, explicitly enables or disables duplication detection.
    /// </summary>
    public bool? EnableDuplicationDetection { get; set; }

    /// <summary>
    /// Maximum number of new clone clusters tolerated in incremental mode before returning an error exit code.
    /// </summary>
    public int? DuplicationTolerance { get; set; }

    /// <summary>
    /// When true, shows help and usage information.
    /// </summary>
    public bool Help { get; set; }

    /// <summary>
    /// When true, shows application version.
    /// </summary>
    public bool Version { get; set; }
}
