using System.IO.Hashing;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Pmad.CodeCondenser.Core.Predicates;

/// <summary>
/// Normalizes boolean condition expressions into canonical structural signatures,
/// abstracting free variables into parameter slots, binding instance receivers,
/// and computing De Morgan dual forms.
/// </summary>
public sealed class PredicateNormalizer
{
    private static readonly HashSet<string> RuntimeStaticTypes = new(StringComparer.Ordinal)
    {
        "DateTime", "DateTimeOffset", "TimeSpan", "Environment", "Guid", "Random",
        "Math", "Convert", "Regex", "String"
    };

    /// <summary>
    /// Normalization result containing canonical signatures and variable bindings.
    /// </summary>
    public sealed record NormalizationResult(
        string Signature,
        ulong Hash,
        string? ReceiverExpression,
        IReadOnlyList<string> ArgumentExpressions,
        bool IsNegated);

    /// <summary>
    /// Normalizes a condition expression into its canonical structural signature.
    /// </summary>
    public static NormalizationResult Normalize(
        ExpressionSyntax expression,
        string? explicitReceiver = null,
        IReadOnlyList<string>? knownParameters = null,
        bool isInstanceMemberContext = false)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var unwrapped = UnwrapParentheses(expression);
        bool isDirectNegation = false;

