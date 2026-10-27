namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Specifies the file reorganization action recommended for a source file.
/// </summary>
public enum TypeFileAction
{
    /// <summary>
    /// File contains a single type matching the file name, or auxiliary types within tolerance. No action required.
    /// </summary>
    None = 0,

    /// <summary>
    /// File contains multiple types that should be extracted into separate files.
    /// </summary>
    Split = 1,

    /// <summary>
    /// File contains a single type whose name does not match the file name.
    /// </summary>
    Rename = 2,

    /// <summary>
    /// File contains multiple types and its primary type does not match the file name.
    /// </summary>
    SplitAndRename = 3
}
