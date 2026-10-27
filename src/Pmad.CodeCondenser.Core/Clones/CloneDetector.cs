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
    private readonly IProjectLocator _projectLocator;
    private readonly ISubMethodCloneDetector _subMethodDetector;

    /// <summary>
    /// Initializes a new instance of <see cref="CloneDetector"/>.
    /// </summary>
    /// <param name="normalizer">AST normalizer (defaults to <see cref="AstNormalizer"/>).</param>
    /// <param name="hasher">Method hasher (defaults to <see cref="MethodHasher"/>).</param>
    /// <param name="extractor">Method extractor (defaults to <see cref="MethodExtractor"/>).</param>
    /// <param name="projectLocator">Project locator (defaults to <see cref="ProjectLocator"/>).</param>
    /// <param name="subMethodDetector">Sub-method clone detector (defaults to <see cref="SubMethodCloneDetector"/>).</param>
    public CloneDetector(
        IAstNormalizer? normalizer = null,
        IMethodHasher? hasher = null,
        IMethodExtractor? extractor = null,
        IProjectLocator? projectLocator = null,
        ISubMethodCloneDetector? subMethodDetector = null)
    {
        _normalizer = normalizer ?? new AstNormalizer();
        _hasher = hasher ?? new MethodHasher();
        _extractor = extractor ?? new MethodExtractor();
        _projectLocator = projectLocator ?? new ProjectLocator();
        _subMethodDetector = subMethodDetector ?? new SubMethodCloneDetector(_normalizer, _projectLocator);
    }

    /// <inheritdoc />
    public IReadOnlyList<MethodCloneInfo> ExtractMethods(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings)
    {
        return ExtractMethods(syntaxTree, filePath, settings, projectId: null);
    }

    /// <inheritdoc />
    public IReadOnlyList<MethodCloneInfo> ExtractMethods(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings,
        string? projectId)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        ArgumentNullException.ThrowIfNull(settings);

        var methods = _extractor.ExtractWithStatementCounts(syntaxTree, settings.DuplicationStatementThreshold);
        var result = new List<MethodCloneInfo>(methods.Count);

        projectId ??= _projectLocator.LocateProjectId(filePath);

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
                Signature: signature,
                ProjectId: projectId));
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

        var results = new List<CodeCloneGroup>();

        foreach (var hashGroup in methods.GroupBy(m => m.NormalizedHash))
        {
            var methodList = hashGroup.ToList();
            if (methodList.Count < 2)
            {
                continue;
            }

            var distinctProjects = methodList
                .Select(m => m.ProjectId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int statementCount = methodList.Max(m => m.StatementCount);

            if (distinctProjects.Count > 1)
            {
                // Multi-project cluster
                if (statementCount >= 12)
                {
                    // Meets cross-project threshold (>= 12 statements)
                    PartitionAndAddCluster(results, hashGroup.Key, methodList, fileClassifier, isCrossProject: true);
                }
                else
                {
                    // Below 12 statements across multiple projects: partition into project-local sub-clusters
                    foreach (var projectGroup in methodList.GroupBy(m => m.ProjectId, StringComparer.OrdinalIgnoreCase))
                    {
                        var localMethods = projectGroup.ToList();
                        if (localMethods.Count >= 2)
                        {
                            PartitionAndAddCluster(results, hashGroup.Key, localMethods, fileClassifier, isCrossProject: false);
                        }
                    }
                }
            }
            else
            {
                // Single-project cluster
                PartitionAndAddCluster(results, hashGroup.Key, methodList, fileClassifier, isCrossProject: false);
            }
        }

        return results
            .OrderByDescending(g => g.ImpactScore)
            .ThenByDescending(g => g.StatementCount)
            .ThenBy(g => g.NormalizedHash, StringComparer.Ordinal)
            .ToList();
    }

    private static void PartitionAndAddCluster(
        List<CodeCloneGroup> results,
        string hash,
        List<MethodCloneInfo> methods,
        Func<string, FileCategory>? fileClassifier,
        bool isCrossProject)
    {
        if (methods.Count < 2)
        {
            return;
        }

        int statementCount = methods.Max(m => m.StatementCount);
        var category = DetermineCategory(methods, fileClassifier);

        if (category == CloneCategory.CrossBoundary && statementCount < 12)
        {
            // CrossBoundary requires >= 12 statements.
            // Partition by category so cross-boundary noise is eliminated.
            foreach (var catGroup in methods.GroupBy(m => fileClassifier != null ? fileClassifier(m.FilePath) : FileCategory.Production))
            {
                var catMethods = catGroup.ToList();
                if (catMethods.Count >= 2)
                {
                    int catStatementCount = catMethods.Max(m => m.StatementCount);
                    int catImpact = catStatementCount * Math.Max(0, catMethods.Count - 1);
                    var cat = catGroup.Key == FileCategory.Test ? CloneCategory.Test : CloneCategory.Production;
                    bool isComp = CheckComplementaryPair(catMethods, catStatementCount, isCrossProject: false);
                    results.Add(new CodeCloneGroup(hash, catMethods, cat, catImpact, isComp));
                }
            }
            return;
        }

        int impact = statementCount * Math.Max(0, methods.Count - 1);
        bool comp = CheckComplementaryPair(methods, statementCount, isCrossProject);
        results.Add(new CodeCloneGroup(hash, methods, category, impact, comp));
    }

    private static readonly (string P1, string P2)[] ComplementaryPrefixPairs =
    [
        ("Join", "Leave"),
        ("Add", "Remove"),
        ("Register", "Unregister"),
        ("Subscribe", "Unsubscribe"),
        ("Open", "Close"),
        ("Start", "Stop"),
        ("Attach", "Detach"),
        ("Encode", "Decode")
    ];

    internal static bool CheckComplementaryPair(
        IReadOnlyList<MethodCloneInfo> methods,
        int statementCount,
        bool isCrossProject)
    {
        int maxStatements = isCrossProject ? 14 : 10;
        if (statementCount > maxStatements || methods.Count < 2)
        {
            return false;
        }

        for (int i = 0; i < methods.Count; i++)
        {
            for (int j = i + 1; j < methods.Count; j++)
            {
                var name1 = methods[i].MethodName;
                var name2 = methods[j].MethodName;

                foreach (var (p1, p2) in ComplementaryPrefixPairs)
                {
                    if (MatchesComplementaryPair(name1, name2, p1, p2) ||
                        MatchesComplementaryPair(name2, name1, p1, p2))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool MatchesComplementaryPair(string name1, string name2, string p1, string p2)
    {
        if (name1.StartsWith(p1, StringComparison.Ordinal) && name2.StartsWith(p2, StringComparison.Ordinal))
        {
            var suffix1 = name1[p1.Length..];
            var suffix2 = name2[p2.Length..];
            if ((suffix1.Length == 0 || char.IsUpper(suffix1[0])) &&
                string.Equals(suffix1, suffix2, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static CloneCategory DetermineCategory(
        List<MethodCloneInfo> methods,
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

    /// <inheritdoc />
    public IReadOnlyList<SubMethodBlock> ExtractSubMethodBlocks(
        SyntaxTree syntaxTree,
        string filePath,
        CodeCondenserSettings settings,
        string? projectId = null)
    {
        return _subMethodDetector.ExtractBlocks(syntaxTree, filePath, settings, projectId);
    }

    /// <inheritdoc />
    public IReadOnlyList<SubMethodCloneGroup> GroupSubMethodClones(
        IEnumerable<SubMethodBlock> blocks,
        int threshold,
        IReadOnlyList<CodeCloneGroup>? wholeMethodClones = null,
        Func<string, FileCategory>? fileClassifier = null)
    {
        return _subMethodDetector.DetectClones(blocks, threshold, wholeMethodClones, fileClassifier);
    }

    /// <inheritdoc />
    public IReadOnlyList<SubMethodCloneGroup> DetectSubMethodClones(
        IEnumerable<(SyntaxTree Tree, string FilePath)> syntaxTrees,
        CodeCondenserSettings settings,
        IReadOnlyList<CodeCloneGroup>? wholeMethodClones = null,
        Func<string, FileCategory>? fileClassifier = null)
    {
        return _subMethodDetector.DetectClones(syntaxTrees, settings, wholeMethodClones, fileClassifier);
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
