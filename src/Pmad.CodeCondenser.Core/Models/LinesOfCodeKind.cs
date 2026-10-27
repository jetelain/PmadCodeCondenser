namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Specifies the syntactic kind of a lines of code violation.
/// </summary>
public enum LinesOfCodeKind
{
    /// <summary>
    /// A method, constructor, operator, or local function exceeding the method line threshold.
    /// </summary>
    Method,

    /// <summary>
    /// A class or record declaration exceeding the class line threshold.
    /// </summary>
    Class
}
