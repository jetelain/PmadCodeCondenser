namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Represents a group of methods that share an identical normalized AST hash (code clones).
/// </summary>
public sealed record CodeCloneGroup(
    string NormalizedHash,
    IReadOnlyList<MethodCloneInfo> Methods);
