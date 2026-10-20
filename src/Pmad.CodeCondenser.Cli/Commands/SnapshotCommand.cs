using Pmad.CodeCondenser.Cli.Formatting;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;

namespace Pmad.CodeCondenser.Cli.Commands;

/// <summary>
/// Refreshes or creates the snapshot baseline for incremental analysis.
/// </summary>
public sealed class SnapshotCommand : ICommand
{
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
