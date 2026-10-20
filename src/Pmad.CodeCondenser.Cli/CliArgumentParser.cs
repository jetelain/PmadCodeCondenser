using System.Reflection;

namespace Pmad.CodeCondenser.Cli;

/// <summary>
/// Lightweight, robust argument parser for pmad-codecondenser command line options.
/// </summary>
public static class CliArgumentParser
{
    public static bool TryParse(string[] args, out CliOptions options, out string? errorMessage)
    {
        options = new CliOptions();
        errorMessage = null;

        var positionalArgs = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-?", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "/?", StringComparison.OrdinalIgnoreCase))
            {
                options.Help = true;
                return true;
            }

            if (string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-v", StringComparison.OrdinalIgnoreCase))
            {
                options.Version = true;
                return true;
            }

            if (string.Equals(arg, "--apply", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-a", StringComparison.OrdinalIgnoreCase))
            {
                options.Apply = true;
                continue;
            }

            if (string.Equals(arg, "--incremental", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-i", StringComparison.OrdinalIgnoreCase))
            {
                options.Incremental = true;
                continue;
            }

            if (string.Equals(arg, "--update-snapshot", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-u", StringComparison.OrdinalIgnoreCase))
            {
                options.UpdateSnapshot = true;
                continue;
            }

            if (string.Equals(arg, "--no-color", StringComparison.OrdinalIgnoreCase))
            {
                options.NoColor = true;
                continue;
            }

            // Options with values: check both "--option value" and "--option=value"
            if (TryExtractOptionValue(args, ref i, "--threshold-statements", "-ts", out var tsVal, out errorMessage))
            {
                if (errorMessage != null) return false;
                if (!int.TryParse(tsVal, out var ts) || ts < 1)
                {
                    errorMessage = $"Invalid value for --threshold-statements: '{tsVal}'. Must be a positive integer.";
                    return false;
                }
                options.ThresholdStatements = ts;
                continue;
            }

            if (TryExtractOptionValue(args, ref i, "--threshold-comments", "-tc", out var tcVal, out errorMessage))
            {
                if (errorMessage != null) return false;
                if (!int.TryParse(tcVal, out var tc) || tc < 1)
                {
                    errorMessage = $"Invalid value for --threshold-comments: '{tcVal}'. Must be a positive integer.";
                    return false;
                }
                options.ThresholdComments = tc;
                continue;
            }

            if (TryExtractOptionValue(args, ref i, "--model", "-m", out var modelVal, out errorMessage))
            {
                if (errorMessage != null) return false;
                options.Model = modelVal;
                continue;
            }

            if (TryExtractOptionValue(args, ref i, "--endpoint", "-e", out var endpointVal, out errorMessage))
            {
                if (errorMessage != null) return false;
                options.Endpoint = endpointVal;
                continue;
            }

            if (TryExtractOptionValue(args, ref i, "--config", "-c", out var configVal, out errorMessage))
            {
                if (errorMessage != null) return false;
                options.ConfigPath = configVal;
                continue;
            }

            if (arg.StartsWith('-'))
            {
                errorMessage = $"Unrecognized option: '{arg}'. Use --help to view available options.";
                return false;
            }

            positionalArgs.Add(arg);
        }

        // Process positional arguments
        int positionalIdx = 0;
        if (positionalArgs.Count > 0)
        {
            var first = positionalArgs[0].ToLowerInvariant();
            if (first is CliOptions.CommandAnalyze or CliOptions.CommandApply or CliOptions.CommandSnapshot)
            {
                options.Command = first;
                positionalIdx = 1;
            }
        }

        if (positionalIdx < positionalArgs.Count)
        {
            options.TargetPath = positionalArgs[positionalIdx];
        }

        // Flags override command verbs if explicitly set
        if (options.Apply)
        {
            options.Command = CliOptions.CommandApply;
        }
        else if (options.UpdateSnapshot && options.Command == CliOptions.CommandAnalyze)
        {
            options.Command = CliOptions.CommandSnapshot;
        }

        return true;
    }

    private static bool TryExtractOptionValue(
        string[] args,
        ref int index,
        string longName,
        string shortName,
        out string? value,
        out string? errorMessage)
    {
        value = null;
        errorMessage = null;

        var arg = args[index];

        if (arg.StartsWith(longName + "=", StringComparison.OrdinalIgnoreCase))
        {
            value = arg[(longName.Length + 1)..];
            return true;
        }

        if (!string.IsNullOrEmpty(shortName) && arg.StartsWith(shortName + "=", StringComparison.OrdinalIgnoreCase))
        {
            value = arg[(shortName.Length + 1)..];
            return true;
        }

        if (string.Equals(arg, longName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, shortName, StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 < args.Length && !args[index + 1].StartsWith('-'))
            {
                index++;
                value = args[index];
                return true;
            }

            errorMessage = $"Missing required value for option '{arg}'.";
            return true;
        }

        return false;
    }

    public static string GetHelpText()
    {
        return """
        Pmad.CodeCondenser - Static analysis tool for C# clone detection and comment summarization

        Usage:
          pmad-codecondenser [path] [options]
          pmad-codecondenser <command> [path] [options]

        Commands:
          analyze [path]              Analyze C# codebase in dry-run mode (default)
          apply [path]                Apply summarized comments directly to source files on disk
          snapshot [path]             Create or refresh the snapshot baseline

        Arguments:
          [path]                      Target directory or C# file to analyze (default: current directory)

        Options:
          -a, --apply                 Write shortened comments directly to source files
          -i, --incremental           Use snapshot cache to analyze only modified or new files
          -u, --update-snapshot       Refresh the snapshot baseline
          -ts, --threshold-statements <n>  Minimum statements for clone detection (default: 8)
          -tc, --threshold-comments <n>    Minimum character length for verbose comments (default: 100)
          -m, --model <name>          Specify LLM model name (default: llama3.2:3b)
          -e, --endpoint <url>        Specify Ollama endpoint URL (default: http://localhost:11434)
          -c, --config <path>         Path to custom codecondenser.json configuration file
          --no-color                  Disable ANSI color formatting
          -h, --help                  Show command line help and options
          -v, --version               Show application version
        """;
    }

    public static string GetVersionText()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = assembly.GetName().Version?.ToString(3) ?? "0.1.0-preview";

        return $"pmad-codecondenser version {infoVersion ?? version}";
    }
}
