using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pmad.CodeCondenser.Core.Snapshots;

namespace Pmad.CodeCondenser.Infrastructure.Storage;

/// <summary>
/// JSON file-based implementation of <see cref="ISnapshotStore"/>.
/// </summary>
public sealed class JsonSnapshotStore : ISnapshotStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<JsonSnapshotStore>? _logger;

    public JsonSnapshotStore(ILogger<JsonSnapshotStore>? logger = null)
    {
        _logger = logger;
    }

    public async Task<AnalysisSnapshot?> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<AnalysisSnapshot>(stream, SerializerOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            if (_logger != null)
            {
                _logger.LogWarning(ex, "Failed to load snapshot from '{FilePath}'. The snapshot file may be corrupt or unreadable.", filePath);
            }
            else
            {
                Console.Error.WriteLine($"[Warning] Failed to load snapshot from '{filePath}': {ex.Message}. Treating as fresh run.");
            }

            return null;
        }
    }

    public async Task SaveAsync(string filePath, AnalysisSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(snapshot);

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = filePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, cancellationToken);
            }

            File.Move(tempPath, filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            if (_logger != null)
            {
                _logger.LogError(ex, "Failed to save snapshot to '{FilePath}'.", filePath);
            }
            else
            {
                Console.Error.WriteLine($"[Error] Failed to save snapshot to '{filePath}': {ex.Message}.");
            }

            throw;
        }
    }
}
