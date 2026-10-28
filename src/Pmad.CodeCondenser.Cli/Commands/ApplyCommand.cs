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
        var displayed = DisplayedItemLimiter.SelectItems(result, settings, baseDirectory, estimator, includeComments: false);

        bool isDuplicationAnalyzed = settings.EnableDuplicationDetection;
        bool isSubMethodAnalyzed = settings.EnableSubMethodCloneDetection;
        bool isStringsAnalyzed = settings.EnableRepeatedStringDetection;
        bool isTypeAnalyzed = settings.EnableTypeSplitting;
        bool isLocAnalyzed = settings.EnableLinesOfCodeDetection;
        bool isPredicatesAnalyzed = settings.EnablePredicateCloneDetection;
        bool isPatternsAnalyzed = settings.EnablePatternCondensation;
        bool isCommentsAnalyzed = settings.GetEffectiveSummaryMode() != SummaryMode.Disabled && settings.EnableCommentSummarization;

        if (isDuplicationAnalyzed)
        {
            int totalProd = result.CloneGroups.Count(g => g.Category != CloneCategory.Test);
            int totalTest = result.CloneGroups.Count(g => g.Category == CloneCategory.Test);
            formatter.PrintCloneGroups(
                output,
                displayed.CloneGroups,
                baseDirectory,
                estimator,
                totalCount: result.CloneGroups.Count,
                totalProductionCount: totalProd,
                totalTestCount: totalTest);
        }

        if (isSubMethodAnalyzed && displayed.SubMethodCloneGroups.Count > 0)
        {
            formatter.PrintSubMethodCloneGroups(
                output,
                displayed.SubMethodCloneGroups,
                baseDirectory,
                estimator,
                totalCount: result.SubMethodCloneGroups.Count);
        }

        if (isStringsAnalyzed && displayed.RepeatedStringGroups.Count > 0)
        {
            int totalProd = result.RepeatedStringGroups.Count(g => g.Category != CloneCategory.Test);
            int totalTest = result.RepeatedStringGroups.Count(g => g.Category == CloneCategory.Test);
            formatter.PrintRepeatedStringGroups(
                output,
                displayed.RepeatedStringGroups,
                baseDirectory,
                estimator,
                totalCount: result.RepeatedStringGroups.Count,
                totalProductionCount: totalProd,
                totalTestCount: totalTest);
        }

        if (isTypeAnalyzed && displayed.TypeFileIssues.Count > 0)
        {
            formatter.PrintTypeFileIssues(
                output,
                displayed.TypeFileIssues,
                baseDirectory,
                settings.TypeLineTolerance,
                totalCount: result.TypeFileIssues.Count(i => i.HasViolation));
        }

        if (isLocAnalyzed && displayed.LinesOfCodeViolations.Count > 0)
        {
            formatter.PrintLinesOfCodeViolations(
                output,
                displayed.LinesOfCodeViolations,
                baseDirectory,
                totalCount: result.LinesOfCodeViolations.Count);
        }

        if (isPredicatesAnalyzed && displayed.PredicateCloneGroups.Count > 0)
        {
            int totalProd = result.PredicateCloneGroups.Count(g => g.Category != CloneCategory.Test);
            int totalTest = result.PredicateCloneGroups.Count(g => g.Category == CloneCategory.Test);
            formatter.PrintPredicateCloneGroups(
                output,
                displayed.PredicateCloneGroups,
                baseDirectory,
                estimator,
                totalCount: result.PredicateCloneGroups.Count,
                totalProductionCount: totalProd,
                totalTestCount: totalTest);
        }

        if (isPatternsAnalyzed && displayed.CondensationCandidates.Count > 0)
        {
            formatter.PrintCondensationCandidates(
                output,
                displayed.CondensationCandidates,
                baseDirectory,
                estimator,
                totalCount: result.CondensationCandidates.Count);
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
                if (settings.EnablePatternCondensation)
                {
                    await cce.ApplyPatternsAsync(result, settings, cancellationToken).ConfigureAwait(false);
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
            if (settings.EnablePatternCondensation)
            {
                await engine.ApplyPatternsAsync(result, settings, cancellationToken).ConfigureAwait(false);
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
        var commentGainResult = isCommentsAnalyzed ? result.CalculateCommentGain(estimator) : null;
        int? commentGain = (commentGainResult != null && commentGainResult.SummarizedCount > 0) ? commentGainResult.GainCharacters : null;
        int? commentReduction = (commentGainResult != null && commentGainResult.SummarizedCount > 0) ? commentGainResult.ReductionPercent : null;
        int? commentGainTokens = (commentGainResult != null && commentGainResult.SummarizedCount > 0) ? commentGainResult.GainTokens : null;

        int? methodsScanned = isDuplicationAnalyzed ? result.TotalMethodsScanned : null;
        int? cloneGroupsCount = isDuplicationAnalyzed ? result.CloneGroups.Count : null;
        int? prodClones = isDuplicationAnalyzed ? result.CloneGroups.Count(g => g.Category != CloneCategory.Test) : null;
        int? testClones = isDuplicationAnalyzed ? result.CloneGroups.Count(g => g.Category == CloneCategory.Test) : null;
        int? subMethodClones = isSubMethodAnalyzed ? result.SubMethodCloneGroups.Count : null;
        int? cloneGainTokens = (isDuplicationAnalyzed || isSubMethodAnalyzed) ? Pmad.CodeCondenser.Core.Clones.CloneGainEstimator.EstimateTotalGainTokens(result.CloneGroups, result.SubMethodCloneGroups, estimator, baseDirectory) : null;

        int? repeatedStringsCount = isStringsAnalyzed ? result.RepeatedStringGroups.Count : null;
        int? repeatedStringGainChars = (isStringsAnalyzed && result.RepeatedStringGroups.Count > 0) ? result.TotalRepeatedStringGainCharacters : null;
        int? repeatedStringGainTokens = (isStringsAnalyzed && result.RepeatedStringGroups.Count > 0) ? result.TotalRepeatedStringGainTokens : null;
        int? typeIssuesCount = isTypeAnalyzed ? result.TypeFileIssues.Count(i => i.HasViolation) : null;
        int? linesOfCodeViolationsCount = isLocAnalyzed ? result.LinesOfCodeViolations.Count : null;
        int? predicateGroupsCount = isPredicatesAnalyzed ? result.PredicateCloneGroups.Count : null;
        int? predicateGainChars = (isPredicatesAnalyzed && result.PredicateCloneGroups.Count > 0) ? result.TotalPredicateGainCharacters : null;
        int? predicateGainTokens = (isPredicatesAnalyzed && result.PredicateCloneGroups.Count > 0) ? result.TotalPredicateGainTokens : null;

        int? patternsCount = isPatternsAnalyzed ? result.CondensationCandidates.Count : null;
        int? patternGainChars = (isPatternsAnalyzed && result.CondensationCandidates.Count > 0) ? result.TotalPatternGainCharacters : null;
        int? patternGainTokens = (isPatternsAnalyzed && result.CondensationCandidates.Count > 0) ? result.TotalPatternGainTokens : null;
        int? verboseCommentsCount = isCommentsAnalyzed ? result.VerboseComments.Count : null;

        formatter.PrintSummary(
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
            predicateGainTokens: predicateGainTokens,
            modernizationsCount: patternsCount,
            modernizationGainChars: patternGainChars,
            modernizationGainTokens: patternGainTokens);

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

        return ExitCodes.Success;
    }
}
