using Pmad.CodeCondenser.Cli.Formatting;
using Pmad.CodeCondenser.Cli.Formatting.Json;
using Pmad.CodeCondenser.Cli.Formatting.Sarif;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;
using Pmad.CodeCondenser.Core.Filtering;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Cli.Commands;

/// <summary>
/// Executes code clone analysis and verbose comment detection in dry-run mode (does not modify source files).
/// </summary>
public sealed class AnalyzeCommand : ICommand
{
    private readonly Func<OutputFormat, IReportFormatter>? _formatterFactory;

    public AnalyzeCommand(Func<OutputFormat, IReportFormatter>? formatterFactory = null)
    {
        _formatterFactory = formatterFactory;
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
        ArgumentNullException.ThrowIfNull(error);

        bool isMachineReadableStdout = options.Format != OutputFormat.Console && string.IsNullOrWhiteSpace(options.OutputFilePath);
        var statusWriter = isMachineReadableStdout ? error : output;

        formatter.PrintInfo(statusWriter, $"Analyzing target: {Path.GetFullPath(options.TargetPath)} (Mode: Dry-Run{(options.Incremental ? " Incremental" : "")})");
        statusWriter.WriteLine();

        var progress = new Progress<Pmad.CodeCondenser.Core.Comments.CommentSummarizationProgress>(p =>
        {
            formatter.PrintCommentProgress(statusWriter, p.Completed, p.Total);
        });

        if (engine is CodeCondenserEngine engineWithInfo)
        {
            engineWithInfo.InfoLogged += msg =>
            {
                var clean = msg.StartsWith("[Info] ", StringComparison.Ordinal) ? msg[7..] : msg;
                formatter.PrintInfo(statusWriter, clean);
                statusWriter.WriteLine();
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
            formatter.PrintInfo(statusWriter, $"Skipped {result.SkippedGeneratedFilesCount} auto-generated file(s).");
            statusWriter.WriteLine();
        }

        var baseDirectory = PathFilter.ResolveBaseDirectory(options.TargetPath);

        IReportFormatter reportFormatter = _formatterFactory?.Invoke(options.Format)
            ?? options.Format switch
            {
                OutputFormat.Sarif => new SarifReportFormatter(),
                OutputFormat.Json => new JsonReportFormatter(),
                _ => !string.IsNullOrWhiteSpace(options.OutputFilePath)
                    ? new ConsoleReportFormatter(new ConsoleFormatter(enableColor: false), isIncremental: options.Incremental)
                    : new ConsoleReportFormatter(formatter, isIncremental: options.Incremental)
            };

        if (!string.IsNullOrWhiteSpace(options.OutputFilePath))
        {
            var fullOutputPath = Path.GetFullPath(options.OutputFilePath);
            var parentDir = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            await using (var fileStream = new FileStream(fullOutputPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            await using (var fileWriter = new StreamWriter(fileStream, new System.Text.UTF8Encoding(false)))
            {
                await reportFormatter.FormatAsync(result, settings, fileWriter, baseDirectory, cancellationToken).ConfigureAwait(false);
            }

            formatter.PrintSuccess(statusWriter, $"Report written to: {fullOutputPath}");
            statusWriter.WriteLine();
        }
        else
        {
            await reportFormatter.FormatAsync(result, settings, output, baseDirectory, cancellationToken).ConfigureAwait(false);
        }

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
                    statusWriter,
                    gatedClones,
                    gatedSubMethodClones,
                    newVerboseComments,
                    options.TargetPath,
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

        if (options.UpdateSnapshot && engine is CodeCondenserEngine cce)
        {
            await cce.UpdateSnapshotBaselineAsync(options.TargetPath, settings, cancellationToken).ConfigureAwait(false);
            formatter.PrintSnapshotSaved(statusWriter, settings.SnapshotFilePath);
            statusWriter.WriteLine();
        }

        return ExitCodes.Success;
    }
}
