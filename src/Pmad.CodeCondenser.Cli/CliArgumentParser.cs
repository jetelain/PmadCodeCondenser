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

            if (string.Equals(arg, "--no-comments", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--disable-comments", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--no-comment-summarization", StringComparison.OrdinalIgnoreCase))
            {
                options.EnableCommentSummarization = false;
                continue;
            }

            if (string.Equals(arg, "--enable-comments", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--enable-comment-summarization", StringComparison.OrdinalIgnoreCase))
            {
                options.EnableCommentSummarization = true;
                continue;
            }

            if (string.Equals(arg, "--no-duplication", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--disable-duplication", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--no-clone-detection", StringComparison.OrdinalIgnoreCase))
            {
                options.EnableDuplicationDetection = false;
                continue;
            }

            if (string.Equals(arg, "--enable-duplication", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--enable-duplication-detection", StringComparison.OrdinalIgnoreCase))
            {
                options.EnableDuplicationDetection = true;
                continue;
            }

            if (string.Equals(arg, "--exclude-tests", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--production-only", StringComparison.OrdinalIgnoreCase))
            {
                options.ExcludeTests = true;
                continue;
            }

            if (string.Equals(arg, "--enforce-test-duplication", StringComparison.OrdinalIgnoreCase))
            {
                options.EnforceTestDuplication = true;
                continue;
            }

            if (string.Equals(arg, "--include-generated", StringComparison.OrdinalIgnoreCase))
            {
                options.IncludeGenerated = true;
                continue;
            }

            if (string.Equals(arg, "--sub-method-clones", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--submethod-clones", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--sub-method", StringComparison.OrdinalIgnoreCase))
            {
                options.SubMethodClones = true;
                continue;
            }

            // Options with values: check both "--option value" and "--option=value"
            if (TryExtractOptionValue(args, ref i, "--threshold-submethod-statements", "--submethod-threshold", out var tsmVal, out errorMessage))
            {
                if (errorMessage != null) return false;
                if (!int.TryParse(tsmVal, out var tsm) || tsm < 1)
                {
                    errorMessage = $"Invalid value for --threshold-submethod-statements: '{tsmVal}'. Must be a positive integer.";
                    return false;
                }
                options.ThresholdSubMethodStatements = tsm;
                continue;
            }
            if (TryExtractOptionValue(args, ref i, "--duplication-tolerance", "-dt", out var dtVal, out errorMessage))
            {
                if (errorMessage != null) return false;
                if (!int.TryParse(dtVal, out var dt) || dt < 0)
                {
                    errorMessage = $"Invalid value for --duplication-tolerance: '{dtVal}'. Must be a non-negative integer.";
                    return false;
                }
                options.DuplicationTolerance = dt;
                continue;
            }
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

            if (TryExtractOptionValue(args, ref i, "--threshold-xml-summary", "-txs", out var txsVal, out errorMessage))
            {
                if (errorMessage != null) return false;
                if (!int.TryParse(txsVal, out var txs) || txs < 1)
                {
                    errorMessage = $"Invalid value for --threshold-xml-summary: '{txsVal}'. Must be a positive integer.";
                    return false;
                }
                options.ThresholdXmlSummary = txs;
                continue;
            }

            if (TryExtractOptionValue(args, ref i, "--threshold-xml-param", "-txp", out var txpVal, out errorMessage))
            {
                if (errorMessage != null) return false;
                if (!int.TryParse(txpVal, out var txp) || txp < 1)
                {
                    errorMessage = $"Invalid value for --threshold-xml-param: '{txpVal}'. Must be a positive integer.";
                    return false;
                }
                options.ThresholdXmlParam = txp;
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

            if (TryExtractOptionValue(args, ref i, "--context-size", "-cs", out var ctxVal, out errorMessage))
            {
                if (errorMessage != null) return false;
                if (!int.TryParse(ctxVal, out var ctx) || ctx < 512)
                {
                    errorMessage = $"Invalid value for --context-size: '{ctxVal}'. Must be an integer >= 512.";
                    return false;
                }
                options.ContextSize = ctx;
                continue;
            }

            if (TryExtractOptionValue(args, ref i, "--config", "-c", out var configVal, out errorMessage))
            {
                if (errorMessage != null) return false;
                options.ConfigPath = configVal;
                continue;
            }

            if (TryExtractOptionValue(args, ref i, "--summarized-comments", "-sc", out var scVal, out errorMessage) ||
                TryExtractOptionValue(args, ref i, "--comments-file", "-cf", out scVal, out errorMessage))
            {
                if (errorMessage != null) return false;
                options.SummarizedCommentsPath = scVal;
                continue;
            }

            if (TryExtractOptionValue(args, ref i, "--max-line-length", "-mll", out var mllVal, out errorMessage))
            {
                if (errorMessage != null) return false;
                if (!int.TryParse(mllVal, out var mll) || mll < 40)
                {
                    errorMessage = $"Invalid value for --max-line-length: '{mllVal}'. Must be an integer >= 40.";
                    return false;
                }
                options.MaxLineLength = mll;
                continue;
            }

            if (TryExtractOptionValue(args, ref i, "--include", "-inc", out var incVal, out errorMessage))
            {
                if (errorMessage != null) return false;
                if (string.IsNullOrWhiteSpace(incVal))
                {
                    errorMessage = "Missing pattern value for --include.";
                    return false;
                }
                options.Include.Add(incVal);
                continue;
            }

            if (TryExtractOptionValue(args, ref i, "--exclude", "-exc", out var excVal, out errorMessage))
            {
                if (errorMessage != null) return false;
                if (string.IsNullOrWhiteSpace(excVal))
                {
                    errorMessage = "Missing pattern value for --exclude.";
                    return false;
                }
                options.Exclude.Add(excVal);
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
        var explicitCommand = false;
        if (positionalArgs.Count > 0)
        {
            var first = positionalArgs[0].ToLowerInvariant();
            if (first is CliOptions.CommandAnalyze or CliOptions.CommandApply or CliOptions.CommandSnapshot)
            {
                options.Command = first;
                explicitCommand = true;
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
        else if (options.UpdateSnapshot && !explicitCommand)
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
            if (index + 1 < args.Length && (!args[index + 1].StartsWith('-') || int.TryParse(args[index + 1], out _)))
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
          -dt, --duplication-tolerance <n> Maximum new clone clusters tolerated in incremental mode (default: 0)
          --no-comments               Disable comment extraction and summarization
          --no-duplication            Disable structural code clone detection
          --sub-method-clones         Enable sliding-window sub-method clone detection
          -ts, --threshold-statements <n>  Minimum statements for clone detection (default: 8)
          --threshold-submethod-statements <n> Minimum statements for sub-method clone detection (default: 10)
          -tc, --threshold-comments <n>    Minimum character length for verbose comments (default: 100)
          -txs, --threshold-xml-summary <n> Minimum content length for XML summary/remarks (default: threshold-comments)
          -txp, --threshold-xml-param <n>   Minimum content length for XML param (default: threshold-comments / 2)
          -sc, --summarized-comments <path> Path to summarized comments JSON file stored in git (default: .codecondenser/summarized-comments.json)
          -mll, --max-line-length <n>  Maximum line length for formatted comments (default: 120)
          -m, --model <name>          Specify LLM model name (default: gemma4:e4b)
          -e, --endpoint <url>        Specify Ollama endpoint URL (default: http://localhost:11434)
          -cs, --context-size <n>     Specify context window size in tokens (default: 24576)
          -c, --config <path>         Path to custom codecondenser.json configuration file
          --include <pattern>         Add glob pattern of files/directories to include
          --exclude <pattern>         Add glob pattern of files/directories to exclude
          --exclude-tests             Completely exclude test files (alias: --production-only)
          --enforce-test-duplication  Treat test clones as CI policy violation in gate check
          --include-generated         Disable automatic exclusion of generated files
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
