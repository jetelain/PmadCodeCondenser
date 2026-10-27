using System.CommandLine;
using Pmad.CodeCondenser.Core.Configuration;

namespace Pmad.CodeCondenser.Cli.Commands;

/// <summary>
/// Configures the 'report' command that scans and reports codebase findings (clones, verbose comments, repeated strings) without invoking an LLM.
/// </summary>
public static class ReportCommand
{
    public static Command Create(GlobalOptions globals, CommandServices services)
    {
        ArgumentNullException.ThrowIfNull(globals);
        ArgumentNullException.ThrowIfNull(services);

        var pathArg = new Argument<string?>("path")
        {
            Description = "Target directory, solution, or C# file to report on (default: current directory)",
            Arity = ArgumentArity.ZeroOrOne
        };

        var cmd = new Command("report", "Report codebase status (clones, verbose comments, repeated strings, metrics) without calling an LLM");
        cmd.Add(pathArg);

        cmd.SetAction(async (parseResult, ct) =>
        {
            var path = parseResult.GetValue(pathArg) ?? ".";
            var options = globals.BindCliOptions(parseResult, CliOptions.CommandReport, path);
            options.SummaryMode = SummaryMode.Report;
            return await services.RunCommandAsync(new AnalyzeCommand(), options, ct).ConfigureAwait(false);
        });

        return cmd;
    }
}
