using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pmad.CodeCondenser.Core.Snapshots;

namespace Pmad.CodeCondenser.Infrastructure.Storage;

/// <summary>
/// JSON file-based implementation of <see cref="ISubMethodCacheStore"/>.
/// </summary>
public sealed class JsonSubMethodCacheStore : ISubMethodCacheStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<JsonSubMethodCacheStore>? _logger;

    public JsonSubMethodCacheStore(ILogger<JsonSubMethodCacheStore>? logger = null)
    {
        _logger = logger;
    }

    public async Task<SubMethodCacheModel?> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<SubMethodCacheModel>(stream, SerializerOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            if (_logger != null)
            {
                _logger.LogWarning(ex, "Failed to load sub-method cache from '{FilePath}'.", filePath);
            }
            else
            {
                Console.Error.WriteLine($"[Warning] Failed to load sub-method cache from '{filePath}': {ex.Message}. Treating as empty cache.");
            }

            return null;
        }
    }

    public async Task SaveAsync(string filePath, SubMethodCacheModel cache, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(cache);

        string? tempPath = null;
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            tempPath = filePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, cache, SerializerOptions, cancellationToken);
            }

            File.Move(tempPath, filePath, overwrite: true);
            tempPath = null;
        }
        catch (Exception ex)
        {
            if (tempPath != null && File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Best effort cleanup
                }
            }

            if (_logger != null)
            {
                _logger.LogError(ex, "Failed to save sub-method cache to '{FilePath}'.", filePath);
            }
            else
            {
                Console.Error.WriteLine($"[Error] Failed to save sub-method cache to '{filePath}': {ex.Message}.");
            }

            throw;
        }
    }
}
