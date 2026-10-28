using Pmad.CodeCondenser.Core.Clones;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Cli.Formatting;

/// <summary>
/// Renders analysis results as formatted console output using an <see cref="IConsoleFormatter"/>.
/// </summary>
public sealed class ConsoleReportFormatter : IReportFormatter
{
    private readonly IConsoleFormatter _consoleFormatter;
    private readonly bool _isIncremental;

    public ConsoleReportFormatter(IConsoleFormatter consoleFormatter, bool isIncremental = false)
    {
        _consoleFormatter = consoleFormatter ?? throw new ArgumentNullException(nameof(consoleFormatter));
        _isIncremental = isIncremental;
    }

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

        var estimator = CodeCondenserEngine.ResolveEstimator(settings);
        var displayed = DisplayedItemLimiter.SelectItems(result, settings, baseDirectory, estimator);

        bool isDuplicationAnalyzed = settings.EnableDuplicationDetection;
        bool isSubMethodAnalyzed = settings.EnableSubMethodCloneDetection;
        bool isStringsAnalyzed = settings.EnableRepeatedStringDetection;
        bool isTypeAnalyzed = settings.EnableTypeSplitting;
        bool isLocAnalyzed = settings.EnableLinesOfCodeDetection;
        bool isPredicatesAnalyzed = settings.EnablePredicateCloneDetection;
        bool isCommentsAnalyzed = settings.GetEffectiveSummaryMode() != SummaryMode.Disabled && settings.EnableCommentSummarization;

        if (isDuplicationAnalyzed)
        {
            int totalProd = result.CloneGroups.Count(g => g.Category != CloneCategory.Test);
            int totalTest = result.CloneGroups.Count(g => g.Category == CloneCategory.Test);
            _consoleFormatter.PrintCloneGroups(
                output,
                displayed.CloneGroups,
                baseDirectory,
                estimator,
                totalCount: result.CloneGroups.Count,
                totalProductionCount: totalProd,
                totalTestCount: totalTest);
        }

        if (isSubMethodAnalyzed && (displayed.SubMethodCloneGroups.Count > 0 || result.SubMethodCloneGroups.Count > 0))
        {
            _consoleFormatter.PrintSubMethodCloneGroups(
                output,
                displayed.SubMethodCloneGroups,
                baseDirectory,
                estimator,
                totalCount: result.SubMethodCloneGroups.Count);
        }

        if (isStringsAnalyzed && (displayed.RepeatedStringGroups.Count > 0 || result.RepeatedStringGroups.Count > 0))
        {
            int totalProd = result.RepeatedStringGroups.Count(g => g.Category != CloneCategory.Test);
            int totalTest = result.RepeatedStringGroups.Count(g => g.Category == CloneCategory.Test);
            _consoleFormatter.PrintRepeatedStringGroups(
                output,
                displayed.RepeatedStringGroups,
                baseDirectory,
                estimator,
                totalCount: result.RepeatedStringGroups.Count,
                totalProductionCount: totalProd,
                totalTestCount: totalTest);
        }

        if (isTypeAnalyzed && (displayed.TypeFileIssues.Count > 0 || result.TypeFileIssues.Any(i => i.HasViolation)))
        {
            _consoleFormatter.PrintTypeFileIssues(
                output,
                displayed.TypeFileIssues,
                baseDirectory,
                settings.TypeLineTolerance,
                totalCount: result.TypeFileIssues.Count(i => i.HasViolation));
        }

        if (isLocAnalyzed && (displayed.LinesOfCodeViolations.Count > 0 || result.LinesOfCodeViolations.Count > 0))
        {
            _consoleFormatter.PrintLinesOfCodeViolations(
                output,
                displayed.LinesOfCodeViolations,
                baseDirectory,
                totalCount: result.LinesOfCodeViolations.Count);
        }

        if (isPredicatesAnalyzed && (displayed.PredicateCloneGroups.Count > 0 || result.PredicateCloneGroups.Count > 0))
        {
            int totalProd = result.PredicateCloneGroups.Count(g => g.Category != CloneCategory.Test);
            int totalTest = result.PredicateCloneGroups.Count(g => g.Category == CloneCategory.Test);
            _consoleFormatter.PrintPredicateCloneGroups(
                output,
                displayed.PredicateCloneGroups,
                baseDirectory,
                estimator,
                totalCount: result.PredicateCloneGroups.Count,
                totalProductionCount: totalProd,
                totalTestCount: totalTest);
        }

