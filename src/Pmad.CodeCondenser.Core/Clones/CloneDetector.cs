using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Clones;

/// <summary>
/// Detects structural code clones across C# syntax trees.
/// </summary>
public sealed class CloneDetector : ICloneDetector
{
    private readonly IAstNormalizer _normalizer;
    private readonly IMethodHasher _hasher;
    private readonly IMethodExtractor _extractor;

    /// <summary>
    /// Initializes a new instance of <see cref="CloneDetector"/>.
    /// </summary>
    /// <param name="normalizer">AST normalizer (defaults to <see cref="AstNormalizer"/>).</param>
    /// <param name="hasher">Method hasher (defaults to <see cref="MethodHasher"/>).</param>
    /// <param name="extractor">Method extractor (defaults to <see cref="MethodExtractor"/>).</param>
    public CloneDetector(
        IAstNormalizer? normalizer = null,
        IMethodHasher? hasher = null,
        IMethodExtractor? extractor = null)
    {
        _normalizer = normalizer ?? new AstNormalizer();
        _hasher = hasher ?? new MethodHasher();
        _extractor = extractor ?? new MethodExtractor();
    }

    /// <inheritdoc />
    public IReadOnlyList<MethodCloneInfo> ExtractMethods(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        ArgumentNullException.ThrowIfNull(settings);

        var methods = _extractor.ExtractWithStatementCounts(syntaxTree, settings.DuplicationStatementThreshold);
        var result = new List<MethodCloneInfo>(methods.Count);

        foreach (var (method, statementCount) in methods)
        {
            var lineSpan = syntaxTree.GetLineSpan(method.Span);
            int startLine = lineSpan.StartLinePosition.Line + 1;
            int endLine = lineSpan.EndLinePosition.Line + 1;

            var normalized = _normalizer.Normalize(method);
            var hash = _hasher.ComputeHash(normalized);
            var signature = BuildSignature(method);

            result.Add(new MethodCloneInfo(
                FilePath: filePath,
                MethodName: method.Identifier.ValueText,
                StartLine: startLine,
                EndLine: endLine,
                StatementCount: statementCount,
                NormalizedHash: hash,
                Signature: signature));
        }

        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<CodeCloneGroup> GroupClones(IEnumerable<MethodCloneInfo> methods)
    {
        return GroupClones(methods, fileClassifier: null);
    }

    /// <inheritdoc />
    public IReadOnlyList<CodeCloneGroup> GroupClones(
        IEnumerable<MethodCloneInfo> methods,
        Func<string, FileCategory>? fileClassifier)
    {
        ArgumentNullException.ThrowIfNull(methods);

        return methods
            .GroupBy(m => m.NormalizedHash)
            .Where(g => g.Count() > 1)
            .Select(g =>
            {
                var methodList = g.ToList();
                var statementCount = methodList.Count > 0 ? methodList.Max(m => m.StatementCount) : 0;
                var impactScore = statementCount * Math.Max(0, methodList.Count - 1);
                var category = DetermineCategory(methodList, fileClassifier);
                return new CodeCloneGroup(g.Key, methodList, category, impactScore);
            })
            .OrderByDescending(g => g.ImpactScore)
            .ThenByDescending(g => g.StatementCount)
            .ThenBy(g => g.NormalizedHash, StringComparer.Ordinal)
            .ToList();
    }

    private static CloneCategory DetermineCategory(
        IReadOnlyList<MethodCloneInfo> methods,
        Func<string, FileCategory>? fileClassifier)
    {
        if (fileClassifier == null || methods.Count == 0)
        {
            return CloneCategory.Production;
        }

        bool hasProduction = false;
        bool hasTest = false;

        foreach (var method in methods)
        {
            var cat = fileClassifier(method.FilePath);
            if (cat == FileCategory.Test)
            {
                hasTest = true;
            }
            else
            {
                hasProduction = true;
            }
        }

        if (hasProduction && hasTest)
        {
            return CloneCategory.CrossBoundary;
        }

        if (hasTest)
        {
            return CloneCategory.Test;
        }

        return CloneCategory.Production;
    }

    /// <inheritdoc />
    public IReadOnlyList<CodeCloneGroup> DetectClones(
        IEnumerable<(SyntaxTree Tree, string FilePath)> syntaxTrees,
        CodeCondenserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(syntaxTrees);
        ArgumentNullException.ThrowIfNull(settings);

        var allMethods = new List<MethodCloneInfo>();
        foreach (var (tree, path) in syntaxTrees)
        {
            allMethods.AddRange(ExtractMethods(tree, path, settings));
        }

        return GroupClones(allMethods);
    }

    internal static string BuildSignature(MethodDeclarationSyntax method)
    {
        var modifiers = method.Modifiers.ToString();
        var returnType = method.ReturnType.ToString();
        var name = method.Identifier.Text;
        var typeParams = method.TypeParameterList?.ToString() ?? string.Empty;
        var parameters = method.ParameterList.ToString();

        var prefix = string.IsNullOrWhiteSpace(modifiers) ? returnType : $"{modifiers} {returnType}";
        return $"{prefix} {name}{typeParams}{parameters}".Trim();
    }
}
