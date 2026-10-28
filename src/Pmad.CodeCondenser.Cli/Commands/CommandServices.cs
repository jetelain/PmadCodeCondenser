using Pmad.CodeCondenser.Cli.Formatting;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;
using Pmad.CodeCondenser.Infrastructure.Llm;
using Pmad.CodeCondenser.Infrastructure.Storage;

namespace Pmad.CodeCondenser.Cli.Commands;

/// <summary>
/// Provides execution context and dependency resolution for CLI commands.
/// </summary>
public sealed class CommandServices
{
    public TextWriter? Output { get; set; }
    public TextWriter? Error { get; set; }
    public ICodeCondenserEngine? CustomEngine { get; set; }
    public IConsoleFormatter? CustomFormatter { get; set; }

    public CommandServices(
        TextWriter? output = null,
        TextWriter? error = null,
        ICodeCondenserEngine? customEngine = null,
        IConsoleFormatter? customFormatter = null)
    {
        Output = output;
        Error = error;
        CustomEngine = customEngine;
        CustomFormatter = customFormatter;
    }

    public Task<int> RunCommandAsync(
        CliOptions options,
        ICommand command,
        CancellationToken cancellationToken = default)
    {
        return RunCommandAsync(command, options, cancellationToken);
    }

    public async Task<int> RunCommandAsync(
        ICommand command,
        CliOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(options);

        var output = Output ?? Console.Out;
        var error = Error ?? Console.Error;

        bool disableAnsi = options.NoColor
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"))
            || (output == Console.Out && Console.IsOutputRedirected)
            || (error == Console.Error && Console.IsErrorRedirected);

        bool isMachineReadableStdout = options.Format != OutputFormat.Console && string.IsNullOrWhiteSpace(options.OutputFilePath);
        var formatter = CustomFormatter ?? new ConsoleFormatter(enableColor: !disableAnsi);
        var bannerWriter = isMachineReadableStdout ? error : output;
        formatter.PrintBanner(bannerWriter);

        if (!string.IsNullOrWhiteSpace(options.ConfigPath) && !File.Exists(options.ConfigPath))
        {
            formatter.PrintError(error, $"Configuration file not found: '{options.ConfigPath}'");
            return ExitCodes.Error;
        }

        if (!string.IsNullOrWhiteSpace(options.GlobalConfigPath) && !File.Exists(options.GlobalConfigPath) && !Directory.Exists(options.GlobalConfigPath))
        {
            formatter.PrintError(error, $"Global configuration path not found: '{options.GlobalConfigPath}'");
            return ExitCodes.Error;
        }

        // Load configuration from global user profile and codecondenser.json if present
        var settings = CodeCondenserConfigurationLoader.Load(
            options.ConfigPath ?? options.TargetPath,
            options.GlobalConfigPath,
            options.NoGlobalConfig);

        // Apply CLI overrides
        if (options.ThresholdStatements.HasValue)
        {
            settings.DuplicationStatementThreshold = options.ThresholdStatements.Value;
        }

        if (options.ThresholdComments.HasValue)
        {
            settings.CommentLengthThreshold = options.ThresholdComments.Value;
        }

        if (options.ThresholdXmlSummary.HasValue)
        {
            settings.XmlSummaryLengthThreshold = options.ThresholdXmlSummary.Value;
        }

        if (options.ThresholdXmlParam.HasValue)
        {
            settings.XmlParamLengthThreshold = options.ThresholdXmlParam.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.Model))
        {
            settings.Ollama.Model = options.Model;
            settings.IsLlmConfigured = true;
            settings.IsLlmConfiguredGlobally = false;
        }

