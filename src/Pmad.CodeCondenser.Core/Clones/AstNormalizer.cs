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
                node.IsKind(SyntaxKind.Utf8StringLiteralExpression) ||
                node.IsKind(SyntaxKind.DefaultLiteralExpression))
            {
                return SyntaxFactory.IdentifierName("CONST");
            }

            return base.VisitLiteralExpression(node);
        }

        public override SyntaxNode? VisitDefaultExpression(DefaultExpressionSyntax node)
        {
            // Normalize default(T) to CONST
            return SyntaxFactory.IdentifierName("CONST");
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            // Check if this matches an enum, flag, or static constant pattern in value positions: [Namespace.]TypeName.MemberName
            if (IsEnumOrConstantPattern(node))
            {
                return SyntaxFactory.IdentifierName("CONST");
            }

            return base.VisitMemberAccessExpression(node);
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            // Handle nameof(...) folding in value positions
            if (node.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" })
            {
                return SyntaxFactory.IdentifierName("CONST");
            }

            // Preserve the method name of the invocation target while normalizing receiver and arguments.
            // Intercepting member access here prevents the target method name from being visited by VisitIdentifierName.
            if (node.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                // `this.Method(...)` is semantically identical to a direct call `Method(...)`;
                // normalize it to the same METHOD_CALL: form so `this`-prefixed and un-prefixed
                // variants of the same clone still match.
                if (memberAccess.Expression is ThisExpressionSyntax)
                {
                    var thisName = memberAccess.Name;
                    var thisText = thisName is GenericNameSyntax tg ? tg.Identifier.ValueText : thisName.Identifier.ValueText;
                    var thisIdentifier = SyntaxFactory.Identifier("METHOD_CALL:" + thisText);
                    var thisTarget = thisName is GenericNameSyntax gn
                        ? (ExpressionSyntax)gn
                            .WithIdentifier(thisIdentifier)
                            .WithTypeArgumentList(SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SeparatedList(gn.TypeArgumentList.Arguments
                                    .Select(_ => (TypeSyntax)SyntaxFactory.IdentifierName("VAR")))))
                        : (ExpressionSyntax)SyntaxFactory.IdentifierName(thisIdentifier);
                    var thisArgs = (ArgumentListSyntax)Visit(node.ArgumentList)!;
                    return node.WithExpression(thisTarget).WithArgumentList(thisArgs);
                }

                // `base.Method(...)` preserves the target name under a fixed BASE receiver.
                var normalizedExpression = memberAccess.Expression is BaseExpressionSyntax
                    ? (ExpressionSyntax)SyntaxFactory.IdentifierName("BASE")
                    : (ExpressionSyntax)Visit(memberAccess.Expression)!;

                SimpleNameSyntax normalizedName;
                if (memberAccess.Name is GenericNameSyntax genericName)
                {
                    var normalizedTypeArgs = genericName.TypeArgumentList.Arguments
                        .Select(_ => (TypeSyntax)SyntaxFactory.IdentifierName("VAR"));
                    normalizedName = genericName.WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(normalizedTypeArgs)));
                }
                else
                {
                    normalizedName = SyntaxFactory.IdentifierName(memberAccess.Name.Identifier.ValueText);
                }

                var newTarget = SyntaxFactory.MemberAccessExpression(
                    memberAccess.Kind(),
                    normalizedExpression,
                    normalizedName);

                var normalizedArgs = (ArgumentListSyntax)Visit(node.ArgumentList)!;
                return node.WithExpression(newTarget).WithArgumentList(normalizedArgs);
            }

            // Handle null-conditional member binding invocations (e.g. receiver?.Method(...))
            if (node.Expression is MemberBindingExpressionSyntax memberBinding)
            {
                SimpleNameSyntax normalizedName;
                if (memberBinding.Name is GenericNameSyntax genericName)
                {
                    var normalizedTypeArgs = genericName.TypeArgumentList.Arguments
                        .Select(_ => (TypeSyntax)SyntaxFactory.IdentifierName("VAR"));
                    normalizedName = genericName.WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(normalizedTypeArgs)));
                }
                else
                {
                    normalizedName = SyntaxFactory.IdentifierName(memberBinding.Name.Identifier.ValueText);
                }

                var newTarget = memberBinding.WithName(normalizedName);
                var normalizedArgs = (ArgumentListSyntax)Visit(node.ArgumentList)!;
                return node.WithExpression(newTarget).WithArgumentList(normalizedArgs);
            }

            // Handle direct local method calls (e.g. ProcessOrder(x)) preserving helper name to avoid Cluster #13 collisions
            if (node.Expression is IdentifierNameSyntax simpleName)
            {
                var newTarget = SyntaxFactory.IdentifierName($"METHOD_CALL:{simpleName.Identifier.ValueText}");
                var normalizedArgs = (ArgumentListSyntax)Visit(node.ArgumentList)!;
                return node.WithExpression(newTarget).WithArgumentList(normalizedArgs);
            }

            // Handle direct generic helper calls (e.g. Validate<Order>(x))
            if (node.Expression is GenericNameSyntax genericSimple)
            {
                var normalizedTypeArgs = genericSimple.TypeArgumentList.Arguments
                    .Select(_ => (TypeSyntax)SyntaxFactory.IdentifierName("VAR"));
                var newTarget = genericSimple
                    .WithIdentifier(SyntaxFactory.Identifier($"METHOD_CALL:{genericSimple.Identifier.ValueText}"))
                    .WithTypeArgumentList(SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(normalizedTypeArgs)));
                var normalizedArgs = (ArgumentListSyntax)Visit(node.ArgumentList)!;
                return node.WithExpression(newTarget).WithArgumentList(normalizedArgs);
            }

            return base.VisitInvocationExpression(node);
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            return SyntaxFactory.IdentifierName("VAR");
        }

        private static readonly HashSet<string> WellKnownStaticTypes = new(StringComparer.Ordinal)
        {
            "DateTime", "DateTimeOffset", "TimeSpan", "Guid", "Task", "CancellationToken", "String", "Math", "Encoding"
        };

        private static bool IsEnumOrConstantPattern(MemberAccessExpressionSyntax node)
        {
            // Allowed only in value positions
            var current = (ExpressionSyntax)node;
            var parent = node.Parent;
            while (parent is ParenthesizedExpressionSyntax p)
            {
                current = p;
                parent = parent.Parent;
            }

            // Left-hand side of assignment is an l-value target, never an enum or constant
            if (parent is AssignmentExpressionSyntax assignment && assignment.Left == current)
            {
                return false;
            }

            if (parent is not (ArgumentSyntax 
                or BinaryExpressionSyntax 
                or AssignmentExpressionSyntax 
                or EqualsValueClauseSyntax 
                or ReturnStatementSyntax 
                or ArrowExpressionClauseSyntax 
                or ConditionalExpressionSyntax 
                or InterpolationSyntax 
                or InitializerExpressionSyntax
                or ConstantPatternSyntax
                or CaseSwitchLabelSyntax
                or CasePatternSwitchLabelSyntax
                or CastExpressionSyntax
                or SwitchExpressionArmSyntax))
            {
                return false;
            }

            // Never fold 'this' or 'base' member accesses
            if (node.Expression is ThisExpressionSyntax or BaseExpressionSyntax)
            {
                return false;
            }

            // Rightmost member must be PascalCase
            if (node.Name is not SimpleNameSyntax memberId ||
                string.IsNullOrEmpty(memberId.Identifier.ValueText) ||
                !char.IsUpper(memberId.Identifier.ValueText[0]))
            {
                return false;
            }

            // Left expression can be an identifier (TypeName.Member) or qualified type access (Namespace.TypeName.Member)
            // or predefined type (string.Empty, int.MaxValue)
            string? typeName = node.Expression switch
            {
                IdentifierNameSyntax id => id.Identifier.ValueText,
                PredefinedTypeSyntax pt => pt.Keyword.ValueText switch
                {
                    "string" => "String",
                    "int" => "Int32",
                    "long" => "Int64",
                    "double" => "Double",
                    "float" => "Single",
                    "byte" => "Byte",
                    "bool" => "Boolean",
                    _ => null
                },
                MemberAccessExpressionSyntax nested when nested.Expression is not (ThisExpressionSyntax or BaseExpressionSyntax) => nested.Name.Identifier.ValueText,
                _ => null
            };

            if (string.IsNullOrEmpty(typeName) || !char.IsUpper(typeName[0]))
            {
                return false;
            }

            // Accept if well-known static framework type (DateTime.UtcNow, String.Empty)
            // or represents an enum member access (TypeName.Member, or Namespace.TypeName.Member for
            // qualified paths such as System.Threading.Tasks.TaskPriority.Critical),
            // or static constant member on a predefined type (string.Empty, int.MaxValue).
            bool isKnownStatic = WellKnownStaticTypes.Contains(typeName);
            bool isEnumPattern = char.IsUpper(typeName[0]) &&
                (node.Expression is IdentifierNameSyntax || AllComponentsUppercase(node.Expression));
            bool isPredefinedConstant = node.Expression is PredefinedTypeSyntax;

            return isKnownStatic || isEnumPattern || isPredefinedConstant;
        }

        // Guards qualified (namespace-qualified) enum access: every component of the receiver chain
        // must be an uppercase-starting identifier, so instance property chains such as
        // order.Customer.Priority or request.Path are never folded to CONST.
        private static bool AllComponentsUppercase(ExpressionSyntax expression)
        {
            while (expression is MemberAccessExpressionSyntax { Name: SimpleNameSyntax name } member)
            {
                var component = name.Identifier.ValueText;
                if (component.Length == 0 || !char.IsUpper(component[0]))
                {
                    return false;
                }
                expression = member.Expression;
            }

            if (expression is not IdentifierNameSyntax final)
            {
                return false;
            }

            var root = final.Identifier.ValueText;
            return root.Length > 0 && char.IsUpper(root[0]);
        }
    }
}
