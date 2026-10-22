using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Filtering;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Strings;

/// <summary>
/// Detects and groups repeated inline string literals across C# syntax trees.
/// </summary>
public sealed class RepeatedStringDetector : IRepeatedStringDetector
{
    /// <inheritdoc />
    public IReadOnlyList<RepeatedStringOccurrence> ExtractStringLiterals(
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

        var walker = new StringLiteralWalker(syntaxTree, filePath, settings, projectId, fileCategory);
        walker.Visit(root);
        return walker.Occurrences;
    }

    /// <inheritdoc />
    public IReadOnlyList<RepeatedStringGroup> GroupRepeatedStrings(
        IEnumerable<RepeatedStringOccurrence> occurrences,
        CodeCondenserSettings settings,
        Func<string, FileCategory>? fileClassifier = null)
    {
        ArgumentNullException.ThrowIfNull(occurrences);
        ArgumentNullException.ThrowIfNull(settings);

        var minOccurrences = Math.Max(2, settings.RepeatedStringMinOccurrences);
        var minLength = settings.RepeatedStringLengthThreshold;

        var grouped = occurrences
            .Where(o => (o.EndColumn - o.StartColumn > 0) || !string.IsNullOrEmpty(o.FilePath))
            .GroupBy(o => o.StringValue, StringComparer.Ordinal)
            .Where(g => !string.IsNullOrEmpty(g.Key) && g.Key.Length >= minLength && g.Count() >= minOccurrences)
            .ToList();

        var result = new List<RepeatedStringGroup>(grouped.Count);

        foreach (var group in grouped)
        {
            var stringValue = group.Key;
            var occList = group.ToList();

            // Determine clone category across occurrences
            var categories = occList.Select(o =>
            {
                if (fileClassifier != null)
                {
                    return fileClassifier(o.FilePath);
                }
                return o.Category;
            }).Distinct().ToList();

            CloneCategory clusterCategory;
            if (categories.Count == 1 && categories[0] == FileCategory.Test)
            {
                clusterCategory = CloneCategory.Test;
            }
            else if (categories.Count == 1 && categories[0] == FileCategory.Production)
            {
                clusterCategory = CloneCategory.Production;
            }
            else
            {
                clusterCategory = CloneCategory.CrossBoundary;
            }

            var constantName = ConstantNamingHelper.SuggestConstantName(stringValue);

            var targetNamespace = occList.Select(o => o.Namespace).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "ApplicationNamespace";

            result.Add(new RepeatedStringGroup
            {
                Value = stringValue,
                Occurrences = occList,
                Category = clusterCategory,
                SuggestedConstantName = constantName,
                SuggestedClassName = "MyConstants",
                TargetNamespace = targetNamespace
            });
        }

        // Sort by impact (characters occupied by repetitions) descending
        return result
            .OrderByDescending(g => g.Length * g.Occurrences.Count)
            .ToList();
    }

    private sealed class StringLiteralWalker : CSharpSyntaxWalker
    {
        private readonly SyntaxTree _syntaxTree;
        private readonly string _filePath;
        private readonly CodeCondenserSettings _settings;
        private readonly string? _projectId;
        private readonly FileCategory _fileCategory;
        private readonly List<RepeatedStringOccurrence> _occurrences = new();

        public IReadOnlyList<RepeatedStringOccurrence> Occurrences => _occurrences;

        public StringLiteralWalker(
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

        public override void VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.StringLiteralExpression) && node.Token.Value is string value)
            {
                ProcessString(node, value);
            }

            base.VisitLiteralExpression(node);
        }

        public override void VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
        {
            // If the interpolated string has no dynamic holes (pure string without {} expressions)
            if (node.Contents.Count > 0 && node.Contents.All(c => c is InterpolatedStringTextSyntax))
            {
                var value = string.Concat(node.Contents.OfType<InterpolatedStringTextSyntax>().Select(t => t.TextToken.ValueText));
                ProcessString(node, value);
            }

            base.VisitInterpolatedStringExpression(node);
        }

        private void ProcessString(ExpressionSyntax node, string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < _settings.RepeatedStringLengthThreshold)
            {
                return;
            }

            if (_settings.IgnoredStrings.Contains(value))
            {
                return;
            }

            if (RepeatedStringIgnoreMarker.IsIgnored(node))
            {
                return;
            }

            if (CheckIsConstDeclaration(node))
            {
                return;
            }

            var lineSpan = _syntaxTree.GetLineSpan(node.Span);
            int startLine = lineSpan.StartLinePosition.Line + 1;
            int endLine = lineSpan.EndLinePosition.Line + 1;
            int startCol = lineSpan.StartLinePosition.Character + 1;
            int endCol = lineSpan.EndLinePosition.Character + 1;

            string? enclosingMember = FindEnclosingMember(node);
            string? enclosingNamespace = FindEnclosingNamespace(node);

            _occurrences.Add(new RepeatedStringOccurrence
            {
                FilePath = _filePath,
                StartLine = startLine,
                EndLine = endLine,
                StartColumn = startCol,
                EndColumn = endCol,
                EnclosingMember = enclosingMember,
                Namespace = enclosingNamespace,
                ProjectId = _projectId,
                Category = _fileCategory,
                IsConstDeclaration = false,
                StringValue = value
            });
        }

        private static bool CheckIsConstDeclaration(ExpressionSyntax node)
        {
            if (node.Parent is EqualsValueClauseSyntax evc &&
                evc.Parent is VariableDeclaratorSyntax vd &&
                vd.Parent is VariableDeclarationSyntax vdecl)
            {
                if (vdecl.Parent is FieldDeclarationSyntax fd && fd.Modifiers.Any(SyntaxKind.ConstKeyword))
                {
                    return true;
                }

                if (vdecl.Parent is LocalDeclarationStatementSyntax ld && ld.Modifiers.Any(SyntaxKind.ConstKeyword))
                {
                    return true;
                }
            }

            return false;
        }

        private static string? FindEnclosingMember(SyntaxNode node)
        {
            var current = node.Parent;
            while (current != null)
            {
                switch (current)
                {
                    case MethodDeclarationSyntax m:
                        return m.Identifier.ValueText;
                    case PropertyDeclarationSyntax p:
                        return p.Identifier.ValueText;
                    case ConstructorDeclarationSyntax c:
                        return c.Identifier.ValueText;
                    case LocalFunctionStatementSyntax lf:
                        return lf.Identifier.ValueText;
                    case AccessorDeclarationSyntax acc:
                        var prop = acc.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
                        return prop != null ? $"{prop.Identifier.ValueText}.{acc.Keyword.ValueText}" : acc.Keyword.ValueText;
                    case ClassDeclarationSyntax cls:
                        return cls.Identifier.ValueText;
                    case RecordDeclarationSyntax rec:
                        return rec.Identifier.ValueText;
                    case StructDeclarationSyntax st:
                        return st.Identifier.ValueText;
                }

                current = current.Parent;
            }

            return null;
        }

        private static string? FindEnclosingNamespace(SyntaxNode node)
        {
            var nsNode = node.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
            return nsNode?.Name.ToString();
        }
    }
}
