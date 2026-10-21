namespace Pmad.CodeCondenser.Core.Configuration;

/// <summary>
/// Specifies the mode for comment summarization and reporting.
/// </summary>
public enum SummaryMode
{
    /// <summary>
    /// Comment extraction and summarization are disabled.
    /// </summary>
    Disabled,

    /// <summary>
    /// Verbose comments are extracted and summarized using an LLM.
    /// Default mode when an LLM is configured.
    /// </summary>
    Summarize,

    /// <summary>
    /// CI/AI agent friendly report-only mode: verbose comments are extracted and reported
    /// with actionable guidance and suppression tips, without calling an LLM.
    /// Default mode when no LLM is configured.
    /// </summary>
    Report
}
