namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Contains structural information about an analyzed method for clone detection.
/// </summary>
public sealed record MethodCloneInfo(
    string FilePath,
    string MethodName,
    int StartLine,
    int EndLine,
    int StatementCount,
    string NormalizedHash,
    string Signature,
    string ProjectId = "Default");
