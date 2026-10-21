namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Represents a single occurrence of a sub-method code clone within a method block.
/// </summary>
/// <param name="FilePath">Relative path to the source file.</param>
/// <param name="MethodName">Name of the containing method.</param>
/// <param name="StartLine">1-based starting line number of the matching statement sequence.</param>
/// <param name="EndLine">1-based ending line number of the matching statement sequence.</param>
/// <param name="StatementCount">Number of consecutive statements in the matching sequence.</param>
/// <param name="StatementIndex">0-based starting statement index within the parent block scope.</param>
/// <param name="BlockScopeId">Identifier of the containing block scope.</param>
/// <param name="ProjectId">Project identifier (defaults to "Default").</param>
public sealed record SubMethodCloneOccurrence(
    string FilePath,
    string MethodName,
    int StartLine,
    int EndLine,
    int StatementCount,
    int StatementIndex = 0,
    string? BlockScopeId = null,
    string ProjectId = "Default");
