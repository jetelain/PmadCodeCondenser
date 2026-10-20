using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Snapshots;

/// <summary>
/// Thread-safe in-memory WYSIWYG comment cache backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class WysiwygCommentCache : ICommentCache
{
    private readonly ConcurrentDictionary<string, string> _cache;

    /// <summary>
    /// Initializes an empty instance of <see cref="WysiwygCommentCache"/>.
    /// </summary>
    public WysiwygCommentCache()
    {
        _cache = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="WysiwygCommentCache"/> pre-populated with initial entries.
    /// </summary>
    public WysiwygCommentCache(IEnumerable<KeyValuePair<string, string>> initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        _cache = new ConcurrentDictionary<string, string>(initial, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public int Count => _cache.Count;

    /// <inheritdoc />
    public IEnumerable<string> Keys => _cache.Keys;

    /// <inheritdoc />
    public IEnumerable<string> Values => _cache.Values;

    /// <inheritdoc />
    public string this[string key]
    {
        get => _cache[key];
        set
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(value);
            _cache[key] = value;
        }
    }

    /// <inheritdoc />
    public bool TryGet(string contentHash, [NotNullWhen(true)] out string? summary)
    {
        ArgumentNullException.ThrowIfNull(contentHash);
        return _cache.TryGetValue(contentHash, out summary);
    }

    /// <inheritdoc />
    public bool TryGet(VerboseComment comment, [NotNullWhen(true)] out string? summary)
    {
        ArgumentNullException.ThrowIfNull(comment);
        return TryGet(comment.ContentHash, out summary);
    }

    /// <inheritdoc />
    public void Set(string contentHash, string summary)
    {
        ArgumentNullException.ThrowIfNull(contentHash);
        ArgumentNullException.ThrowIfNull(summary);
        _cache[contentHash] = summary;
    }

    /// <inheritdoc />
    public void Set(VerboseComment comment, string summary)
    {
        ArgumentNullException.ThrowIfNull(comment);
        Set(comment.ContentHash, summary);
    }

    /// <inheritdoc />
    public void SetRange(IEnumerable<KeyValuePair<string, string>> summaries)
    {
        ArgumentNullException.ThrowIfNull(summaries);
        foreach (var (key, value) in summaries)
        {
            _cache[key] = value;
        }
    }

    /// <inheritdoc />
    public bool Contains(string contentHash)
    {
        ArgumentNullException.ThrowIfNull(contentHash);
        return _cache.ContainsKey(contentHash);
    }

    /// <inheritdoc />
    public bool Contains(VerboseComment comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        return Contains(comment.ContentHash);
    }

    /// <inheritdoc />
    public void LoadFrom(AnalysisSnapshot? snapshot)
    {
        if (snapshot?.CommentCache == null)
        {
            return;
        }

        foreach (var (key, value) in snapshot.CommentCache)
        {
            _cache[key] = value;
        }
    }

    /// <inheritdoc />
    public void SaveTo(AnalysisSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.CommentCache = new Dictionary<string, string>(_cache, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>(_cache, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public void Clear() => _cache.Clear();

    /// <inheritdoc />
    public bool ContainsKey(string key) => _cache.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value) => _cache.TryGetValue(key, out value);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _cache.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Creates and initializes a <see cref="WysiwygCommentCache"/> instance from a snapshot if available.
    /// </summary>
    public static WysiwygCommentCache FromSnapshot(AnalysisSnapshot? snapshot)
    {
        var cache = new WysiwygCommentCache();
        cache.LoadFrom(snapshot);
        return cache;
    }
}
