using System.CommandLine;
using System.Reflection;
using Pmad.CodeCondenser.Cli.Commands;
using Pmad.CodeCondenser.Cli.Formatting;
using Pmad.CodeCondenser.Core.Engine;

namespace Pmad.CodeCondenser.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        return await RunAsync(args, Console.Out, Console.Error).ConfigureAwait(false);
    }

    public static RootCommand BuildRootCommand(
        TextWriter? output = null,
        TextWriter? error = null,
        ICodeCondenserEngine? customEngine = null,
        IConsoleFormatter? customFormatter = null)
    {
        var services = new CommandServices(output, error, customEngine, customFormatter);
        var globals = new GlobalOptions();

        var root = new RootCommand("codecondenser: Roslyn-based static analysis CLI tool to detect C# code clones and condense verbose comments via local LLMs");
        globals.AddTo(root);

        root.Add(ReportCommand.Create(globals, services));
        root.Add(AnalyzeCommand.Create(globals, services));
        root.Add(ApplyCommand.Create(globals, services));
        root.Add(SnapshotCommand.Create(globals, services));
        root.Add(MeasureCommand.Create(globals, services));
        root.Add(SkillsCommand.Create(globals, services));

        var versionOpt = root.Options.OfType<VersionOption>().FirstOrDefault();
        versionOpt?.Aliases.Add("-v");

        return root;
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

        var root = BuildRootCommand(output, error, customEngine, customFormatter);
        var invocationConfig = new InvocationConfiguration
        {
            Output = output,
            Error = error
        };

        var parseResult = root.Parse(args);
        if (parseResult.Action != null && parseResult.Action.GetType().Name.Contains("Version", StringComparison.OrdinalIgnoreCase))
        {
            var assembly = typeof(Program).Assembly;
            var infoVersion = assembly.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var version = assembly.GetName().Version?.ToString(3) ?? "0.1.0-preview";
            var versionText = infoVersion?.Split('+')[0] ?? version;
            output.WriteLine(versionText);
            return 0;
        }

        return await parseResult.InvokeAsync(invocationConfig, cancellationToken).ConfigureAwait(false);
    }
}
