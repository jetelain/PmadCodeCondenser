using System.Diagnostics.CodeAnalysis;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Snapshots;

/// <summary>
/// Thread-safe WYSIWYG comment cache that maps SHA-256 digests of original comments to summarized text.
/// Ensures reproducible comment rewrites and eliminates redundant LLM calls.
/// </summary>
public interface ICommentCache : IReadOnlyDictionary<string, string>
{
    /// <summary>
    /// Attempts to retrieve the cached summary for the given comment content hash.
    /// </summary>
    bool TryGet(string contentHash, [NotNullWhen(true)] out string? summary);

    /// <summary>
    /// Attempts to retrieve the cached summary for the specified verbose comment.
    /// </summary>
    bool TryGet(VerboseComment comment, [NotNullWhen(true)] out string? summary);

    /// <summary>
    /// Adds or updates a comment summary by content hash.
    /// </summary>
    void Set(string contentHash, string summary);

    /// <summary>
    /// Adds or updates a comment summary for the specified verbose comment.
    /// </summary>
    void Set(VerboseComment comment, string summary);

    /// <summary>
    /// Adds or updates a batch of comment summaries.
    /// </summary>
    void SetRange(IEnumerable<KeyValuePair<string, string>> summaries);

    /// <summary>
    /// Checks whether the cache contains a summary for the given content hash.
    /// </summary>
    bool Contains(string contentHash);

    /// <summary>
    /// Checks whether the cache contains a summary for the specified verbose comment.
    /// </summary>
    bool Contains(VerboseComment comment);

    /// <summary>
    /// Loads comment summaries from the specified snapshot.
    /// </summary>
    void LoadFrom(AnalysisSnapshot? snapshot);

    /// <summary>
    /// Persists current cache state into the target snapshot.
    /// </summary>
    void SaveTo(AnalysisSnapshot snapshot);

    /// <summary>
    /// Returns a snapshot dictionary of all cached entries.
    /// </summary>
    IReadOnlyDictionary<string, string> ToDictionary();

    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    void Clear();
}
