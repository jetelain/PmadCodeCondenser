using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Filtering;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Predicates;

/// <summary>
/// Walks C# syntax trees to extract candidate condition expressions and existing boolean helper declarations.
/// </summary>
public sealed class PredicateExtractor : IPredicateExtractor
{
    private static readonly HashSet<string> LinqPredicateMethods = new(StringComparer.Ordinal)
    {
        "Where", "Any", "All", "First", "FirstOrDefault", "Single", "SingleOrDefault",
        "Count", "Find", "FindAll", "Exists", "RemoveAll", "TakeWhile", "SkipWhile"
    };

    /// <summary>
    /// Extracts all candidate condition occurrences and helper declarations from a syntax tree.
    /// </summary>
    public IReadOnlyList<PredicateOccurrence> ExtractPredicates(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings,
        string? projectId = null,
        FileCategory fileCategory = FileCategory.Production)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        ArgumentNullException.ThrowIfNull(settings);

        var root = syntaxTree.GetRoot();
        if (settings.ExcludeGeneratedCode && GeneratedCodeDetector.HasGeneratedCodeAttribute(root))
        {
            return [];
        }

        var walker = new PredicateWalker(syntaxTree, filePath, settings, projectId, fileCategory);
        walker.Visit(root);
        return walker.Occurrences;
    }

    private sealed class PredicateWalker : CSharpSyntaxWalker
    {
        private readonly SyntaxTree _syntaxTree;
        private readonly string _filePath;
        private readonly CodeCondenserSettings _settings;
        private readonly string? _projectId;
        private readonly FileCategory _fileCategory;
        private readonly List<PredicateOccurrence> _occurrences = new();

        public IReadOnlyList<PredicateOccurrence> Occurrences => _occurrences;

        public PredicateWalker(
            SyntaxTree syntaxTree,
            string filePath,
            CodeCondenserSettings settings,
            string? projectId,
            FileCategory fileCategory)
        {
            _syntaxTree = syntaxTree;
            _filePath = filePath;
            _settings = settings;
            _projectId = projectId;
            _fileCategory = fileCategory;
        }

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            ProcessCandidateCondition(node.Condition, node);
            base.VisitIfStatement(node);
        }

        public override void VisitWhileStatement(WhileStatementSyntax node)
        {
            ProcessCandidateCondition(node.Condition, node);
            base.VisitWhileStatement(node);
        }

        public override void VisitDoStatement(DoStatementSyntax node)
        {
            ProcessCandidateCondition(node.Condition, node);
            base.VisitDoStatement(node);
        }

        public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            ProcessCandidateCondition(node.Condition, node);
            base.VisitConditionalExpression(node);
        }

        public override void VisitWhereClause(WhereClauseSyntax node)
        {
            ProcessCandidateCondition(node.Condition, node);
            base.VisitWhereClause(node);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            // Check for LINQ or collection predicate methods like .Where(x => ...)
            if (node.Expression is MemberAccessExpressionSyntax memberAccess &&
                LinqPredicateMethods.Contains(memberAccess.Name.Identifier.ValueText) &&
                node.ArgumentList.Arguments.Count > 0)
            {
                var firstArg = node.ArgumentList.Arguments[0].Expression;
                if (firstArg is SimpleLambdaExpressionSyntax simpleLambda && simpleLambda.Body is ExpressionSyntax lambdaExpr)
                {
                    var receiverParam = simpleLambda.Parameter.Identifier.ValueText;
                    ProcessCandidateCondition(lambdaExpr, node, explicitReceiver: receiverParam);
                }
                else if (firstArg is ParenthesizedLambdaExpressionSyntax parenLambda && parenLambda.Body is ExpressionSyntax parenExpr)
                {
                    var receiverParam = parenLambda.ParameterList.Parameters.FirstOrDefault()?.Identifier.ValueText;
                    ProcessCandidateCondition(parenExpr, node, explicitReceiver: receiverParam);
                }
            }

            base.VisitInvocationExpression(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (PredicateIgnoreMarker.IsIgnored(node))
            {
                base.VisitMethodDeclaration(node);
                return;
            }

            var returnType = node.ReturnType.ToString();
            bool isBoolReturn = string.Equals(returnType, "bool", StringComparison.Ordinal) ||
                               string.Equals(returnType, "Boolean", StringComparison.Ordinal);

            if (isBoolReturn)
            {
                ExpressionSyntax? bodyExpr = null;

                if (node.ExpressionBody != null)
                {
                    bodyExpr = node.ExpressionBody.Expression;
                }
                else if (node.Body != null && node.Body.Statements.Count == 1 &&
                         node.Body.Statements[0] is ReturnStatementSyntax ret && ret.Expression != null)
                {
                    bodyExpr = ret.Expression;
                }

                if (bodyExpr != null)
                {
                    ProcessHelperDeclaration(node, bodyExpr);
                    // Do not process bodyExpr as a call site
                    return;
                }
            }

            base.VisitMethodDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            if (PredicateIgnoreMarker.IsIgnored(node))
            {
                base.VisitPropertyDeclaration(node);
                return;
            }

            var propType = node.Type.ToString();
            bool isBoolProp = string.Equals(propType, "bool", StringComparison.Ordinal) ||
                              string.Equals(propType, "Boolean", StringComparison.Ordinal);

            if (isBoolProp)
            {
                ExpressionSyntax? bodyExpr = null;

                if (node.ExpressionBody != null)
                {
                    bodyExpr = node.ExpressionBody.Expression;
                }
                else if (node.AccessorList != null)
                {
                    var getAccessor = node.AccessorList.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
                    if (getAccessor != null)
                    {
                        if (getAccessor.ExpressionBody != null)
                        {
                            bodyExpr = getAccessor.ExpressionBody.Expression;
                        }
                        else if (getAccessor.Body != null && getAccessor.Body.Statements.Count == 1 &&
                                 getAccessor.Body.Statements[0] is ReturnStatementSyntax ret && ret.Expression != null)
                        {
                            bodyExpr = ret.Expression;
                        }
                    }
                }

                if (bodyExpr != null)
                {
                    ProcessHelperProperty(node, bodyExpr);
                    return;
                }
            }

            base.VisitPropertyDeclaration(node);
        }

        public override void VisitReturnStatement(ReturnStatementSyntax node)
        {
            if (node.Expression != null && IsCompoundBooleanExpression(node.Expression))
            {
                ProcessCandidateCondition(node.Expression, node);
            }

            base.VisitReturnStatement(node);
        }

        private void ProcessCandidateCondition(
            ExpressionSyntax condition,
            SyntaxNode contextNode,
            string? explicitReceiver = null)
        {
            if (PredicateIgnoreMarker.IsIgnored(contextNode) || PredicateIgnoreMarker.IsIgnored(condition))
            {
                return;
            }

            var text = condition.ToString();
            var minThreshold = _settings.PredicateLengthThreshold;
            if (text.Length < minThreshold)
            {
                return;
            }

            if (IsTrivialExpression(condition))
            {
                return;
            }

            var lineSpan = _syntaxTree.GetLineSpan(condition.Span);
            int startLine = lineSpan.StartLinePosition.Line + 1;
            int endLine = lineSpan.EndLinePosition.Line + 1;
            int startCol = lineSpan.StartLinePosition.Character + 1;
            int endCol = lineSpan.EndLinePosition.Character + 1;

            var enclosingMember = FindEnclosingMember(condition);
            var enclosingType = condition.FirstAncestorOrSelf<TypeDeclarationSyntax>();
            var enclosingTypeName = enclosingType?.Identifier.ValueText;
            var enclosingNamespace = FindEnclosingNamespace(condition);

            var receiver = explicitReceiver ?? PredicateNormalizer.DetectCandidateReceiver(condition);

            // Compute standard positive and negated normalizations
            var normResult = PredicateNormalizer.Normalize(condition);
            var negResult = PredicateNormalizer.NormalizeNegated(condition);

            // Compute instance-bound positive and negated normalizations if a receiver exists
            PredicateNormalizer.NormalizationResult? instResult = null;
            PredicateNormalizer.NormalizationResult? instNegResult = null;
            if (receiver != null)
            {
                instResult = PredicateNormalizer.Normalize(condition, explicitReceiver: receiver);
                instNegResult = PredicateNormalizer.NormalizeNegated(condition, explicitReceiver: receiver);
            }

            _occurrences.Add(new PredicateOccurrence
            {
                FilePath = _filePath,
                StartLine = startLine,
                EndLine = endLine,
                StartColumn = startCol,
                EndColumn = endCol,
                ExpressionText = text,
                NormalizedSignature = normResult.Signature,
                NormalizedHash = normResult.Hash,
                NegatedNormalizedSignature = negResult.Signature,
                NegatedNormalizedHash = negResult.Hash,
                InstanceNormalizedSignature = instResult?.Signature,
                InstanceNormalizedHash = instResult?.Hash ?? 0,
                NegatedInstanceNormalizedSignature = instNegResult?.Signature,
                NegatedInstanceNormalizedHash = instNegResult?.Hash ?? 0,
                EnclosingMember = enclosingMember,
                EnclosingTypeName = enclosingTypeName,
                Namespace = enclosingNamespace,
                ProjectId = _projectId,
                Category = _fileCategory,
                IsHelperDeclaration = false,
                ReceiverExpression = receiver,
                StaticArgumentExpressions = normResult.ArgumentExpressions,
                InstanceArgumentExpressions = instResult?.ArgumentExpressions ?? [],
                ArgumentExpressions = instResult != null ? instResult.ArgumentExpressions : normResult.ArgumentExpressions,
                IsNegatedAtCallSite = normResult.IsNegated
            });
        }

        private void ProcessHelperDeclaration(MethodDeclarationSyntax method, ExpressionSyntax bodyExpr)
        {
            var text = bodyExpr.ToString();
            var minThreshold = _settings.ExistingHelperPredicateLengthThreshold;
            if (text.Length < minThreshold || IsTrivialExpression(bodyExpr))
            {
                return;
            }

            var lineSpan = _syntaxTree.GetLineSpan(bodyExpr.Span);
            int startLine = lineSpan.StartLinePosition.Line + 1;
            int endLine = lineSpan.EndLinePosition.Line + 1;
            int startCol = lineSpan.StartLinePosition.Character + 1;
            int endCol = lineSpan.EndLinePosition.Character + 1;

            var enclosingType = method.FirstAncestorOrSelf<TypeDeclarationSyntax>();
            var declaringTypeName = enclosingType?.Identifier.ValueText ?? "Helper";
            var isStatic = method.Modifiers.Any(SyntaxKind.StaticKeyword);
            var isPrivate = method.Modifiers.Any(SyntaxKind.PrivateKeyword);

            var parameters = method.ParameterList.Parameters.Select(p => p.Identifier.ValueText).ToList();
            var parameterTypes = method.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "var").ToList();

            var normResult = PredicateNormalizer.Normalize(
                bodyExpr,
                knownParameters: parameters,
                isInstanceMemberContext: !isStatic);

            var helperInfo = new PredicateHelperInfo
            {
                HelperName = method.Identifier.ValueText,
                DeclaringTypeName = declaringTypeName,
                IsStatic = isStatic,
                IsProperty = false,
                IsPrivate = isPrivate,
                Parameters = parameters,
                ParameterTypes = parameterTypes,
                Accessibility = GetAccessibility(method.Modifiers),
                ReturnType = method.ReturnType.ToString(),
                Namespace = FindEnclosingNamespace(method),
                IsBodyNegated = normResult.IsNegated
            };

            _occurrences.Add(new PredicateOccurrence
            {
                FilePath = _filePath,
                StartLine = startLine,
                EndLine = endLine,
                StartColumn = startCol,
                EndColumn = endCol,
                ExpressionText = text,
                NormalizedSignature = normResult.Signature,
                NormalizedHash = normResult.Hash,
                InstanceNormalizedSignature = !isStatic ? normResult.Signature : null,
                InstanceNormalizedHash = !isStatic ? normResult.Hash : 0,
                EnclosingMember = method.Identifier.ValueText,
                EnclosingTypeName = declaringTypeName,
                Namespace = helperInfo.Namespace,
                ProjectId = _projectId,
                Category = _fileCategory,
                IsHelperDeclaration = true,
                HelperInfo = helperInfo
            });
        }

        private void ProcessHelperProperty(PropertyDeclarationSyntax property, ExpressionSyntax bodyExpr)
        {
            var text = bodyExpr.ToString();
            var minThreshold = _settings.ExistingHelperPredicateLengthThreshold;
            if (text.Length < minThreshold || IsTrivialExpression(bodyExpr))
            {
                return;
            }

            var lineSpan = _syntaxTree.GetLineSpan(bodyExpr.Span);
            int startLine = lineSpan.StartLinePosition.Line + 1;
            int endLine = lineSpan.EndLinePosition.Line + 1;
            int startCol = lineSpan.StartLinePosition.Character + 1;
            int endCol = lineSpan.EndLinePosition.Character + 1;

            var enclosingType = property.FirstAncestorOrSelf<TypeDeclarationSyntax>();
            var declaringTypeName = enclosingType?.Identifier.ValueText ?? "Helper";
            var isStatic = property.Modifiers.Any(SyntaxKind.StaticKeyword);
            var isPrivate = property.Modifiers.Any(SyntaxKind.PrivateKeyword);

            var normResult = PredicateNormalizer.Normalize(
                bodyExpr,
                knownParameters: [],
                isInstanceMemberContext: !isStatic);

            var helperInfo = new PredicateHelperInfo
            {
                HelperName = property.Identifier.ValueText,
                DeclaringTypeName = declaringTypeName,
                IsStatic = isStatic,
                IsProperty = true,
                IsPrivate = isPrivate,
                Parameters = [],
                ParameterTypes = [],
                Accessibility = GetAccessibility(property.Modifiers),
                ReturnType = property.Type.ToString(),
                Namespace = FindEnclosingNamespace(property),
                IsBodyNegated = normResult.IsNegated
            };

            _occurrences.Add(new PredicateOccurrence
            {
                FilePath = _filePath,
                StartLine = startLine,
                EndLine = endLine,
                StartColumn = startCol,
                EndColumn = endCol,
                ExpressionText = text,
                NormalizedSignature = normResult.Signature,
                NormalizedHash = normResult.Hash,
                InstanceNormalizedSignature = !isStatic ? normResult.Signature : null,
                InstanceNormalizedHash = !isStatic ? normResult.Hash : 0,
                EnclosingMember = property.Identifier.ValueText,
                EnclosingTypeName = declaringTypeName,
                Namespace = helperInfo.Namespace,
                ProjectId = _projectId,
                Category = _fileCategory,
                IsHelperDeclaration = true,
                HelperInfo = helperInfo
            });
        }

        private static bool IsCompoundBooleanExpression(ExpressionSyntax expr)
        {
            var unwrapped = PredicateNormalizer.UnwrapParentheses(expr);
            if (unwrapped is BinaryExpressionSyntax binary)
            {
                return binary.IsKind(SyntaxKind.LogicalAndExpression) ||
                       binary.IsKind(SyntaxKind.LogicalOrExpression);
            }
            return false;
        }

        private static bool IsTrivialExpression(ExpressionSyntax expr)
        {
            var unwrapped = PredicateNormalizer.UnwrapParentheses(expr);

            // Single identifier: if (isValid)
            if (unwrapped is IdentifierNameSyntax)
            {
                return true;
            }

            // Single negation of identifier: if (!isValid)
            if (unwrapped is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } notExpr &&
                PredicateNormalizer.UnwrapParentheses(notExpr.Operand) is IdentifierNameSyntax)
            {
                return true;
            }

            // Single comparison against null: if (x == null), if (x != null)
            if (unwrapped is BinaryExpressionSyntax binary)
            {
                if (binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression))
                {
                    if (binary.Left.IsKind(SyntaxKind.NullLiteralExpression) ||
                        binary.Right.IsKind(SyntaxKind.NullLiteralExpression))
                    {
                        return true;
                    }
                }
            }

            // Single pattern matching null check: if (x is null), if (x is not null)
            if (unwrapped is IsPatternExpressionSyntax isPattern)
            {
                if (isPattern.Pattern is ConstantPatternSyntax { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } } ||
                    isPattern.Pattern is UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern, Pattern: ConstantPatternSyntax { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } } })
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetAccessibility(SyntaxTokenList modifiers)
        {
            if (modifiers.Any(SyntaxKind.PublicKeyword)) return "public";
            if (modifiers.Any(SyntaxKind.InternalKeyword)) return "internal";
            if (modifiers.Any(SyntaxKind.ProtectedKeyword)) return "protected";
            if (modifiers.Any(SyntaxKind.PrivateKeyword)) return "private";
            return "private";
        }

        private static string? FindEnclosingMember(SyntaxNode node)
        {
            var member = node.FirstAncestorOrSelf<MemberDeclarationSyntax>();
            return member switch
            {
                MethodDeclarationSyntax m => m.Identifier.ValueText,
                PropertyDeclarationSyntax p => p.Identifier.ValueText,
                ConstructorDeclarationSyntax c => c.Identifier.ValueText,
                _ => null
            };
        }

        private static string? FindEnclosingNamespace(SyntaxNode node)
        {
            var fileScoped = node.FirstAncestorOrSelf<FileScopedNamespaceDeclarationSyntax>();
            if (fileScoped != null) return fileScoped.Name.ToString();

            var blockScoped = node.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
            return blockScoped?.Name.ToString();
        }
    }
}
