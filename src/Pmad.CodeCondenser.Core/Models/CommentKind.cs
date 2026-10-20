namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Identifies the kind of C# comment trivia.
/// </summary>
public enum CommentKind
{
    /// <summary>
    /// Single-line comment starting with //
    /// </summary>
    SingleLine,

    /// <summary>
    /// Delimited multi-line comment /* ... */
    /// </summary>
    MultiLine,

    /// <summary>
    /// XML documentation comment /// &lt;summary&gt;...
    /// </summary>
    XmlDocumentation
}
