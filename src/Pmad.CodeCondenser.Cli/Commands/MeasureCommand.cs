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
