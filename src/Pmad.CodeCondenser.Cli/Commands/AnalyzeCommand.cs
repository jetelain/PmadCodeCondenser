using Pmad.CodeCondenser.Cli.Formatting;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;

namespace Pmad.CodeCondenser.Cli.Commands;

/// <summary>
/// Executes code clone analysis and verbose comment detection in dry-run mode (does not modify source files).
/// </summary>
public sealed class AnalyzeCommand : ICommand
{
    public async Task<int> ExecuteAsync(
        CliOptions options,
        CodeCondenserSettings settings,
        ICodeCondenserEngine engine,
        IConsoleFormatter formatter,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(formatter);
        ArgumentNullException.ThrowIfNull(output);

        formatter.PrintInfo(output, $"Analyzing target: {Path.GetFullPath(options.TargetPath)} (Mode: Dry-Run{(options.Incremental ? " Incremental" : "")})");
        output.WriteLine();

        var progress = new Progress<Pmad.CodeCondenser.Core.Comments.CommentSummarizationProgress>(p =>
        {
            formatter.PrintCommentProgress(output, p.Completed, p.Total);
        });

        var result = await engine.AnalyzeAsync(
            options.TargetPath,
            settings,
            incremental: options.Incremental,
            progress: progress,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        formatter.PrintCloneGroups(output, result.CloneGroups, options.TargetPath);
        formatter.PrintVerboseComments(output, result.VerboseComments, result.SummarizedComments, options.TargetPath);

        var mode = options.Incremental ? "Dry-Run (Incremental)" : "Dry-Run";
        formatter.PrintSummary(
            output,
            result.AnalyzedFilesCount,
            result.TotalMethodsScanned,
            result.CloneGroups.Count,
            result.VerboseComments.Count,
            mode);

        if (options.Incremental && settings.EnableDuplicationDetection && result.CloneGroups.Count > settings.DuplicationTolerance)
        {
            formatter.PrintAiActionableDuplicationFeedback(output, result.CloneGroups, options.TargetPath, settings.DuplicationTolerance);
            return settings.DuplicationExitCode;
        }

        if (options.UpdateSnapshot && engine is CodeCondenserEngine cce)
        {
            await cce.UpdateSnapshotBaselineAsync(options.TargetPath, settings, cancellationToken).ConfigureAwait(false);
            formatter.PrintSnapshotSaved(output, settings.SnapshotFilePath);
            output.WriteLine();
        }

        return ExitCodes.Success;
    }
}
