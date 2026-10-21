using Pmad.CodeCondenser.Cli.Formatting;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;
using Pmad.CodeCondenser.Core.Models;

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

        if (engine is CodeCondenserEngine engineWithInfo)
        {
            engineWithInfo.InfoLogged += msg =>
            {
                var clean = msg.StartsWith("[Info] ", StringComparison.Ordinal) ? msg[7..] : msg;
                formatter.PrintInfo(output, clean);
                output.WriteLine();
            };
        }

        var result = await engine.AnalyzeAsync(
            options.TargetPath,
            settings,
            incremental: options.Incremental,
            progress: progress,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.SkippedGeneratedFilesCount > 0)
        {
            formatter.PrintInfo(output, $"Skipped {result.SkippedGeneratedFilesCount} auto-generated file(s).");
            output.WriteLine();
        }

        formatter.PrintCloneGroups(output, result.CloneGroups, options.TargetPath);
        if (result.SubMethodCloneGroups.Count > 0)
        {
            formatter.PrintSubMethodCloneGroups(output, result.SubMethodCloneGroups, options.TargetPath);
        }
        formatter.PrintVerboseComments(output, result.VerboseComments, result.SummarizedComments, options.TargetPath);

        var mode = options.Incremental ? "Dry-Run (Incremental)" : "Dry-Run";
        int prodClones = result.CloneGroups.Count(g => g.Category != CloneCategory.Test);
        int testClones = result.CloneGroups.Count(g => g.Category == CloneCategory.Test);

        formatter.PrintSummary(
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
            result.SubMethodCloneGroups.Count);

        if (options.Incremental && (settings.EnableDuplicationDetection || settings.EnableSubMethodCloneDetection))
        {
            var gatedClones = settings.EnableDuplicationDetection
                ? result.CloneGroups
                    .Where(g => g.Category != CloneCategory.Test || settings.EnforceTestDuplication)
                    .ToList()
                : [];

            var gatedSubMethodClones = settings.EnableSubMethodCloneDetection
                ? result.SubMethodCloneGroups
                    .Where(g => g.Category != CloneCategory.Test || settings.EnforceTestDuplication)
                    .ToList()
                : [];

            int totalGated = gatedClones.Count + gatedSubMethodClones.Count;

            if (totalGated > settings.DuplicationTolerance)
            {
                formatter.PrintAiActionableDuplicationFeedback(output, gatedClones, gatedSubMethodClones, options.TargetPath, settings.DuplicationTolerance);
                return settings.DuplicationExitCode;
            }
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
