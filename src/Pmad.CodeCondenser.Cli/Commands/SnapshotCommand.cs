using Pmad.CodeCondenser.Cli.Formatting;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;

namespace Pmad.CodeCondenser.Cli.Commands;

/// <summary>
/// Refreshes or creates the snapshot baseline for incremental analysis.
/// </summary>
public sealed class SnapshotCommand : ICommand
{
    public static System.CommandLine.Command Create(GlobalOptions globals, CommandServices services)
    {
        ArgumentNullException.ThrowIfNull(globals);
        ArgumentNullException.ThrowIfNull(services);

        var pathArg = new System.CommandLine.Argument<string?>("path")
        {
            Description = "Target directory, solution, or C# file to snapshot (default: current directory)",
            Arity = System.CommandLine.ArgumentArity.ZeroOrOne
        };

        var cmd = new System.CommandLine.Command("snapshot", "Create or refresh the snapshot baseline");
        cmd.Add(pathArg);

        cmd.SetAction(async (parseResult, ct) =>
        {
            var path = parseResult.GetValue(pathArg) ?? ".";
            var options = globals.BindCliOptions(parseResult, CliOptions.CommandSnapshot, path);
            options.UpdateSnapshot = true;
            return await services.RunCommandAsync(new SnapshotCommand(), options, ct).ConfigureAwait(false);
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

        formatter.PrintInfo(output, $"Refreshing snapshot baseline for: {Path.GetFullPath(options.TargetPath)}");

        if (engine is CodeCondenserEngine cce)
        {
            var snapshot = await cce.UpdateSnapshotBaselineAsync(
                options.TargetPath,
                settings,
                cancellationToken).ConfigureAwait(false);

            formatter.PrintSnapshotSaved(output, settings.SnapshotFilePath);
            if (snapshot != null)
            {
                formatter.PrintSuccess(output, $"Snapshot baseline updated: {snapshot.Files.Count} file(s), {snapshot.MethodHashes.Count} method hash(es).");
            }
        }
        else
        {
            await engine.AnalyzeAsync(options.TargetPath, settings, incremental: false, cancellationToken: cancellationToken).ConfigureAwait(false);
            formatter.PrintSnapshotSaved(output, settings.SnapshotFilePath);
        }

        return 0;
    }
}
