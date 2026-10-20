using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Comments;

/// <summary>
/// Summarizes verbose comments using an LLM client with cache awareness.
/// </summary>
public interface ICommentSummarizer
{
    Task<IReadOnlyDictionary<string, string>> SummarizeAsync(
        IReadOnlyList<VerboseComment> comments,
        IReadOnlyDictionary<string, string> existingCache,
        CodeCondenserSettings settings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Summarizes verbose comments with progress reporting support.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> SummarizeAsync(
        IReadOnlyList<VerboseComment> comments,
        IReadOnlyDictionary<string, string> existingCache,
        CodeCondenserSettings settings,
        IProgress<CommentSummarizationProgress>? progress,
        CancellationToken cancellationToken = default)
        => SummarizeAsync(comments, existingCache, settings, cancellationToken);
}
