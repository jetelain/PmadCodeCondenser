using Pmad.CodeCondenser.Cli.Commands;
using Pmad.CodeCondenser.Cli.Formatting;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;
using Pmad.CodeCondenser.Infrastructure.Llm;
using Pmad.CodeCondenser.Infrastructure.Storage;

namespace Pmad.CodeCondenser.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        return await RunAsync(args, Console.Out, Console.Error).ConfigureAwait(false);
    }

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter? output = null,
        TextWriter? error = null,
        ICodeCondenserEngine? customEngine = null,
        IConsoleFormatter? customFormatter = null,
        CancellationToken cancellationToken = default)
    {
        output ??= Console.Out;
        error ??= Console.Error;

        if (!CliArgumentParser.TryParse(args, out var options, out var parseError))
        {
            error.WriteLine($"Error: {parseError}");
            return 1;
        }

        if (options.Help)
        {
            output.WriteLine(CliArgumentParser.GetHelpText());
            return 0;
        }

        if (options.Version)
        {
            output.WriteLine(CliArgumentParser.GetVersionText());
            return 0;
        }

        bool disableAnsi = options.NoColor
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"))
            || (output == Console.Out && Console.IsOutputRedirected)
            || (error == Console.Error && Console.IsErrorRedirected);

        bool isMachineReadableStdout = options.Format != OutputFormat.Console && string.IsNullOrWhiteSpace(options.OutputFilePath);
        var formatter = customFormatter ?? new ConsoleFormatter(enableColor: !disableAnsi);
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
        else if (customEngine != null && !settings.IsLlmConfigured)
        {
            settings.IsLlmConfigured = true;
        }

        if (options.SummaryMode.HasValue)
        {
            settings.SummaryMode = options.SummaryMode.Value;
        }
        else if (options.EnableCommentSummarization.HasValue)
        {
            settings.EnableCommentSummarization = options.EnableCommentSummarization.Value;
            if (!options.EnableCommentSummarization.Value)
            {
                settings.SummaryMode = SummaryMode.Disabled;
            }
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

        var engine = customEngine ?? new CodeCondenserEngine(
            snapshotStore: new JsonSnapshotStore(),
            summarizedCommentsStore: new JsonSummarizedCommentsStore(),
            commentSummarizer: new CommentSummarizer(new OllamaClientFactory()),
            subMethodCacheStore: new JsonSubMethodCacheStore());

        ICommand command = options.Command switch
        {
            CliOptions.CommandApply => new ApplyCommand(),
            CliOptions.CommandSnapshot => new SnapshotCommand(),
            CliOptions.CommandSkills => new SkillsCommand(),
            CliOptions.CommandMeasure => new MeasureCommand(),
            _ => new AnalyzeCommand()
        };

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