        // Check if top-level expression is !inner
        if (unwrapped is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } notExpr)
        {
            unwrapped = UnwrapParentheses(notExpr.Operand);
            isDirectNegation = true;
        }

        var rewriter = new NormalizingRewriter(explicitReceiver, knownParameters, isInstanceMemberContext);
        var normalizedNode = rewriter.Visit(unwrapped);
        var signature = normalizedNode.NormalizeWhitespace("", "").ToFullString();
        var hash = ComputeHash(signature);

        return new NormalizationResult(
            Signature: signature,
            Hash: hash,
            ReceiverExpression: rewriter.DetectedReceiver,
            ArgumentExpressions: rewriter.OrderedArguments,
            IsNegated: isDirectNegation);
    }

    /// <summary>
    /// Inverts the condition using De Morgan's laws and normalizes the resulting expression.
    /// </summary>
    public static NormalizationResult NormalizeNegated(
        ExpressionSyntax expression,
        string? explicitReceiver = null,
        IReadOnlyList<string>? knownParameters = null,
        bool isInstanceMemberContext = false)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var inverted = InvertCondition(expression);
        var result = Normalize(inverted, explicitReceiver, knownParameters, isInstanceMemberContext);

        return result with { IsNegated = true };
    }

    /// <summary>
    /// Finds candidate instance receiver identifiers in an expression (e.g. "slot" in "slot.State && slot.CachedAt").
    /// </summary>
    public static string? DetectCandidateReceiver(ExpressionSyntax expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var memberAccesses = expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>();

        foreach (var ma in memberAccesses)
        {
            if (ma.Expression is IdentifierNameSyntax id)
            {
                var name = id.Identifier.ValueText;
                if (!string.IsNullOrEmpty(name) && !char.IsUpper(name[0]) && !RuntimeStaticTypes.Contains(name))
                {
                    counts[name] = counts.GetValueOrDefault(name) + 1;
                }
            }
        }

        if (counts.Count == 0)
        {
            return null;
        }

        return counts.OrderByDescending(kv => kv.Value).First().Key;
    }

    /// <summary>
    /// Recursively inverts a condition using De Morgan's laws and operator inversion.
    /// </summary>
    public static ExpressionSyntax InvertCondition(ExpressionSyntax expression)
    {
        var unwrapped = UnwrapParentheses(expression);

        // 1. !inner -> inverting !(inner) yields inner
        if (unwrapped is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } notExpr)
        {
            return UnwrapParentheses(notExpr.Operand);
        }

        // 2. Binary logical expressions (De Morgan)
        if (unwrapped is BinaryExpressionSyntax binary)
        {
            if (binary.IsKind(SyntaxKind.LogicalAndExpression))
            {
                // !(A && B) -> !A || !B
                var leftInverted = InvertCondition(binary.Left);
                var rightInverted = InvertCondition(binary.Right);
                return SyntaxFactory.BinaryExpression(SyntaxKind.LogicalOrExpression, leftInverted, rightInverted);
            }

            if (binary.IsKind(SyntaxKind.LogicalOrExpression))
            {
                // !(A || B) -> !A && !B
                var leftInverted = InvertCondition(binary.Left);
                var rightInverted = InvertCondition(binary.Right);
                if (leftInverted.IsKind(SyntaxKind.LogicalOrExpression))
                {
                    leftInverted = SyntaxFactory.ParenthesizedExpression(leftInverted);
                }
                if (rightInverted.IsKind(SyntaxKind.LogicalOrExpression))
                {
                    rightInverted = SyntaxFactory.ParenthesizedExpression(rightInverted);
                }
                return SyntaxFactory.BinaryExpression(SyntaxKind.LogicalAndExpression, leftInverted, rightInverted);
            }

            // Relational operators
            SyntaxKind? invertedKind = binary.Kind() switch
            {
                SyntaxKind.EqualsExpression => SyntaxKind.NotEqualsExpression,
                SyntaxKind.NotEqualsExpression => SyntaxKind.EqualsExpression,
                SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanOrEqualExpression,
                SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanExpression,
                SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanOrEqualExpression,
                SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanExpression,
                _ => null
            };

            if (invertedKind.HasValue)
            {
                return SyntaxFactory.BinaryExpression(invertedKind.Value, binary.Left, binary.Right);
            }
        }

        // 3. Pattern matching (expr is null <-> expr is not null)
        if (unwrapped is IsPatternExpressionSyntax isPattern)
        {
            if (isPattern.Pattern is ConstantPatternSyntax { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } })
            {
                return SyntaxFactory.IsPatternExpression(
                    isPattern.Expression,
                    SyntaxFactory.UnaryPattern(
                        SyntaxFactory.Token(SyntaxKind.NotKeyword),
                        isPattern.Pattern));
            }

            if (isPattern.Pattern is UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern } notPattern)
            {
                return SyntaxFactory.IsPatternExpression(isPattern.Expression, notPattern.Pattern);
            }
        }

        // 4. Boolean literal
        if (unwrapped.IsKind(SyntaxKind.TrueLiteralExpression))
        {
            return SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression);
        }
        if (unwrapped.IsKind(SyntaxKind.FalseLiteralExpression))
        {
            return SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);
        }

        // 5. Fallback: wrap with logical not
        var target = unwrapped is IdentifierNameSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax or ElementAccessExpressionSyntax
            ? unwrapped
            : SyntaxFactory.ParenthesizedExpression(unwrapped);
        return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, target);
    }

    public static ulong ComputeHash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return XxHash64.HashToUInt64(bytes);
    }

    public static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }
        return expression;
    }

    private sealed class NormalizingRewriter : CSharpSyntaxRewriter
    {
        private readonly string? _explicitReceiver;
        private readonly Dictionary<string, string> _variableMap = new(StringComparer.Ordinal);
        private readonly List<string> _orderedArguments = new();
        private readonly HashSet<string> _knownParameterSet = new(StringComparer.Ordinal);
        private readonly bool _isInstanceMemberContext;

        public string? DetectedReceiver => _explicitReceiver;
        public IReadOnlyList<string> OrderedArguments => _orderedArguments;

        public NormalizingRewriter(
            string? explicitReceiver,
            IReadOnlyList<string>? knownParameters,
            bool isInstanceMemberContext)
        {
            _explicitReceiver = explicitReceiver;
            _isInstanceMemberContext = isInstanceMemberContext;

            if (knownParameters != null)
            {
                for (int i = 0; i < knownParameters.Count; i++)
                {
                    var paramName = knownParameters[i];
                    _knownParameterSet.Add(paramName);
                    _variableMap[paramName] = $"${i}";
                }
            }
        }

        public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
        {
            return default;
        }

        public override SyntaxNode? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
        {
            var inner = (ExpressionSyntax)Visit(node.Expression)!;
            return SyntaxFactory.ParenthesizedExpression(inner);
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            // 1. Explicit `this.Member` in instance context
            if (node.Expression is ThisExpressionSyntax)
            {
                return SyntaxFactory.MemberAccessExpression(
                    node.Kind(),
                    SyntaxFactory.IdentifierName("$this"),
                    node.Name);
            }

            // 2. Member on explicit receiver: e.g. slot.State -> $this.State
            if (_explicitReceiver != null &&
                node.Expression is IdentifierNameSyntax idExpr &&
                string.Equals(idExpr.Identifier.ValueText, _explicitReceiver, StringComparison.Ordinal))
            {
                return SyntaxFactory.MemberAccessExpression(
                    node.Kind(),
                    SyntaxFactory.IdentifierName("$this"),
                    node.Name);
            }

            // 3. Enum or constant pattern: e.g. TargetLivenessState.Online, StringComparison.OrdinalIgnoreCase
            // Preserve the enum/constant access as-is so different constants don't collide into false-positive clones
            if (IsEnumOrConstantPattern(node))
            {
                return node;
            }

            // 4. Runtime static member access (e.g. DateTimeOffset.UtcNow) in value position
            if (IsRuntimeStaticPropertyAccess(node))
            {
                return RegisterArgument(node.ToString());
            }

            var visitedExpr = (ExpressionSyntax)Visit(node.Expression)!;
            return node.WithExpression(visitedExpr);
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var receiverText = memberAccess.Expression.ToString();

                // If invocation is on explicit receiver (e.g. slot.Method())
                ExpressionSyntax normalizedReceiver;
                if (_explicitReceiver != null && string.Equals(receiverText, _explicitReceiver, StringComparison.Ordinal))
                {
                    normalizedReceiver = SyntaxFactory.IdentifierName("$this");
                }
                else
                {
                    normalizedReceiver = (ExpressionSyntax)Visit(memberAccess.Expression)!;
                }

                var normalizedArgs = (ArgumentListSyntax)Visit(node.ArgumentList)!;

                var newTarget = SyntaxFactory.MemberAccessExpression(
                    memberAccess.Kind(),
                    normalizedReceiver,
                    memberAccess.Name);

                return node.WithExpression(newTarget).WithArgumentList(normalizedArgs);
            }

            return base.VisitInvocationExpression(node);
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            var name = node.Identifier.ValueText;

            // In helper definition context inside an instance class:
            // Unqualified member access that is not a parameter is implicitly `this.Member`!
            if (_isInstanceMemberContext && !_knownParameterSet.Contains(name) && !RuntimeStaticTypes.Contains(name))
            {
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("$this"),
                    node);
            }

            // If identifier is a runtime static type name (e.g. DateTimeOffset), preserve as is
            if (RuntimeStaticTypes.Contains(name))
            {
                return node;
            }

            // If it matches explicit receiver at call site, map to $this
            if (_explicitReceiver != null && string.Equals(name, _explicitReceiver, StringComparison.Ordinal))
            {
                return SyntaxFactory.IdentifierName("$this");
            }

            return RegisterArgument(name);
        }

        public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            // Preserve exact literal values (numbers, strings, characters, booleans, null)
            // so conditions with different literals are not falsely merged into identical clones.
            return node;
        }

        private IdentifierNameSyntax RegisterArgument(string expressionText)
        {
            if (!_variableMap.TryGetValue(expressionText, out var mapped))
            {
                mapped = $"${_variableMap.Count}";
                _variableMap[expressionText] = mapped;
                _orderedArguments.Add(expressionText);
            }

            return SyntaxFactory.IdentifierName(mapped);
        }

        private static bool IsRuntimeStaticPropertyAccess(MemberAccessExpressionSyntax node)
        {
            var parent = node.Parent;
            while (parent is ParenthesizedExpressionSyntax p)
            {
                parent = p.Parent;
            }

            if (parent is InvocationExpressionSyntax inv && inv.Expression == node)
            {
                return false;
            }

            if (node.Expression is IdentifierNameSyntax id && RuntimeStaticTypes.Contains(id.Identifier.ValueText))
            {
                return true;
            }
            return false;
        }

        private static bool IsEnumOrConstantPattern(MemberAccessExpressionSyntax node)
        {
            var parent = node.Parent;
            while (parent is ParenthesizedExpressionSyntax p)
            {
                parent = parent.Parent;
            }

            if (parent is InvocationExpressionSyntax inv && inv.Expression == node)
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

            // Do not fold runtime static types like DateTimeOffset.UtcNow to CONST
            if (node.Expression is IdentifierNameSyntax id && RuntimeStaticTypes.Contains(id.Identifier.ValueText))
            {
                return false;
            }

            string? typeName = node.Expression switch
            {
                IdentifierNameSyntax idExpr => idExpr.Identifier.ValueText,
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

            bool isEnumPattern = char.IsUpper(typeName[0]) &&
                (node.Expression is IdentifierNameSyntax || AllComponentsUppercase(node.Expression));
            bool isPredefinedConstant = node.Expression is PredefinedTypeSyntax;

            return isEnumPattern || isPredefinedConstant;
        }

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
