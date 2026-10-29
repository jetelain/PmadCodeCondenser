namespace Pmad.CodeCondenser.Core.Clones;

/// <summary>
/// Resolves project identifiers for source files by locating the nearest enclosing .csproj file.
/// </summary>
public interface IProjectLocator
{
    /// <summary>
    /// Locates the project identifier (the enclosing csproj file name without extension) for the specified file.
    /// Falls back to "Default" if no project file is found.
    /// </summary>
    /// <param name="filePath">Path to the source file.</param>
    /// <param name="rootDirectory">Optional root directory boundary.</param>
    /// <returns>The inferred project identifier or "Default".</returns>
    string LocateProjectId(string filePath, string? rootDirectory = null);

    /// <summary>
    /// Locates the absolute path to the enclosing *.csproj file for the specified file, or null if none found.
    /// </summary>
    /// <param name="filePath">Path to the source file.</param>
    /// <param name="rootDirectory">Optional root directory boundary.</param>
    /// <returns>The absolute path to the enclosing project file, or null.</returns>
    string? LocateProjectFilePath(string filePath, string? rootDirectory = null) => null;

}
