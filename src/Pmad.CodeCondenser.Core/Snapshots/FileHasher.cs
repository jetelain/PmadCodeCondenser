using System.Security.Cryptography;
using System.Text;

namespace Pmad.CodeCondenser.Core.Snapshots;

/// <summary>
/// Default implementation of <see cref="IFileHasher"/> computing SHA-256 lowercase hex digests.
/// </summary>
public sealed class FileHasher : IFileHasher
{
    /// <inheritdoc />
    public string ComputeHash(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        int byteCount = Encoding.UTF8.GetByteCount(content);
        byte[]? rented = null;
        Span<byte> utf8Bytes = byteCount <= 1024
            ? stackalloc byte[byteCount]
            : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(byteCount));

        try
        {
            int written = Encoding.UTF8.GetBytes(content, utf8Bytes);
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(utf8Bytes[..written], hash);
            return Convert.ToHexStringLower(hash);
        }
        finally
        {
            if (rented != null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <inheritdoc />
    public string ComputeHash(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(bytes, hash);
        return Convert.ToHexStringLower(hash);
    }

    /// <inheritdoc />
    public string ComputeHash(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexStringLower(hash);
    }

    /// <inheritdoc />
    public string ComputeFileHash(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 16384, useAsync: false);
        return ComputeHash(stream);
    }

    /// <inheritdoc />
    public async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 16384, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    /// <inheritdoc />
    public string NormalizeRelativePath(string basePath, string fullOrRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullOrRelativePath);

        string relative;
        if (Path.IsPathRooted(fullOrRelativePath))
        {
            relative = Path.GetRelativePath(basePath, fullOrRelativePath);
        }
        else
        {
            relative = fullOrRelativePath;
        }

        var normalized = relative.Replace('\\', '/');
        if (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized;
    }
}
