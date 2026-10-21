using System.Text.Json;
using System.Text.Json.Serialization;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Cli.Formatting.Json;

/// <summary>
/// Formats analysis findings into structured, machine-readable JSON documents.
/// </summary>
public sealed class JsonReportFormatter : IReportFormatter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public Task FormatAsync(
        AnalysisResult result,
        CodeCondenserSettings settings,
        TextWriter output,
        string baseDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(output);

        var report = BuildReport(result, settings, baseDirectory);
        var json = JsonSerializer.Serialize(report, SerializerOptions);

        output.WriteLine(json);
        return Task.CompletedTask;
    }

    public static CodeCondenserJsonReport BuildReport(AnalysisResult result, string baseDirectory)
    {
        return BuildReport(result, new CodeCondenserSettings(), baseDirectory);
    }

    public static CodeCondenserJsonReport BuildReport(AnalysisResult result, CodeCondenserSettings settings, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(result);
        settings ??= new CodeCondenserSettings();

        int prodClones = result.CloneGroups.Count(g => g.Category == CloneCategory.Production);
        int testClones = result.CloneGroups.Count(g => g.Category == CloneCategory.Test);
        int crossClones = result.CloneGroups.Count(g => g.Category == CloneCategory.CrossBoundary);

        var summary = new JsonReportSummary
        {
            FilesAnalyzed = result.AnalyzedFilesCount,
            ProductionFiles = result.ProductionFilesCount,
            TestFiles = result.TestFilesCount,
            GeneratedFilesSkipped = result.SkippedGeneratedFilesCount,
            MethodsScanned = result.TotalMethodsScanned,
            CloneClustersCount = result.CloneGroups.Count,
            ProductionClustersCount = prodClones,
            TestClustersCount = testClones,
            CrossBoundaryClustersCount = crossClones,
            SubMethodClustersCount = result.SubMethodCloneGroups.Count,
            VerboseCommentsCount = result.VerboseComments.Count,
            SummaryMode = settings.GetEffectiveSummaryMode().ToString()
        };

        var cloneClusters = new List<JsonCloneCluster>();
        for (int i = 0; i < result.CloneGroups.Count; i++)
        {
            var group = result.CloneGroups[i];
            var methods = group.Methods.Select(m => new JsonCloneMethod
            {
                FilePath = ReportPathHelper.NormalizeRelativePath(m.FilePath, baseDirectory),
                MethodName = m.MethodName,
                StartLine = m.StartLine,
                EndLine = m.EndLine,
                Signature = m.Signature
            }).ToList();

            var recommendation = group.Category == CloneCategory.Test
                ? "Consider parameterized test theories ([Theory] + [InlineData]) or test builder fixtures."
                : "Extract duplicate structural logic into a shared helper method.";

            cloneClusters.Add(new JsonCloneCluster
            {
                ClusterId = i + 1,
                Category = group.Category.ToString(),
                ImpactScore = group.ImpactScore,
                AstHash = group.NormalizedHash,
                StatementCount = group.StatementCount,
                Methods = methods,
                Recommendation = recommendation
            });
        }

        var subMethodClusters = new List<JsonSubMethodCluster>();
        for (int i = 0; i < result.SubMethodCloneGroups.Count; i++)
        {
            var subGroup = result.SubMethodCloneGroups[i];
            var occurrences = subGroup.Occurrences.Select(occ => new JsonSubMethodOccurrence
            {
                FilePath = ReportPathHelper.NormalizeRelativePath(occ.FilePath, baseDirectory),
                MethodName = occ.MethodName,
                StartLine = occ.StartLine,
                EndLine = occ.EndLine
            }).ToList();

            var recommendation = subGroup.Recommendation
                ?? $"Extract duplicate {subGroup.StatementCount}-statement block into a shared helper method.";

            subMethodClusters.Add(new JsonSubMethodCluster
            {
                ClusterId = i + 1,
                Category = subGroup.Category.ToString(),
                StatementCount = subGroup.StatementCount,
                Occurrences = occurrences,
                Recommendation = recommendation
            });
        }

        var verboseComments = result.VerboseComments.Select(c =>
        {
            result.SummarizedComments.TryGetValue(c.ContentHash, out var summaryText);
            var recommendation = c.Kind == CommentKind.XmlDocumentation
                ? "Condense XML doc comment, or add condense=\"no\" attribute to tag to suppress."
                : "Condense comment text to max 120 columns, or add '// codecondenser:ignore' to suppress.";

            return new JsonVerboseComment
            {
                FilePath = ReportPathHelper.NormalizeRelativePath(c.FilePath, baseDirectory),
                StartLine = c.StartLine,
                EndLine = c.EndLine,
                CharacterCount = c.OriginalText.Length,
                Kind = c.Kind.ToString(),
                Summary = summaryText,
                Recommendation = recommendation
            };
        }).ToList();

        return new CodeCondenserJsonReport
        {
            Summary = summary,
            CloneClusters = cloneClusters,
            SubMethodClusters = subMethodClusters,
            VerboseComments = verboseComments
        };
    }
}
