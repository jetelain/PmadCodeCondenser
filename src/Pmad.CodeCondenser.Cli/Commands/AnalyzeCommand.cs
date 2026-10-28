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

    public static System.CommandLine.Command Create(GlobalOptions globals, CommandServices services)
    {
        ArgumentNullException.ThrowIfNull(globals);
        ArgumentNullException.ThrowIfNull(services);

        var pathArg = new System.CommandLine.Argument<string?>("path")
        {
            Description = "Target directory, solution, or C# file to analyze (default: current directory)",
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne
        };

        var summaryOpt = new System.CommandLine.Option<string?>("--summary", "--summary-mode")
        {
            Description = "Comment summary mode: summarize (default), report, or disabled"
        };

        var cmd = new System.CommandLine.Command("analyze", "Analyze C# codebase and summarize verbose comments via LLM in dry-run mode");
        cmd.Add(pathArg);
        cmd.Add(summaryOpt);

        cmd.SetAction(async (parseResult, ct) =>
        {
            var path = parseResult.GetValue(pathArg) ?? ".";
            var options = globals.BindCliOptions(parseResult, CliOptions.CommandAnalyze, path);

            var summary = parseResult.GetValue(summaryOpt);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                if (summary.Equals("disabled", StringComparison.OrdinalIgnoreCase) ||
                    summary.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                    summary.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                    summary.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    options.SummaryMode = SummaryMode.Disabled;
                    options.EnableCommentSummarization = false;
                }
                else if (summary.Equals("report", StringComparison.OrdinalIgnoreCase) ||
                         summary.Equals("report-only", StringComparison.OrdinalIgnoreCase))
                {
                    options.SummaryMode = SummaryMode.Report;
                }
                else if (summary.Equals("summarize", StringComparison.OrdinalIgnoreCase) ||
                         summary.Equals("condense", StringComparison.OrdinalIgnoreCase) ||
                         summary.Equals("llm", StringComparison.OrdinalIgnoreCase) ||
                         summary.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    options.SummaryMode = SummaryMode.Summarize;
                    options.EnableCommentSummarization = true;
                }
            }

            return await services.RunCommandAsync(new AnalyzeCommand(), options, ct).ConfigureAwait(false);
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

            var gatedRepeatedStrings = settings.EnableRepeatedStringDetection
                ? result.RepeatedStringGroups
                    .Where(g => g.Category != CloneCategory.Test || settings.EnforceTestDuplication)
                    .ToList()
                : [];

            bool repeatedStringViolation = settings.RepeatedStringTolerance.HasValue && gatedRepeatedStrings.Count > settings.RepeatedStringTolerance.Value;
            bool hasRepeatedStringIssue = settings.RepeatedStringTolerance.HasValue ? repeatedStringViolation : gatedRepeatedStrings.Count > 0;

            var gatedTypeIssues = settings.EnableTypeSplitting
                ? result.TypeFileIssues
                    .Where(i => i.HasViolation && (i.Category != FileCategory.Test || settings.EnforceTestDuplication))
                    .ToList()
                : [];

            bool typeViolation = settings.TypeTolerance.HasValue && gatedTypeIssues.Count > settings.TypeTolerance.Value;
            bool hasTypeIssue = settings.TypeTolerance.HasValue ? typeViolation : gatedTypeIssues.Count > 0;

            var gatedLocViolations = settings.EnableLinesOfCodeDetection
                ? result.LinesOfCodeViolations
                    .Where(v => v.Category != FileCategory.Test || settings.EnforceTestDuplication)
                    .ToList()
                : [];

            bool locViolation = settings.LinesOfCodeTolerance.HasValue && gatedLocViolations.Count > settings.LinesOfCodeTolerance.Value;
            bool hasLocIssue = settings.LinesOfCodeTolerance.HasValue ? locViolation : gatedLocViolations.Count > 0;

            var gatedPredicates = settings.EnablePredicateCloneDetection
                ? result.PredicateCloneGroups
                    .Where(g => g.Category != CloneCategory.Test || settings.EnforceTestDuplication)
                    .ToList()
                : [];

            bool predicateViolation = settings.PredicateTolerance.HasValue && gatedPredicates.Count > settings.PredicateTolerance.Value;
            bool hasPredicateIssue = settings.PredicateTolerance.HasValue ? predicateViolation : gatedPredicates.Count > 0;

            if (duplicationViolation || hasCommentIssue || hasRepeatedStringIssue || hasTypeIssue || hasLocIssue || hasPredicateIssue)
            {
                formatter.PrintAiActionableIncrementalFeedback(
                    statusWriter,
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
                    hasTypeViolation: typeViolation,
                    linesOfCodeViolations: gatedLocViolations,
                    linesOfCodeTolerance: settings.LinesOfCodeTolerance,
                    hasLinesOfCodeViolation: locViolation,
                    predicateGroups: gatedPredicates,
                    predicateTolerance: settings.PredicateTolerance,
                    hasPredicateViolation: predicateViolation);

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

                if (locViolation)
                {
                    return settings.LinesOfCodeExitCode;
                }

                if (predicateViolation)
                {
                    return settings.PredicateExitCode;
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
