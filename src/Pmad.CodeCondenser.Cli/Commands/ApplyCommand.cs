using Pmad.CodeCondenser.Cli.Formatting;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;
using Pmad.CodeCondenser.Core.Filtering;
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

        var baseDirectory = PathFilter.ResolveBaseDirectory(options.TargetPath);

        formatter.PrintCloneGroups(output, result.CloneGroups, baseDirectory);
        if (result.SubMethodCloneGroups.Count > 0)
        {
            formatter.PrintSubMethodCloneGroups(output, result.SubMethodCloneGroups, baseDirectory);
        }

        int modifiedCount = 0;
        if (engine is CodeCondenserEngine cce)
        {
            void OnFileApplied(string file)
            {
                formatter.PrintFileApplied(output, file, baseDirectory);
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

        if (modifiedCount == 0)
        {
            formatter.PrintSuccess(output, "No source files required modification.");
        }
        else
        {
            output.WriteLine();
            formatter.PrintSuccess(output, $"Successfully updated comments across {modifiedCount} modified source file(s).");
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

        if (options.Incremental)
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
            bool duplicationViolation = (settings.EnableDuplicationDetection || settings.EnableSubMethodCloneDetection) && totalGated > settings.DuplicationTolerance;

            var newVerboseComments = settings.GetEffectiveSummaryMode() != Core.Configuration.SummaryMode.Disabled
                ? result.VerboseComments
                : [];

            bool commentViolation = settings.CommentTolerance.HasValue && newVerboseComments.Count > settings.CommentTolerance.Value;
            bool hasCommentIssue = settings.CommentTolerance.HasValue ? commentViolation : newVerboseComments.Count > 0;

            if (duplicationViolation || hasCommentIssue)
            {
                formatter.PrintAiActionableIncrementalFeedback(
                    output,
                    gatedClones,
                    gatedSubMethodClones,
                    newVerboseComments,
                    baseDirectory,
                    settings.DuplicationTolerance,
                    hasDuplicationViolation: duplicationViolation,
                    hasCommentViolation: commentViolation);

                if (duplicationViolation)
                {
                    return settings.DuplicationExitCode;
                }

                if (commentViolation)
                {
                    return settings.CommentExitCode;
                }
            }
        }

        return ExitCodes.Success;
    }
}
