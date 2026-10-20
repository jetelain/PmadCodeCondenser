using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Engine;

/// <summary>
/// Top-level engine interface orchestrating code clone analysis, comment extraction, and rewriting.
/// </summary>
public interface ICodeCondenserEngine
{
    Task<AnalysisResult> AnalyzeAsync(
        string targetPath,
        CodeCondenserSettings settings,
        bool incremental = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes the codebase with optional progress reporting for comment summarization.
    /// </summary>
    Task<AnalysisResult> AnalyzeAsync(
        string targetPath,
        CodeCondenserSettings settings,
        bool incremental,
        IProgress<Comments.CommentSummarizationProgress>? progress,
        CancellationToken cancellationToken = default)
        => AnalyzeAsync(targetPath, settings, incremental, cancellationToken);

    Task ApplySummariesAsync(
        AnalysisResult analysisResult,
        CodeCondenserSettings settings,
        CancellationToken cancellationToken = default);
}
