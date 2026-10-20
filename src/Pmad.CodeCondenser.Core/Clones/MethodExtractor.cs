using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pmad.CodeCondenser.Core.Clones;

/// <summary>
/// Syntax walker that extracts method declarations meeting or exceeding a minimum statement count threshold.
/// </summary>
public sealed class MethodExtractor : CSharpSyntaxWalker, IMethodExtractor
{
    private readonly int _minStatements;
    private readonly List<MethodDeclarationSyntax> _methods = new();
    private readonly List<(MethodDeclarationSyntax Method, int StatementCount)> _methodsWithCount = new();

    /// <summary>
    /// Initializes a new instance of <see cref="MethodExtractor"/> with the specified statement threshold.
    /// </summary>
    /// <param name="minStatements">The minimum statement count required for a method to be collected (default: 8).</param>
    public MethodExtractor(int minStatements = 8)
    {
        _minStatements = minStatements;
    }

    /// <summary>
    /// Gets the list of extracted method declarations.
    /// </summary>
    public IReadOnlyList<MethodDeclarationSyntax> Methods => _methods;

    /// <summary>
    /// Gets the list of extracted method declarations along with their statement count.
    /// </summary>
    public IReadOnlyList<(MethodDeclarationSyntax Method, int StatementCount)> MethodsWithCount => _methodsWithCount;

    /// <summary>
    /// Calculates the number of non-block executable statements in the given method.
    /// </summary>
    /// <param name="method">The method declaration syntax.</param>
    /// <returns>The total number of non-block statements in the method body, 1 for expression bodies, or 0 if empty.</returns>
    public static int GetStatementCount(MethodDeclarationSyntax method)
    {
        ArgumentNullException.ThrowIfNull(method);

        if (method.Body != null)
        {
            int count = 0;
            foreach (var node in method.Body.DescendantNodes())
            {
                if (node is StatementSyntax and not BlockSyntax and not EmptyStatementSyntax)
                {
                    count++;
                }
            }
            return count;
        }

        if (method.ExpressionBody != null)
        {
            return 1;
        }

        return 0;
    }

    /// <inheritdoc />
    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (!DuplicationIgnoreMarker.IsIgnored(node))
        {
            var count = GetStatementCount(node);
            if (count >= _minStatements)
            {
                _methods.Add(node);
                _methodsWithCount.Add((node, count));
            }
        }

        base.VisitMethodDeclaration(node);
    }

    /// <inheritdoc />
    public IReadOnlyList<MethodDeclarationSyntax> Extract(SyntaxTree syntaxTree, int minStatements = 8)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);

        var root = syntaxTree.GetRoot();
        return Extract(root, minStatements);
    }

    /// <inheritdoc />
    public IReadOnlyList<MethodDeclarationSyntax> Extract(SyntaxNode node, int minStatements = 8)
    {
        ArgumentNullException.ThrowIfNull(node);

        var extractor = new MethodExtractor(minStatements);
        extractor.Visit(node);
        return extractor.Methods;
    }

    /// <inheritdoc />
    public IReadOnlyList<(MethodDeclarationSyntax Method, int StatementCount)> ExtractWithStatementCounts(SyntaxTree syntaxTree, int minStatements = 8)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);

        var root = syntaxTree.GetRoot();
        return ExtractWithStatementCounts(root, minStatements);
    }

    /// <inheritdoc />
    public IReadOnlyList<(MethodDeclarationSyntax Method, int StatementCount)> ExtractWithStatementCounts(SyntaxNode node, int minStatements = 8)
    {
        ArgumentNullException.ThrowIfNull(node);

        var extractor = new MethodExtractor(minStatements);
        extractor.Visit(node);
        return extractor.MethodsWithCount;
    }
}
