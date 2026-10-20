using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pmad.CodeCondenser.Core.Clones;

/// <summary>
/// Extracts method declarations from C# syntax trees with statement count filtering.
/// </summary>
public interface IMethodExtractor
{
    /// <summary>
    /// Extracts all method declarations from the given syntax tree that meet or exceed the minimum statement threshold.
    /// </summary>
    IReadOnlyList<MethodDeclarationSyntax> Extract(SyntaxTree syntaxTree, int minStatements = 8);

    /// <summary>
    /// Extracts all method declarations from the given syntax node that meet or exceed the minimum statement threshold.
    /// </summary>
    IReadOnlyList<MethodDeclarationSyntax> Extract(SyntaxNode node, int minStatements = 8);

    /// <summary>
    /// Extracts all method declarations along with their statement count from the given syntax tree that meet or exceed the minimum statement threshold.
    /// </summary>
    IReadOnlyList<(MethodDeclarationSyntax Method, int StatementCount)> ExtractWithStatementCounts(SyntaxTree syntaxTree, int minStatements = 8) =>
        Extract(syntaxTree, minStatements).Select(m => (m, MethodExtractor.GetStatementCount(m))).ToList();

    /// <summary>
    /// Extracts all method declarations along with their statement count from the given syntax node that meet or exceed the minimum statement threshold.
    /// </summary>
    IReadOnlyList<(MethodDeclarationSyntax Method, int StatementCount)> ExtractWithStatementCounts(SyntaxNode node, int minStatements = 8) =>
        Extract(node, minStatements).Select(m => (m, MethodExtractor.GetStatementCount(m))).ToList();
}
