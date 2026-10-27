using System.CommandLine;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pmad.CodeCondenser.Cli.Formatting;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;

namespace Pmad.CodeCondenser.Cli.Commands;

/// <summary>
/// Measures codebase tokens in terms of LLM tokens (total, comments, code) per category (production, tests).
/// </summary>
public sealed class MeasureCommand : ICommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static Command Create(GlobalOptions globals, CommandServices services)
    {
        ArgumentNullException.ThrowIfNull(globals);
        ArgumentNullException.ThrowIfNull(services);

        var pathArg = new Argument<string?>("path")
        {
            Description = "Target directory, solution, or C# file to measure (default: current directory)",
            Arity = ArgumentArity.ZeroOrOne
        };

        var cmd = new Command("measure", "Measure codebase tokens in terms of LLM tokens (total, comments, code) per category");
        cmd.Add(pathArg);

        cmd.SetAction(async (parseResult, ct) =>
        {
            var path = parseResult.GetValue(pathArg) ?? ".";
            var options = globals.BindCliOptions(parseResult, CliOptions.CommandMeasure, path);

            return await services.RunCommandAsync(options, new MeasureCommand(), ct).ConfigureAwait(false);
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

        if (options.Format == OutputFormat.Sarif)
        {
            formatter.PrintError(error, "SARIF report format is only supported for code analysis ('analyze'), not for token measurement. Use '--format json' or console format.");
            return ExitCodes.Error;
        }

        bool isMachineReadableStdout = options.Format != OutputFormat.Console && string.IsNullOrWhiteSpace(options.OutputFilePath);
        var statusWriter = isMachineReadableStdout ? error : output;

        formatter.PrintInfo(statusWriter, $"Measuring code tokens for: {Path.GetFullPath(options.TargetPath)}");
        statusWriter.WriteLine();

        var result = await engine.MeasureAsync(
            options.TargetPath,
            settings,
            cancellationToken).ConfigureAwait(false);

        if (result.SkippedGeneratedFilesCount > 0)
        {
            formatter.PrintInfo(statusWriter, $"Skipped {result.SkippedGeneratedFilesCount} auto-generated file(s).");
            statusWriter.WriteLine();
        }

        if (options.Format == OutputFormat.Json)
        {
            var json = JsonSerializer.Serialize(result, JsonOptions);

            if (!string.IsNullOrWhiteSpace(options.OutputFilePath))
            {
                var fullOutputPath = Path.GetFullPath(options.OutputFilePath);
                var parentDir = Path.GetDirectoryName(fullOutputPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                await File.WriteAllTextAsync(fullOutputPath, json, cancellationToken).ConfigureAwait(false);
                formatter.PrintSuccess(statusWriter, $"Measurement report written to: {fullOutputPath}");
                statusWriter.WriteLine();
            }
            else
            {
                await output.WriteLineAsync(json).ConfigureAwait(false);
            }
        }
        else
        {
            var estimatorName = !string.IsNullOrWhiteSpace(settings.TokenEstimator)
                ? settings.TokenEstimator
                : "default";

            if (!string.IsNullOrWhiteSpace(options.OutputFilePath))
            {
                var fullOutputPath = Path.GetFullPath(options.OutputFilePath);
                var parentDir = Path.GetDirectoryName(fullOutputPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                await using var stream = new FileStream(fullOutputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                await using var fileWriter = new StreamWriter(stream, new UTF8Encoding(false));
                var fileFormatter = new ConsoleFormatter(enableColor: false);
                fileFormatter.PrintMeasurementResult(fileWriter, result, options.TargetPath, estimatorName);

                formatter.PrintSuccess(statusWriter, $"Measurement report written to: {fullOutputPath}");
                statusWriter.WriteLine();
            }
            else
            {
                formatter.PrintMeasurementResult(output, result, options.TargetPath, estimatorName);
            }
        }

        return ExitCodes.Success;
    }
}
