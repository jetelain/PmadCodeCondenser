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
    public static System.CommandLine.Command Create(GlobalOptions globals, CommandServices services)
    {
        ArgumentNullException.ThrowIfNull(globals);
        ArgumentNullException.ThrowIfNull(services);

        var pathArg = new System.CommandLine.Argument<string?>("path")
        {
            Description = "Target directory, solution, or C# file to modify (default: current directory)",
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne
        };

        var cmd = new System.CommandLine.Command("apply", "Apply summarized comments directly to source files on disk");
        cmd.Add(pathArg);

        cmd.SetAction(async (parseResult, ct) =>
        {
            var path = parseResult.GetValue(pathArg) ?? ".";
            var options = globals.BindCliOptions(parseResult, CliOptions.CommandApply, path);
            options.Apply = true;
            return await services.RunCommandAsync(new ApplyCommand(), options, ct).ConfigureAwait(false);
        });

        return cmd;
    }
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

        if (settings.EnableTypeSplitting && settings.GetEffectiveSummaryMode() != Core.Configuration.SummaryMode.Disabled)
        {
            formatter.PrintInfo(output, $"Applying single-type file splits and comment summaries to target: {Path.GetFullPath(options.TargetPath)}");
        }
        else if (settings.EnableTypeSplitting)
        {
            formatter.PrintInfo(output, $"Applying single-type file splits to target: {Path.GetFullPath(options.TargetPath)}");
        }
        else
        {
            formatter.PrintInfo(output, $"Applying comment summaries to target: {Path.GetFullPath(options.TargetPath)}");
        }
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

        var estimator = CodeCondenserEngine.ResolveEstimator(settings);

        formatter.PrintCloneGroups(output, result.CloneGroups, baseDirectory, estimator);
        if (result.SubMethodCloneGroups.Count > 0)
        {
            formatter.PrintSubMethodCloneGroups(output, result.SubMethodCloneGroups, baseDirectory, estimator);
        }
        if (result.TypeFileIssues.Count > 0)
        {
            formatter.PrintTypeFileIssues(output, result.TypeFileIssues, baseDirectory, settings.TypeLineTolerance);
        }

        var modifiedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (engine is CodeCondenserEngine cce)
        {
            void OnFileApplied(string file)
            {
                lock (modifiedFiles)
                {
                    if (modifiedFiles.Add(file))
                    {
                        formatter.PrintFileApplied(output, file, baseDirectory);
                    }
                }
            }

            cce.FileApplied += OnFileApplied;
            try
            {
                await cce.ApplySummariesWithModifiedFilesAsync(result, settings, cancellationToken).ConfigureAwait(false);
                if (settings.EnableTypeSplitting)
                {
                    await cce.ApplyTypeSplitsWithModifiedFilesAsync(result, settings, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                cce.FileApplied -= OnFileApplied;
            }

            if (modifiedFiles.Count > 0)
            {
                await cce.UpdateSnapshotBaselineAsync(options.TargetPath, settings, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            await engine.ApplySummariesAsync(result, settings, cancellationToken).ConfigureAwait(false);
            if (settings.EnableTypeSplitting)
            {
                await engine.ApplyTypeSplitsAsync(result, settings, cancellationToken).ConfigureAwait(false);
            }
        }

        if (modifiedFiles.Count == 0)
        {
            formatter.PrintSuccess(output, "No source files required modification.");
        }
        else
        {
            output.WriteLine();
            formatter.PrintSuccess(output, $"Successfully updated {modifiedFiles.Count} modified source file(s).");
        }

        var mode = options.Incremental ? "Apply (Incremental)" : "Apply";
        var commentGainResult = result.CalculateCommentGain(estimator);
        int? commentGain = commentGainResult.SummarizedCount > 0 ? commentGainResult.GainCharacters : null;
        int? commentReduction = commentGainResult.SummarizedCount > 0 ? commentGainResult.ReductionPercent : null;
        int? commentGainTokens = commentGainResult.SummarizedCount > 0 ? commentGainResult.GainTokens : null;

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
            result.SubMethodCloneGroups.Count,
            commentGain,
            commentReduction,
            commentGainTokens,
            Pmad.CodeCondenser.Core.Clones.CloneGainEstimator.EstimateTotalGainTokens(result.CloneGroups, result.SubMethodCloneGroups, estimator, baseDirectory),
            result.RepeatedStringGroups.Count,
            null,
            null,
            result.TypeFileIssues.Count(i => i.HasViolation));

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

            var gatedRepeatedStrings = settings.EnableRepeatedStringDetection
                ? result.RepeatedStringGroups
                    .Where(g => g.Category != CloneCategory.Test || settings.EnforceTestDuplication)
                    .ToList()
                : [];

            bool repeatedStringViolation = settings.RepeatedStringTolerance.HasValue && gatedRepeatedStrings.Count > settings.RepeatedStringTolerance.Value;
            bool hasRepeatedStringIssue = settings.RepeatedStringTolerance.HasValue ? repeatedStringViolation : gatedRepeatedStrings.Count > 0;

            var gatedTypeIssues = settings.EnableTypeSplitting
                ? result.TypeFileIssues
                    .Where(i => i.HasViolation &&
                                (i.Category != FileCategory.Test || settings.EnforceTestDuplication) &&
                                File.Exists(Path.IsPathRooted(i.FilePath) ? i.FilePath : Path.Combine(baseDirectory, i.FilePath)) &&
                                !modifiedFiles.Contains(Path.GetFullPath(Path.IsPathRooted(i.FilePath) ? i.FilePath : Path.Combine(baseDirectory, i.FilePath))))
                    .ToList()
                : [];

            bool typeViolation = settings.TypeTolerance.HasValue && gatedTypeIssues.Count > settings.TypeTolerance.Value;
            bool hasTypeIssue = settings.TypeTolerance.HasValue ? typeViolation : gatedTypeIssues.Count > 0;

            if (duplicationViolation || hasCommentIssue || hasRepeatedStringIssue || hasTypeIssue)
            {
                formatter.PrintAiActionableIncrementalFeedback(
                    output,
                    gatedClones,
                    gatedSubMethodClones,
                    newVerboseComments,
                    baseDirectory,
                    settings.DuplicationTolerance,
                    hasDuplicationViolation: duplicationViolation,
                    hasCommentViolation: commentViolation,
                    repeatedStrings: gatedRepeatedStrings,
                    repeatedStringTolerance: settings.RepeatedStringTolerance,
                    hasRepeatedStringViolation: repeatedStringViolation,
                    typeIssues: gatedTypeIssues,
                    typeTolerance: settings.TypeTolerance,
                    hasTypeViolation: typeViolation);

                if (duplicationViolation)
                {
                    return settings.DuplicationExitCode;
                }

                if (commentViolation)
                {
                    return settings.CommentExitCode;
                }

                if (repeatedStringViolation)
                {
                    return settings.RepeatedStringExitCode;
                }

                if (typeViolation)
                {
                    return settings.TypeExitCode;
                }
            }
        }

        return ExitCodes.Success;
    }
}
