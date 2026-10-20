using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pmad.CodeCondenser.Core.Clones;

/// <summary>
/// Normalizes method syntax trees by replacing identifiers with 'VAR' and literals with 'CONST'.
/// </summary>
public sealed class AstNormalizer : IAstNormalizer
{
    private static readonly NormalizerRewriter s_rewriter = new();

    /// <inheritdoc />
    public MethodDeclarationSyntax Normalize(MethodDeclarationSyntax method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return (MethodDeclarationSyntax)s_rewriter.Visit(method);
    }

    private sealed class NormalizerRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia) ||
                trivia.IsKind(SyntaxKind.DocumentationCommentExteriorTrivia))
            {
                return default;
            }

            return base.VisitTrivia(trivia);
        }

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;

            // Strip accessibility modifiers (public, private, protected, internal)
            var remainingModifiers = visited.Modifiers
                .Where(m => !m.IsKind(SyntaxKind.PublicKeyword) &&
                            !m.IsKind(SyntaxKind.PrivateKeyword) &&
                            !m.IsKind(SyntaxKind.ProtectedKeyword) &&
                            !m.IsKind(SyntaxKind.InternalKeyword));

            return visited
                .WithIdentifier(SyntaxFactory.Identifier("METHOD"))
                .WithModifiers(SyntaxFactory.TokenList(remainingModifiers))
                .WithAttributeLists(default);
        }

        public override SyntaxNode? VisitParameter(ParameterSyntax node)
        {
            var visited = (ParameterSyntax)base.VisitParameter(node)!;
            return visited
                .WithIdentifier(SyntaxFactory.Identifier("VAR"))
                .WithAttributeLists(default);
        }

        public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            var visited = (VariableDeclaratorSyntax)base.VisitVariableDeclarator(node)!;
            return visited.WithIdentifier(SyntaxFactory.Identifier("VAR"));
        }

        public override SyntaxNode? VisitSingleVariableDesignation(SingleVariableDesignationSyntax node)
        {
            return SyntaxFactory.SingleVariableDesignation(SyntaxFactory.Identifier("VAR"));
        }

        public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
        {
            var visited = (ForEachStatementSyntax)base.VisitForEachStatement(node)!;
            return visited.WithIdentifier(SyntaxFactory.Identifier("VAR"));
        }

        public override SyntaxNode? VisitCatchDeclaration(CatchDeclarationSyntax node)
        {
            var visited = (CatchDeclarationSyntax)base.VisitCatchDeclaration(node)!;
            if (!node.Identifier.IsKind(SyntaxKind.None) && !string.IsNullOrEmpty(node.Identifier.ValueText))
            {
                return visited.WithIdentifier(SyntaxFactory.Identifier("VAR"));
            }

            return visited;
        }

        public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            var visited = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!;
            return visited.WithIdentifier(SyntaxFactory.Identifier("LOCAL_FUNC"));
        }

        public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.NumericLiteralExpression) ||
                node.IsKind(SyntaxKind.StringLiteralExpression) ||
                node.IsKind(SyntaxKind.CharacterLiteralExpression) ||
                node.IsKind(SyntaxKind.TrueLiteralExpression) ||
                node.IsKind(SyntaxKind.FalseLiteralExpression) ||
                node.IsKind(SyntaxKind.Utf8StringLiteralExpression))
            {
                return SyntaxFactory.IdentifierName("CONST");
            }

            return base.VisitLiteralExpression(node);
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            return SyntaxFactory.IdentifierName("VAR");
        }
    }
}
