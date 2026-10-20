using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pmad.CodeCondenser.Core.Clones;

/// <summary>
/// Computes a structural cryptographic hash of a normalized method declaration.
/// </summary>
public interface IMethodHasher
{
    string ComputeHash(MethodDeclarationSyntax normalizedMethod);
}
