using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pmad.CodeCondenser.Core.Clones;

/// <summary>
/// Normalizes method syntax trees by replacing identifiers with 'VAR' and literals with 'CONST'.
/// </summary>
public interface IAstNormalizer
{
    MethodDeclarationSyntax Normalize(MethodDeclarationSyntax method);
}
