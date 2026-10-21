using Pmad.CodeCondenser.Cli.Formatting;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Cli.Commands;

/// <summary>
/// Executes analysis and rewrites verbose comments directly in source files on disk using the cached summaries.
/// </summary>
public sealed class ApplyCommand : ICommand
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

        formatter.PrintInfo(output, $"Applying comment summaries to target: {Path.GetFullPath(options.TargetPath)}");
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

        formatter.PrintCloneGroups(output, result.CloneGroups, options.TargetPath);
        if (result.SubMethodCloneGroups.Count > 0)
        {
            formatter.PrintSubMethodCloneGroups(output, result.SubMethodCloneGroups, options.TargetPath);
        }

        int modifiedCount = 0;
        if (engine is CodeCondenserEngine cce)
        {
            void OnFileApplied(string file)
            {
                formatter.PrintFileApplied(output, file, options.TargetPath);
                Interlocked.Increment(ref modifiedCount);
            }

            cce.FileApplied += OnFileApplied;
            try
            {
                await cce.ApplySummariesWithModifiedFilesAsync(result, settings, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                cce.FileApplied -= OnFileApplied;
            }

            if (modifiedCount > 0)
            {
                await cce.UpdateSnapshotBaselineAsync(options.TargetPath, settings, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            await engine.ApplySummariesAsync(result, settings, cancellationToken).ConfigureAwait(false);
        }

        if (modifiedCount == 0 && result.VerboseComments.Count == 0)
        {
            formatter.PrintSuccess(output, "No source files required modification.");
        }
        else
        {
            output.WriteLine();
            formatter.PrintSuccess(output, $"Successfully updated comments across modified source file(s).");
        }

        var mode = options.Incremental ? "Apply (Incremental)" : "Apply";
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
            result.CloneGroups.Count(g => g.Category != CloneCategory.Test),
            result.CloneGroups.Count(g => g.Category == CloneCategory.Test),
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

        return ExitCodes.Success;
    }
}
