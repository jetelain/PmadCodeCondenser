using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pmad.CodeCondenser.Core.Comments;

namespace Pmad.CodeCondenser.Infrastructure.Storage;

/// <summary>
/// JSON file-based implementation of <see cref="ISummarizedCommentsStore"/> designed to be committed to git.
/// </summary>
public sealed class JsonSummarizedCommentsStore : ISummarizedCommentsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<JsonSummarizedCommentsStore>? _logger;

    public JsonSummarizedCommentsStore(ILogger<JsonSummarizedCommentsStore>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = doc.RootElement;
            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("comments", out var commentsProp) && commentsProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in commentsProp.EnumerateObject())
                    {
                        result[prop.Name] = prop.Value.GetString() ?? string.Empty;
                    }
                }
                else
                {
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (!string.Equals(prop.Name, "version", StringComparison.OrdinalIgnoreCase))
                        {
                            result[prop.Name] = prop.Value.GetString() ?? string.Empty;
                        }
                    }
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    var val = item.GetString();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        result[val] = string.Empty;
                    }
                }
            }

            return result;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            if (_logger != null)
            {
                _logger.LogWarning(ex, "Failed to load summarized comments from '{FilePath}'.", filePath);
            }
            else
            {
                Console.Error.WriteLine($"[Warning] Failed to load summarized comments from '{filePath}': {ex.Message}.");
            }

            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(string filePath, IReadOnlyDictionary<string, string> summarizedComments, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(summarizedComments);

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Order entries deterministically by hash for clean git diffs
            var sortedComments = new SortedDictionary<string, string>(
                summarizedComments.ToDictionary(k => k.Key, v => v.Value),
                StringComparer.Ordinal);

            var model = new SummarizedCommentsDocument
            {
                Version = 1,
                Comments = sortedComments
            };

            var tempPath = filePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, model, SerializerOptions, cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            if (_logger != null)
            {
                _logger.LogError(ex, "Failed to save summarized comments to '{FilePath}'.", filePath);
            }
            else
            {
                Console.Error.WriteLine($"[Error] Failed to save summarized comments to '{filePath}': {ex.Message}.");
            }

            throw;
        }
    }

    private sealed class SummarizedCommentsDocument
    {
        public int Version { get; set; } = 1;
        public IDictionary<string, string> Comments { get; set; } = new Dictionary<string, string>();
    }
}
