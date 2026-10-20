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
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(content.Length);
        byte[]? rented = null;
        Span<byte> utf8Bytes = maxByteCount <= 1024
            ? stackalloc byte[maxByteCount]
            : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(maxByteCount));

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

        var normBase = basePath.Replace('\\', '/');
        if (normBase.Length > 1 && normBase.EndsWith('/'))
        {
            normBase = normBase.TrimEnd('/');
        }

        var normPath = fullOrRelativePath.Replace('\\', '/');
        while (normPath.StartsWith("./", StringComparison.Ordinal))
        {
            normPath = normPath[2..];
        }

        if (string.Equals(normBase, normPath, StringComparison.OrdinalIgnoreCase))
        {
            return ".";
        }

        if (normBase == "/" && normPath.StartsWith('/'))
        {
            return normPath[1..];
        }

        if (normPath.StartsWith(normBase + "/", StringComparison.OrdinalIgnoreCase))
        {
            return normPath[(normBase.Length + 1)..];
        }

        if (!IsRooted(normPath))
        {
            return normPath;
        }

        if (Path.IsPathRooted(basePath) && Path.IsPathRooted(fullOrRelativePath))
        {
            try
            {
                var rel = Path.GetRelativePath(basePath, fullOrRelativePath).Replace('\\', '/');
                if (rel.StartsWith("./", StringComparison.Ordinal))
                {
                    rel = rel[2..];
                }
                return rel;
            }
            catch
            {
                // Fall back to manual computation
            }
        }

        return ComputeRelativePath(normBase, normPath);
    }

    private static bool IsRooted(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return true;
        }

        if (path.StartsWith('/'))
        {
            return true;
        }

        if (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
        {
            return true;
        }

        return false;
    }

    private static string ComputeRelativePath(string fromPath, string toPath)
    {
        var fromSegments = fromPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var toSegments = toPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (fromSegments.Length > 0 && toSegments.Length > 0 &&
            fromSegments[0].EndsWith(':') && toSegments[0].EndsWith(':') &&
            !string.Equals(fromSegments[0], toSegments[0], StringComparison.OrdinalIgnoreCase))
        {
            return toPath;
        }

        int commonCount = 0;
        int maxCommon = Math.Min(fromSegments.Length, toSegments.Length);
        while (commonCount < maxCommon &&
               string.Equals(fromSegments[commonCount], toSegments[commonCount], StringComparison.OrdinalIgnoreCase))
        {
            commonCount++;
        }

        var sb = new StringBuilder();
        for (int i = commonCount; i < fromSegments.Length; i++)
        {
            if (sb.Length > 0) sb.Append('/');
            sb.Append("..");
        }

        for (int i = commonCount; i < toSegments.Length; i++)
        {
            if (sb.Length > 0) sb.Append('/');
            sb.Append(toSegments[i]);
        }

        return sb.Length > 0 ? sb.ToString() : ".";
    }
}
