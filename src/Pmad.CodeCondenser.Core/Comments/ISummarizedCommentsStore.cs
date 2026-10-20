namespace Pmad.CodeCondenser.Core.Comments;

/// <summary>
/// Abstraction for persisting and retrieving summarized comments stored in git.
/// </summary>
public interface ISummarizedCommentsStore
{
    /// <summary>
    /// Loads the stored summarized comments mapping from the specified file path.
    /// Returns a dictionary mapping comment content hashes to their summarized text.
    /// </summary>
    /// <param name="filePath">The file path to load from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary of summarized comment hashes and texts.</returns>
    Task<IReadOnlyDictionary<string, string>> LoadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the summarized comments mapping to the specified file path.
    /// </summary>
    /// <param name="filePath">The file path to save to.</param>
    /// <param name="summarizedComments">The dictionary of summarized comment hashes and texts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(string filePath, IReadOnlyDictionary<string, string> summarizedComments, CancellationToken cancellationToken = default);
}
