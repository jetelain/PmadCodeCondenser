namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Represents a group of methods that share an identical normalized AST hash (code clones).
/// </summary>
public sealed record CodeCloneGroup(
    string NormalizedHash,
    IReadOnlyList<MethodCloneInfo> Methods,
    CloneCategory Category = CloneCategory.Production,
    int ImpactScore = 0,
    bool IsComplementaryPair = false)
{
    /// <summary>
    /// Gets the number of statements in each duplicate method of this cluster.
    /// </summary>
    public int StatementCount => Methods.Count > 0 ? Methods.Max(m => m.StatementCount) : 0;
}
