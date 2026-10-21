namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Categorization of a code clone cluster based on its member methods.
/// </summary>
public enum CloneCategory
{
    /// <summary>
    /// All methods in the clone cluster belong to production code.
    /// </summary>
    Production,

    /// <summary>
    /// All methods in the clone cluster belong to test code.
    /// </summary>
    Test,

    /// <summary>
    /// The clone cluster contains methods spanning both production and test files.
    /// </summary>
    CrossBoundary
}
