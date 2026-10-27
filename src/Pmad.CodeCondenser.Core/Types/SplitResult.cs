namespace Pmad.CodeCondenser.Core.Types;

/// <summary>
/// Result of splitting a source file into one or more new type files.
/// </summary>
public sealed class SplitResult
{
    /// <summary>
    /// The original file path.
    /// </summary>
    public required string OriginalFilePath { get; init; }

    /// <summary>
    /// If the primary file was renamed, the new primary file path.
    /// Otherwise equal to <see cref="OriginalFilePath"/>.
    /// </summary>
    public required string PrimaryFilePath { get; init; }

    /// <summary>
    /// The updated source code content for the primary file.
    /// </summary>
    public required string PrimaryFileContent { get; init; }

    /// <summary>
    /// Map of new file paths to their generated source code contents.
    /// </summary>
    public required IReadOnlyDictionary<string, string> NewFiles { get; init; }

    /// <summary>
    /// True if the primary file needs to be renamed on disk.
    /// </summary>
    public bool IsRenamed => !string.Equals(OriginalFilePath, PrimaryFilePath, StringComparison.OrdinalIgnoreCase);
}
