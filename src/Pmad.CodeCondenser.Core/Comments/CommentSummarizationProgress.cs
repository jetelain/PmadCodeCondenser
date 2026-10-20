using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Comments;

/// <summary>
/// Progress information reported during verbose comment LLM summarization.
/// </summary>
/// <param name="Completed">Number of comments summarized so far.</param>
/// <param name="Total">Total number of comments to summarize.</param>
/// <param name="CurrentComment">The comment that was just processed (if available).</param>
public sealed record CommentSummarizationProgress(
    int Completed,
    int Total,
    VerboseComment? CurrentComment = null)
{
    /// <summary>
    /// Gets the percentage of comments summarized (0 to 100).
    /// </summary>
    public int Percentage => Total > 0 ? (int)Math.Round((double)Completed / Total * 100) : 100;
}
