namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Analysis result of type declarations within a single source file,
/// indicating whether the file should be split, renamed, or left unchanged.
/// </summary>
public sealed class TypeFileAnalysis
{
    /// <summary>
    /// Relative or absolute path to the analyzed source file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// File category (Production or Test).
    /// </summary>
    public FileCategory Category { get; init; } = FileCategory.Production;

    /// <summary>
    /// The recommended action (None, Split, Rename, SplitAndRename).
    /// </summary>
    public TypeFileAction Action { get; init; } = TypeFileAction.None;

    /// <summary>
    /// If renaming is recommended, the new file name (e.g. "OrderService.cs").
    /// </summary>
    public string? SuggestedFileName { get; init; }

    /// <summary>
    /// All top-level types detected in the file.
    /// </summary>
    public IReadOnlyList<ExtractedTypeInfo> Types { get; init; } = [];

    /// <summary>
    /// Indicates whether this file has a violation (requires split and/or rename).
    /// </summary>
    public bool HasViolation => Action != TypeFileAction.None;

    /// <summary>
    /// Gets the list of types that should be extracted into new separate files.
    /// </summary>
    public IReadOnlyList<ExtractedTypeInfo> TypesToExtract =>
        Types.Where(t => !t.IsPrimary && !t.IsTolerated && !t.IsSuppressed).ToList();

    /// <summary>
    /// Gets the primary type of the file, if any.
    /// </summary>
    public ExtractedTypeInfo? PrimaryType => Types.FirstOrDefault(t => t.IsPrimary);
}
