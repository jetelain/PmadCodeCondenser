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

        _consoleFormatter.PrintCloneGroups(output, result.CloneGroups, baseDirectory, estimator);
        if (result.SubMethodCloneGroups.Count > 0)
        {
            _consoleFormatter.PrintSubMethodCloneGroups(output, result.SubMethodCloneGroups, baseDirectory, estimator);
        }
        _consoleFormatter.PrintVerboseComments(output, result.VerboseComments, result.SummarizedComments, baseDirectory, estimator);

        var mode = _isIncremental ? "Dry-Run (Incremental)" : "Dry-Run";
        int prodClones = result.CloneGroups.Count(g => g.Category != CloneCategory.Test);
        int testClones = result.CloneGroups.Count(g => g.Category == CloneCategory.Test);

        var commentGainResult = result.CalculateCommentGain(estimator);
        int? commentGain = commentGainResult.SummarizedCount > 0 ? commentGainResult.GainCharacters : null;
        int? commentReduction = commentGainResult.SummarizedCount > 0 ? commentGainResult.ReductionPercent : null;
        int? commentGainTokens = commentGainResult.SummarizedCount > 0 ? commentGainResult.GainTokens : null;

        _consoleFormatter.PrintSummary(
            output,
            result.AnalyzedFilesCount,
            result.TotalMethodsScanned,
            result.CloneGroups.Count,
            result.VerboseComments.Count,
            mode,
            result.ProductionFilesCount,
            result.TestFilesCount,
            result.SkippedGeneratedFilesCount,
            prodClones,
            testClones,
            result.SubMethodCloneGroups.Count,
            commentGain,
            commentReduction,
            commentGainTokens,
            CloneGainEstimator.EstimateTotalGainTokens(result.CloneGroups, result.SubMethodCloneGroups, estimator));

        return Task.CompletedTask;
    }
}
