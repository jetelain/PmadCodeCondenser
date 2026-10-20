using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Snapshots;

/// <summary>
/// Helper for constructing and parsing unique method keys used in snapshot method hashes.
/// Format: "{normalizedFilePath}::{methodSignatureOrName}".
/// </summary>
public static class MethodKeyHelper
{
    public const string Separator = "::";

    /// <summary>
    /// Builds a normalized method key combining file path and signature or method name.
    /// </summary>
    public static string BuildKey(string filePath, string methodSignatureOrName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodSignatureOrName);

        var normalizedPath = filePath.Replace('\\', '/');
        return $"{normalizedPath}{Separator}{methodSignatureOrName}";
    }

    /// <summary>
    /// Builds a method key for a <see cref="MethodCloneInfo"/> instance using its signature.
    /// </summary>
    public static string BuildKey(MethodCloneInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return BuildKey(method.FilePath, method.Signature);
    }

    /// <summary>
    /// Attempts to parse a method key into its file path and method identifier parts.
    /// </summary>
    public static bool TryParseKey(string key, out string filePath, out string methodIdentifier)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            filePath = string.Empty;
            methodIdentifier = string.Empty;
            return false;
        }

        var idx = key.IndexOf(Separator, StringComparison.Ordinal);
        if (idx >= 0)
        {
            filePath = key[..idx];
            methodIdentifier = key[(idx + Separator.Length)..];
            return true;
        }

        filePath = string.Empty;
        methodIdentifier = string.Empty;
        return false;
    }

    /// <summary>
    /// Extracts the file path portion of a method key.
    /// </summary>
    public static string GetFilePath(string key)
    {
        return TryParseKey(key, out var filePath, out _) ? filePath : string.Empty;
    }
}