        if (options.MaxSummarizationRetries.HasValue)
        {
            settings.MaxSummarizationRetries = options.MaxSummarizationRetries.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.TokenEstimator))
        {
            settings.TokenEstimator = options.TokenEstimator;
        }

        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            settings.Ollama.Endpoint = options.Endpoint;
            settings.IsLlmConfigured = true;
            settings.IsLlmConfiguredGlobally = false;
        }

        if (options.ContextSize.HasValue)
        {
            settings.Ollama.ContextSize = options.ContextSize.Value;
            settings.IsLlmConfigured = true;
            settings.IsLlmConfiguredGlobally = false;
        }

        if (options.Apply || options.Command == CliOptions.CommandApply)
        {
            settings.IsLlmConfigured = true;
            settings.IsLlmConfiguredGlobally = false;
        }
        else if (CustomEngine != null && !settings.IsLlmConfigured)
        {
            settings.IsLlmConfigured = true;
        }

        if (options.SummaryMode.HasValue)
        {
            settings.SummaryMode = options.SummaryMode.Value;
            if (options.SummaryMode.Value == SummaryMode.Disabled)
            {
                settings.EnableCommentSummarization = false;
            }
        }
        else if (options.EnableCommentSummarization.HasValue)
        {
            settings.EnableCommentSummarization = options.EnableCommentSummarization.Value;
            if (!options.EnableCommentSummarization.Value)
            {
                settings.SummaryMode = SummaryMode.Disabled;
            }
        }

        if (options.EnableCommentSummarization == false)
        {
            settings.EnableCommentSummarization = false;
            settings.SummaryMode = SummaryMode.Disabled;
        }

        settings.SummaryMode = settings.GetEffectiveSummaryMode();

        if (options.EnableDuplicationDetection.HasValue)
        {
            settings.EnableDuplicationDetection = options.EnableDuplicationDetection.Value;
        }

        if (options.DuplicationTolerance.HasValue)
        {
            settings.DuplicationTolerance = options.DuplicationTolerance.Value;
        }

        if (options.CommentTolerance.HasValue)
        {
            settings.CommentTolerance = options.CommentTolerance.Value;
        }

        if (options.EnableRepeatedStrings.HasValue)
        {
            settings.EnableRepeatedStringDetection = options.EnableRepeatedStrings.Value;
        }

        if (options.ThresholdRepeatedStrings.HasValue)
        {
            settings.RepeatedStringLengthThreshold = options.ThresholdRepeatedStrings.Value;
        }

        if (options.ThresholdExistingConstants.HasValue)
        {
            settings.ExistingConstantStringLengthThreshold = options.ThresholdExistingConstants.Value;
        }

        if (options.ThresholdStringOccurrences.HasValue)
        {
            settings.RepeatedStringMinOccurrences = options.ThresholdStringOccurrences.Value;
        }

        if (options.RepeatedStringTolerance.HasValue)
        {
            settings.RepeatedStringTolerance = options.RepeatedStringTolerance.Value;
        }

        if (options.EnableTypeSplitting.HasValue)
        {
            settings.EnableTypeSplitting = options.EnableTypeSplitting.Value;
        }

        if (options.EnableTypeRenaming.HasValue)
        {
            settings.EnableTypeRenaming = options.EnableTypeRenaming.Value;
        }

        if (options.TypeLineTolerance.HasValue)
        {
            settings.TypeLineTolerance = options.TypeLineTolerance.Value;
        }

        if (options.TypeTolerance.HasValue)
        {
            settings.TypeTolerance = options.TypeTolerance.Value;
        }

        if (options.EnableLinesOfCodeDetection.HasValue)
        {
            settings.EnableLinesOfCodeDetection = options.EnableLinesOfCodeDetection.Value;
        }

        if (options.MethodLinesOfCodeThreshold.HasValue)
        {
            settings.MethodLinesOfCodeThreshold = options.MethodLinesOfCodeThreshold.Value;
        }

        if (options.ClassLinesOfCodeThreshold.HasValue)
        {
            settings.ClassLinesOfCodeThreshold = options.ClassLinesOfCodeThreshold.Value;
        }

        if (options.LinesOfCodeTolerance.HasValue)
        {
            settings.LinesOfCodeTolerance = options.LinesOfCodeTolerance.Value;
        }

        if (options.EnablePredicateCloneDetection.HasValue)
        {
            settings.EnablePredicateCloneDetection = options.EnablePredicateCloneDetection.Value;
        }

        if (options.PredicateLengthThreshold.HasValue)
        {
            settings.PredicateLengthThreshold = options.PredicateLengthThreshold.Value;
        }

        if (options.ExistingHelperPredicateLengthThreshold.HasValue)
        {
            settings.ExistingHelperPredicateLengthThreshold = options.ExistingHelperPredicateLengthThreshold.Value;
        }

        if (options.PredicateMinOccurrences.HasValue)
        {
            settings.PredicateMinOccurrences = options.PredicateMinOccurrences.Value;
        }

        if (options.PredicateTolerance.HasValue)
        {
            settings.PredicateTolerance = options.PredicateTolerance.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.SummarizedCommentsPath))
        {
            settings.SummarizedCommentsFilePath = options.SummarizedCommentsPath;
        }

        if (options.MaxLineLength.HasValue)
        {
            settings.MaxCommentLineLength = options.MaxLineLength.Value;
        }

        if (options.Include.Count > 0)
        {
            foreach (var inc in options.Include)
            {
                if (!settings.Include.Contains(inc, StringComparer.Ordinal))
                {
                    settings.Include.Add(inc);
                }
            }
        }

        if (options.Exclude.Count > 0)
        {
            foreach (var exc in options.Exclude)
            {
                if (!settings.Exclude.Contains(exc, StringComparer.Ordinal))
                {
                    settings.Exclude.Add(exc);
                }
            }
        }

        if (options.ExcludeTests)
        {
            settings.ExcludeTests = true;
        }

        if (options.EnforceTestDuplication)
        {
            settings.EnforceTestDuplication = true;
        }

        if (options.IncludeGenerated)
        {
            settings.ExcludeGeneratedCode = false;
        }

        if (options.SubMethodClones)
        {
            settings.EnableSubMethodCloneDetection = true;
        }

        if (options.ThresholdSubMethodStatements.HasValue)
        {
            settings.SubMethodStatementThreshold = options.ThresholdSubMethodStatements.Value;
        }

        var engine = CustomEngine ?? new CodeCondenserEngine(
            snapshotStore: new JsonSnapshotStore(),
            summarizedCommentsStore: new JsonSummarizedCommentsStore(),
            commentSummarizer: new CommentSummarizer(new OllamaClientFactory()),
            subMethodCacheStore: new JsonSubMethodCacheStore());

        try
        {
            return await command.ExecuteAsync(
                options,
                settings,
                engine,
                formatter,
                output,
                error,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var cancelWriter = isMachineReadableStdout ? error : output;
            formatter.PrintWarning(cancelWriter, "Operation was canceled.");
            return ExitCodes.Canceled;
        }
        catch (Exception ex)
        {
            formatter.PrintError(error, ex.Message);
            return ExitCodes.Error;
        }
    }
}
