namespace Pmad.CodeCondenser.Core.Snapshots;

/// <summary>
/// Service for computing cryptographic hashes of files and content, and normalizing workspace relative paths.
/// </summary>
public interface IFileHasher
{
    /// <summary>
    /// Computes the SHA-256 hash of a UTF-8 text string as lowercase hexadecimal.
    /// </summary>
    string ComputeHash(string content);

    /// <summary>
    /// Computes the SHA-256 hash of a byte array as lowercase hexadecimal.
    /// </summary>
    string ComputeHash(byte[] bytes);

    /// <summary>
    /// Computes the SHA-256 hash of a stream as lowercase hexadecimal.
    /// </summary>
    string ComputeHash(Stream stream);

    /// <summary>
    /// Computes the SHA-256 hash of a file on disk as lowercase hexadecimal.
    /// </summary>
    string ComputeFileHash(string filePath);

    /// <summary>
    /// Computes the SHA-256 hash of a file on disk asynchronously as lowercase hexadecimal.
    /// </summary>
    Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Normalizes a file path relative to a base directory, using forward slashes ('/') for cross-platform consistency.
    /// </summary>
    string NormalizeRelativePath(string basePath, string fullOrRelativePath);
}
