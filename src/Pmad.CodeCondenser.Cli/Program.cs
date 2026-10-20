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

        var formatter = customFormatter ?? new ConsoleFormatter(enableColor: !options.NoColor);
        formatter.PrintBanner(output);

        // Load configuration from codecondenser.json if present
        var settings = CodeCondenserConfigurationLoader.Load(options.ConfigPath ?? options.TargetPath);

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
        }

        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            settings.Ollama.Endpoint = options.Endpoint;
        }

        if (options.ContextSize.HasValue)
        {
            settings.Ollama.ContextSize = options.ContextSize.Value;
        }

        if (options.EnableCommentSummarization.HasValue)
        {
            settings.EnableCommentSummarization = options.EnableCommentSummarization.Value;
        }

        if (options.EnableDuplicationDetection.HasValue)
        {
            settings.EnableDuplicationDetection = options.EnableDuplicationDetection.Value;
        }

        if (options.DuplicationTolerance.HasValue)
        {
            settings.DuplicationTolerance = options.DuplicationTolerance.Value;
        }

        var engine = customEngine ?? new CodeCondenserEngine(
            snapshotStore: new JsonSnapshotStore(),
            commentSummarizer: new CommentSummarizer(new OllamaClientFactory()));

        ICommand command = options.Command switch
        {
            CliOptions.CommandApply => new ApplyCommand(),
            CliOptions.CommandSnapshot => new SnapshotCommand(),
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
            formatter.PrintWarning(output, "Operation was canceled.");
            return ExitCodes.Canceled;
        }
        catch (Exception ex)
        {
            formatter.PrintError(error, ex.Message);
            return ExitCodes.Error;
        }
    }
}
