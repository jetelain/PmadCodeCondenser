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
}