        if (isCommentsAnalyzed)
        {
            _consoleFormatter.PrintVerboseComments(
                output,
                displayed.VerboseComments,
                result.SummarizedComments,
                baseDirectory,
                estimator,
                totalCount: result.VerboseComments.Count);
        }

        var mode = _isIncremental ? "Dry-Run (Incremental)" : "Dry-Run";

        int? methodsScanned = isDuplicationAnalyzed ? result.TotalMethodsScanned : null;
        int? cloneGroupsCount = isDuplicationAnalyzed ? result.CloneGroups.Count : null;
        int? prodClones = isDuplicationAnalyzed ? result.CloneGroups.Count(g => g.Category != CloneCategory.Test) : null;
        int? testClones = isDuplicationAnalyzed ? result.CloneGroups.Count(g => g.Category == CloneCategory.Test) : null;
        int? subMethodClones = isSubMethodAnalyzed ? result.SubMethodCloneGroups.Count : null;
        int? cloneGainTokens = isDuplicationAnalyzed ? CloneGainEstimator.EstimateTotalGainTokens(result.CloneGroups, result.SubMethodCloneGroups, estimator, baseDirectory) : null;

        var commentGainResult = isCommentsAnalyzed ? result.CalculateCommentGain(estimator) : null;
        int? verboseCommentsCount = isCommentsAnalyzed ? result.VerboseComments.Count : null;
        int? commentGain = (commentGainResult != null && commentGainResult.SummarizedCount > 0) ? commentGainResult.GainCharacters : null;
        int? commentReduction = (commentGainResult != null && commentGainResult.SummarizedCount > 0) ? commentGainResult.ReductionPercent : null;
        int? commentGainTokens = (commentGainResult != null && commentGainResult.SummarizedCount > 0) ? commentGainResult.GainTokens : null;

        int? repeatedStringsCount = isStringsAnalyzed ? result.RepeatedStringGroups.Count : null;
        int? repeatedStringGainChars = (isStringsAnalyzed && result.RepeatedStringGroups.Count > 0) ? result.TotalRepeatedStringGainCharacters : null;
        int? repeatedStringGainTokens = (isStringsAnalyzed && result.RepeatedStringGroups.Count > 0) ? result.TotalRepeatedStringGainTokens : null;

        int? typeIssuesCount = isTypeAnalyzed ? result.TypeFileIssues.Count(i => i.HasViolation) : null;
        int? linesOfCodeViolationsCount = isLocAnalyzed ? result.LinesOfCodeViolations.Count : null;

        int? predicateGroupsCount = isPredicatesAnalyzed ? result.PredicateCloneGroups.Count : null;
        int? predicateGainChars = (isPredicatesAnalyzed && result.PredicateCloneGroups.Count > 0) ? result.TotalPredicateGainCharacters : null;
        int? predicateGainTokens = (isPredicatesAnalyzed && result.PredicateCloneGroups.Count > 0) ? result.TotalPredicateGainTokens : null;

        _consoleFormatter.PrintSummary(
            output,
            result.AnalyzedFilesCount,
            methodsScanned,
            cloneGroupsCount,
            verboseCommentsCount,
            mode,
            result.ProductionFilesCount,
            result.TestFilesCount,
            result.SkippedGeneratedFilesCount,
            prodClones,
            testClones,
            subMethodClones,
            commentGain,
            commentReduction,
            commentGainTokens,
            cloneGainTokens,
            repeatedStringsCount: repeatedStringsCount,
            repeatedStringGainChars: repeatedStringGainChars,
            repeatedStringGainTokens: repeatedStringGainTokens,
            typeIssuesCount: typeIssuesCount,
            linesOfCodeViolationsCount: linesOfCodeViolationsCount,
            predicateGroupsCount: predicateGroupsCount,
            predicateGainChars: predicateGainChars,
            predicateGainTokens: predicateGainTokens);

        return Task.CompletedTask;
    }
}
