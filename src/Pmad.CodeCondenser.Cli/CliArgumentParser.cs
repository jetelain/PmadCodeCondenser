using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Reflection;

namespace Pmad.CodeCondenser.Cli;

/// <summary>
/// Argument parser for codecondenser command line options, built on System.CommandLine.
/// </summary>
public static class CliArgumentParser
{
    private static readonly ParserModel Model = new();

    public static bool TryParse(string[] args, out CliOptions options, out string? errorMessage)
    {
        return Model.TryParse(args, out options, out errorMessage);
    }

    public static string GetHelpText()
    {
        return """
        Pmad.CodeCondenser - Static analysis tool for C# clone detection and comment summarization

        Usage:
          codecondenser [path] [options]
          codecondenser <command> [path] [options]

        Commands:
          analyze [path]              Analyze C# codebase in dry-run mode (default)
          apply [path]                Apply summarized comments directly to source files on disk
          snapshot [path]             Create or refresh the snapshot baseline
          skills [path]               Install or update AI agent skills in workspace or globally (--global)
          measure [path]              Measure code in terms of estimated LLM tokens (total, comments, code)

        Arguments:
          [path]                      Target directory or C# file to analyze (default: current directory)

        Options:
          -a, --apply                 Write shortened comments directly to source files
          -i, --incremental           Use snapshot cache to analyze only modified or new files
          -u, --update-snapshot       Refresh the snapshot baseline
          -dt, --duplication-tolerance <n> Maximum new clone clusters tolerated in incremental mode (default: 0)
          -ct, --comment-tolerance <n>     Maximum new verbose comments tolerated in incremental mode
          -st, --string-tolerance <n>      Maximum new repeated string groups tolerated in incremental mode
          --summary <mode>            Comment summary mode: disabled, summarize, or report (default: summarize if LLM configured, report otherwise)
          --report-only               Report-only mode for comments without calling an LLM
          --no-comments               Disable comment extraction and summarization (alias for --summary disabled)
          --no-duplication            Disable structural code clone detection
          --no-strings                Disable repeated inline string detection
          --sub-method-clones         Enable sliding-window sub-method clone detection
          -ts, --threshold-statements <n>  Minimum statements for clone detection (default: 8)
          --threshold-submethod-statements <n> Minimum statements for sub-method clone detection (default: 10)
          -tc, --threshold-comments <n>    Minimum character length for verbose comments (default: 100)
          -tstr, --threshold-strings <n>   Minimum character length for repeated string detection (default: 100)
          -so, --string-occurrences <n>    Minimum occurrences for repeated string detection (default: 2)
          -txs, --threshold-xml-summary <n> Minimum content length for XML summary/remarks (default: threshold-comments)
          -txp, --threshold-xml-param <n>   Minimum content length for XML param (default: threshold-comments / 2)
          -sc, --summarized-comments <path> Path to summarized comments JSON file stored in git (default: .codecondenser/summarized-comments.json)
          -mll, --max-line-length <n>  Maximum line length for formatted comments (default: 120)
          -m, --model <name>          Specify LLM model name (default: gemma4:e4b)
          -mr, --max-retries <n>      Maximum number of LLM summarization retries (default: 1)
          -te, --estimator <name>     Token estimator preset: default, qwen, or gemma (default: default)
          -e, --endpoint <url>        Specify Ollama endpoint URL (default: http://localhost:11434)
          -cs, --context-size <n>     Specify context window size in tokens (default: 24576)
          -c, --config <path>         Path to custom codecondenser.json configuration file
          -gc, --global-config <path> Path to custom global user profile configuration file
          --no-global-config          Disable loading of global user profile configuration
          --include <pattern>         Add glob pattern of files/directories to include
          --exclude <pattern>         Add glob pattern of files/directories to exclude
          --exclude-tests             Completely exclude test files (alias: --production-only)
          --enforce-test-duplication  Treat test clones as CI policy violation in gate check
          --include-generated         Disable automatic exclusion of generated files
          -n, --dry-run               Preview actions without modifying files on disk
          -g, --global                Install skills globally into ~/.gemini/config/skills/ and ~/.agents/skills/
          --force                     Force overwrite skill files even if up to date
          --clean                     Remove obsolete files from skill directories
          --skill <name>              Specify a specific skill to install/update (default: all bundled skills)
          -d, --dir, --skills-dir <path> Override target skills directory (default: <workspace>/.agents/skills)
          -f, --format <type>          Output format: console, sarif, json (default: console)
          -o, --output <path>          Output file path (default: stdout)
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

        return $"codecondenser version {infoVersion ?? version}";
    }

    private sealed class ParserModel
    {
        public RootCommand RootCommand { get; }

        public Option<bool> ApplyOpt { get; }
        public Option<bool> IncrementalOpt { get; }
        public Option<bool> UpdateSnapshotOpt { get; }
        public Option<bool> NoColorOpt { get; }
        public Option<bool> NoCommentsOpt { get; }
        public Option<bool> EnableCommentsOpt { get; }
        public Option<bool> ReportOnlyOpt { get; }
        public Option<string?> SummaryOpt { get; }
        public Option<bool> NoDuplicationOpt { get; }
        public Option<bool> EnableDuplicationOpt { get; }
        public Option<bool> NoStringsOpt { get; }
        public Option<bool> EnableStringsOpt { get; }
        public Option<bool> ExcludeTestsOpt { get; }
        public Option<bool> EnforceTestDuplicationOpt { get; }
        public Option<bool> IncludeGeneratedOpt { get; }
        public Option<bool> SubMethodClonesOpt { get; }
        public Option<bool> DryRunOpt { get; }
        public Option<bool> GlobalOpt { get; }
        public Option<bool> ForceOpt { get; }
        public Option<bool> CleanOpt { get; }

        public Option<int?> DuplicationToleranceOpt { get; }
        public Option<int?> CommentToleranceOpt { get; }
        public Option<int?> RepeatedStringToleranceOpt { get; }
        public Option<int?> ThresholdStatementsOpt { get; }
        public Option<int?> ThresholdCommentsOpt { get; }
        public Option<int?> ThresholdRepeatedStringsOpt { get; }
        public Option<int?> ThresholdStringOccurrencesOpt { get; }
        public Option<int?> ThresholdXmlSummaryOpt { get; }
        public Option<int?> ThresholdXmlParamOpt { get; }
        public Option<int?> ThresholdSubMethodStatementsOpt { get; }
        public Option<int?> ContextSizeOpt { get; }
        public Option<int?> MaxLineLengthOpt { get; }

        public Option<string?> ModelOpt { get; }
        public Option<int?> MaxRetriesOpt { get; }
        public Option<string?> TokenEstimatorOpt { get; }
        public Option<string?> EndpointOpt { get; }
        public Option<string?> ConfigOpt { get; }
        public Option<string?> GlobalConfigOpt { get; }
        public Option<bool> NoGlobalConfigOpt { get; }
        public Option<string?> SummarizedCommentsOpt { get; }
        public Option<string?> OutputOpt { get; }
        public Option<string[]> IncludeOpt { get; }
        public Option<string[]> ExcludeOpt { get; }
        public Option<OutputFormat?> FormatOpt { get; }
        public Option<string?> SkillNameOpt { get; }
        public Option<string?> SkillsDirOpt { get; }

        public Command AnalyzeCommand { get; }
        public Command ApplyCommand { get; }
        public Command SnapshotCommand { get; }
        public Command SkillsCommand { get; }
        public Command MeasureCommand { get; }

        public ParserModel()
        {
            RootCommand = new RootCommand("Static analysis tool for C# clone detection and comment summarization");
            RootCommand.SetAction(_ => 0);

            // Add -v alias to VersionOption
            var versionOpt = RootCommand.Options.OfType<VersionOption>().FirstOrDefault();
            versionOpt?.Aliases.Add("-v");

            // Define recursive options
            ApplyOpt = new Option<bool>("--apply", "-a")
            {
                Description = "Write shortened comments directly to source files",
                Recursive = true
            };
            IncrementalOpt = new Option<bool>("--incremental", "-i")
            {
                Description = "Use snapshot cache to analyze only modified or new files",
                Recursive = true
            };
            UpdateSnapshotOpt = new Option<bool>("--update-snapshot", "-u")
            {
                Description = "Refresh the snapshot baseline",
                Recursive = true
            };
            NoColorOpt = new Option<bool>("--no-color")
            {
                Description = "Disable ANSI color formatting",
                Recursive = true
            };
            NoCommentsOpt = new Option<bool>("--no-comments", "--disable-comments", "--no-comment-summarization")
            {
                Description = "Disable comment extraction and summarization (alias for --summary disabled)",
                Recursive = true
            };
            EnableCommentsOpt = new Option<bool>("--enable-comments", "--enable-comment-summarization")
            {
                Description = "Explicitly enable comment summarization",
                Recursive = true
            };
            ReportOnlyOpt = new Option<bool>("--report-only")
            {
                Description = "Report-only mode for comments without calling an LLM",
                Recursive = true
            };
            SummaryOpt = new Option<string?>("--summary", "--summary-mode")
            {
                Description = "Comment summary mode: disabled, summarize, or report",
                Recursive = true,
                CustomParser = r =>
                {
                    if (r.Tokens.Count == 0) return null;
                    var val = r.Tokens[0].Value;
                    if (string.Equals(val, "disabled", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(val, "none", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(val, "off", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(val, "false", StringComparison.OrdinalIgnoreCase))
                    {
                        return "disabled";
                    }
                    if (string.Equals(val, "summarize", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(val, "condense", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(val, "llm", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(val, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        return "summarize";
                    }
                    if (string.Equals(val, "report", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(val, "report-only", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(val, "reportonly", StringComparison.OrdinalIgnoreCase))
                    {
                        return "report";
                    }
                    r.AddError($"Invalid value for --summary: '{val}'. Expected 'disabled', 'summarize', or 'report'.");
                    return null;
                }
            };
            NoDuplicationOpt = new Option<bool>("--no-duplication", "--disable-duplication", "--no-clone-detection")
            {
                Description = "Disable structural code clone detection",
                Recursive = true
            };
            EnableDuplicationOpt = new Option<bool>("--enable-duplication", "--enable-duplication-detection")
            {
                Description = "Explicitly enable code clone detection",
                Recursive = true
            };
            NoStringsOpt = new Option<bool>("--no-strings", "--disable-strings", "--no-repeated-strings")
            {
                Description = "Disable repeated inline string detection",
                Recursive = true
            };
            EnableStringsOpt = new Option<bool>("--enable-strings", "--enable-repeated-strings")
            {
                Description = "Explicitly enable repeated inline string detection",
                Recursive = true
            };
            ExcludeTestsOpt = new Option<bool>("--exclude-tests", "--production-only")
            {
                Description = "Completely exclude test files",
                Recursive = true
            };
            EnforceTestDuplicationOpt = new Option<bool>("--enforce-test-duplication")
            {
                Description = "Treat test clones as CI policy violation in gate check",
                Recursive = true
            };
            IncludeGeneratedOpt = new Option<bool>("--include-generated")
            {
                Description = "Disable automatic exclusion of generated files",
                Recursive = true
            };
            SubMethodClonesOpt = new Option<bool>("--sub-method-clones", "--submethod-clones", "--sub-method")
            {
                Description = "Enable sliding-window sub-method clone detection",
                Recursive = true
            };
            DryRunOpt = new Option<bool>("--dry-run", "-n")
            {
                Description = "Preview actions without modifying files on disk",
                Recursive = true
            };
            GlobalOpt = new Option<bool>("--global", "-g")
            {
                Description = "Install skills globally into ~/.gemini/config/skills/ and ~/.agents/skills/",
                Recursive = true
            };
            ForceOpt = new Option<bool>("--force")
            {
                Description = "Force overwrite skill files even if up to date",
                Recursive = true
            };
            CleanOpt = new Option<bool>("--clean")
            {
                Description = "Remove obsolete files from skill directories",
                Recursive = true
            };

            DuplicationToleranceOpt = new Option<int?>("--duplication-tolerance", "-dt")
            {
                Description = "Maximum new clone clusters tolerated in incremental mode",
                Recursive = true,
                CustomParser = r =>
                {
                    if (r.Tokens.Count == 0) return null;
                    var val = r.Tokens[0].Value;
                    if (!int.TryParse(val, out var dt) || dt < 0)
                    {
                        r.AddError($"Invalid value for --duplication-tolerance: '{val}'. Must be a non-negative integer.");
                        return null;
                    }
                    return dt;
                }
            };
            CommentToleranceOpt = new Option<int?>("--comment-tolerance", "-ct")
            {
                Description = "Maximum new verbose comments tolerated in incremental mode",
                Recursive = true,
                CustomParser = r =>
                {
                    if (r.Tokens.Count == 0) return null;
                    var val = r.Tokens[0].Value;
                    if (!int.TryParse(val, out var ct) || ct < 0)
                    {
                        r.AddError($"Invalid value for --comment-tolerance: '{val}'. Must be a non-negative integer.");
                        return null;
                    }
                    return ct;
                }
            };
            RepeatedStringToleranceOpt = new Option<int?>("--string-tolerance", "-st", "--repeated-string-tolerance")
            {
                Description = "Maximum new repeated string groups tolerated in incremental mode",
                Recursive = true,
                CustomParser = r =>
                {
                    if (r.Tokens.Count == 0) return null;
                    var val = r.Tokens[0].Value;
                    if (!int.TryParse(val, out var st) || st < 0)
                    {
                        r.AddError($"Invalid value for --string-tolerance: '{val}'. Must be a non-negative integer.");
                        return null;
                    }
                    return st;
                }
            };
            ThresholdStatementsOpt = new Option<int?>("--threshold-statements", "-ts")
            {
                Description = "Minimum statements for clone detection",
                Recursive = true,
                CustomParser = r =>
                {
                    if (r.Tokens.Count == 0) return null;
                    var val = r.Tokens[0].Value;
                    if (!int.TryParse(val, out var ts) || ts < 1)
                    {
                        r.AddError($"Invalid value for --threshold-statements: '{val}'. Must be a positive integer.");
                        return null;
                    }
                    return ts;
                }
            };
            ThresholdCommentsOpt = new Option<int?>("--threshold-comments", "-tc")
            {
                Description = "Minimum character length for verbose comments",
                Recursive = true,
                CustomParser = r =>
                {
                    if (r.Tokens.Count == 0) return null;
                    var val = r.Tokens[0].Value;
                    if (!int.TryParse(val, out var tc) || tc < 1)
                    {
                        r.AddError($"Invalid value for --threshold-comments: '{val}'. Must be a positive integer.");
                        return null;
                    }
                    return tc;
                }
            };
            ThresholdRepeatedStringsOpt = new Option<int?>("--threshold-strings", "-tstr", "--threshold-repeated-strings")
            {
                Description = "Minimum character length for repeated string detection (default: 100)",
                Recursive = true,
                CustomParser = r =>
                {
                    if (r.Tokens.Count == 0) return null;
                    var val = r.Tokens[0].Value;
                    if (!int.TryParse(val, out var ts) || ts < 1)
                    {
                        r.AddError($"Invalid value for --threshold-strings: '{val}'. Must be a positive integer.");
                        return null;
                    }
                    return ts;
                }
            };
            ThresholdStringOccurrencesOpt = new Option<int?>("--string-occurrences", "-so", "--threshold-string-occurrences")
            {
                Description = "Minimum occurrences for repeated string detection (default: 2)",
                Recursive = true,
                CustomParser = r =>
                {
                    if (r.Tokens.Count == 0) return null;
                    var val = r.Tokens[0].Value;
                    if (!int.TryParse(val, out var so) || so < 2)
                    {
                        r.AddError($"Invalid value for --string-occurrences: '{val}'. Must be an integer >= 2.");
                        return null;
                    }
                    return so;
                }
            };
            ThresholdXmlSummaryOpt = new Option<int?>("--threshold-xml-summary", "-txs")
            {
                Description = "Minimum content length for XML summary/remarks",
                Recursive = true,
                CustomParser = r =>
                {
                    if (r.Tokens.Count == 0) return null;
                    var val = r.Tokens[0].Value;
                    if (!int.TryParse(val, out var txs) || txs < 1)
                    {
                        r.AddError($"Invalid value for --threshold-xml-summary: '{val}'. Must be a positive integer.");
                        return null;
                    }
                    return txs;
                }
            };
            ThresholdXmlParamOpt = new Option<int?>("--threshold-xml-param", "-txp")
            {
                Description = "Minimum content length for XML param",
                Recursive = true,
                CustomParser = r =>
                {
                    if (r.Tokens.Count == 0) return null;
                    var val = r.Tokens[0].Value;
                    if (!int.TryParse(val, out var txp) || txp < 1)
                    {
                        r.AddError($"Invalid value for --threshold-xml-param: '{val}'. Must be a positive integer.");
                        return null;
                    }
                    return txp;
                }
            };
            ThresholdSubMethodStatementsOpt = new Option<int?>("--threshold-submethod-statements", "--submethod-threshold")
            {
                Description = "Minimum statements for sub-method clone detection",
                Recursive = true,
                CustomParser = r =>
                {
                    if (r.Tokens.Count == 0) return null;
                    var val = r.Tokens[0].Value;
                    if (!int.TryParse(val, out var tsm) || tsm < 1)
                    {
                        r.AddError($"Invalid value for --threshold-submethod-statements: '{val}'. Must be a positive integer.");
                        return null;
                    }
                    return tsm;
                }
            };
            ContextSizeOpt = new Option<int?>("--context-size", "-cs")
            {
                Description = "Specify context window size in tokens",
                Recursive = true,
                CustomParser = r =>
                {
                    if (r.Tokens.Count == 0) return null;
                    var val = r.Tokens[0].Value;
                    if (!int.TryParse(val, out var ctx) || ctx < 512)
                    {
                        r.AddError($"Invalid value for --context-size: '{val}'. Must be an integer >= 512.");
                        return null;
                    }
                    return ctx;
                }
            };
            MaxLineLengthOpt = new Option<int?>("--max-line-length", "-mll")
            {
                Description = "Maximum line length for formatted comments",
                Recursive = true,
                CustomParser = r =>
                {
                    if (r.Tokens.Count == 0) return null;
                    var val = r.Tokens[0].Value;
                    if (!int.TryParse(val, out var mll) || mll < 40)
                    {
                        r.AddError($"Invalid value for --max-line-length: '{val}'. Must be an integer >= 40.");
                        return null;
                    }
                    return mll;
                }
            };

            ModelOpt = new Option<string?>("--model", "-m")
            {
                Description = "Specify LLM model name",
                Recursive = true
            };
            MaxRetriesOpt = new Option<int?>("--max-retries", "-mr")
            {
                Description = "Maximum number of LLM summarization retries",
                Recursive = true,
                CustomParser = r =>
                {
                    if (r.Tokens.Count == 0) return null;
                    var val = r.Tokens[0].Value;
                    if (!int.TryParse(val, out var mr) || mr < 0)
                    {
                        r.AddError($"Invalid value for --max-retries: '{val}'. Must be a non-negative integer.");
                        return null;
                    }
                    return mr;
                }
            };
            TokenEstimatorOpt = new Option<string?>("--estimator", "-te", "--token-estimator")
            {
                Description = "Token estimator preset: default, qwen, or gemma",
                Recursive = true
            };
            EndpointOpt = new Option<string?>("--endpoint", "-e")
            {
                Description = "Specify Ollama endpoint URL",
                Recursive = true
            };
            ConfigOpt = new Option<string?>("--config", "-c")
            {
                Description = "Path to custom codecondenser.json configuration file",
                Recursive = true
            };
            GlobalConfigOpt = new Option<string?>("--global-config", "-gc")
            {
                Description = "Path to custom global user profile configuration file",
                Recursive = true
            };
            NoGlobalConfigOpt = new Option<bool>("--no-global-config")
            {
                Description = "Disable loading of global user profile configuration",
                Recursive = true
            };
            SummarizedCommentsOpt = new Option<string?>("--summarized-comments", "-sc", "--comments-file", "-cf")
            {
                Description = "Path to summarized comments JSON file stored in git",
                Recursive = true
            };
            OutputOpt = new Option<string?>("--output", "-o")
            {
                Description = "Output file path",
                Recursive = true,
                CustomParser = r =>
                {
                    if (r.Tokens.Count == 0 || string.IsNullOrWhiteSpace(r.Tokens[0].Value))
                    {
                        r.AddError("Missing file path for --output.");
                        return null;
                    }
                    return r.Tokens[0].Value;
                }
            };
            IncludeOpt = new Option<string[]>("--include", "-inc")
            {
                Description = "Add glob pattern of files/directories to include",
                Recursive = true,
                AllowMultipleArgumentsPerToken = true,
                CustomParser = r =>
                {
                    var list = new List<string>();
                    foreach (var token in r.Tokens)
                    {
                        if (string.IsNullOrWhiteSpace(token.Value))
                        {
                            r.AddError("Missing pattern value for --include.");
                            return Array.Empty<string>();
                        }
                        list.Add(token.Value);
                    }
                    return list.ToArray();
                }
            };
            ExcludeOpt = new Option<string[]>("--exclude", "-exc")
            {
                Description = "Add glob pattern of files/directories to exclude",
                Recursive = true,
                AllowMultipleArgumentsPerToken = true,
                CustomParser = r =>
                {
                    var list = new List<string>();
                    foreach (var token in r.Tokens)
                    {
                        if (string.IsNullOrWhiteSpace(token.Value))
                        {
                            r.AddError("Missing pattern value for --exclude.");
                            return Array.Empty<string>();
                        }
                        list.Add(token.Value);
                    }
                    return list.ToArray();
                }
            };
            FormatOpt = new Option<OutputFormat?>("--format", "-f")
            {
                Description = "Output format: console, sarif, json",
                Recursive = true,
                CustomParser = r =>
                {
                    if (r.Tokens.Count == 0) return null;
                    var val = r.Tokens[0].Value;
                    if (string.Equals(val, "console", StringComparison.OrdinalIgnoreCase)) return OutputFormat.Console;
                    if (string.Equals(val, "sarif", StringComparison.OrdinalIgnoreCase)) return OutputFormat.Sarif;
                    if (string.Equals(val, "json", StringComparison.OrdinalIgnoreCase)) return OutputFormat.Json;

                    r.AddError($"Invalid value for --format: '{val}'. Allowed values are: console, sarif, json.");
                    return null;
                }
            };
            SkillNameOpt = new Option<string?>("--skill", "--skill-name")
            {
                Description = "Specify a specific skill to install/update",
                Recursive = true
            };
            SkillsDirOpt = new Option<string?>("--skills-dir", "--skills-directory", "--dir", "-d")
            {
                Description = "Override target skills directory",
                Recursive = true
            };

            // Add all options to RootCommand (since Recursive = true, subcommands inherit them)
            RootCommand.Add(ApplyOpt);
            RootCommand.Add(IncrementalOpt);
            RootCommand.Add(UpdateSnapshotOpt);
            RootCommand.Add(NoColorOpt);
            RootCommand.Add(NoCommentsOpt);
            RootCommand.Add(EnableCommentsOpt);
            RootCommand.Add(ReportOnlyOpt);
            RootCommand.Add(SummaryOpt);
            RootCommand.Add(NoDuplicationOpt);
            RootCommand.Add(EnableDuplicationOpt);
            RootCommand.Add(NoStringsOpt);
            RootCommand.Add(EnableStringsOpt);
            RootCommand.Add(ExcludeTestsOpt);
            RootCommand.Add(EnforceTestDuplicationOpt);
            RootCommand.Add(IncludeGeneratedOpt);
            RootCommand.Add(SubMethodClonesOpt);
            RootCommand.Add(DryRunOpt);
            RootCommand.Add(GlobalOpt);
            RootCommand.Add(ForceOpt);
            RootCommand.Add(CleanOpt);

            RootCommand.Add(DuplicationToleranceOpt);
            RootCommand.Add(CommentToleranceOpt);
            RootCommand.Add(RepeatedStringToleranceOpt);
            RootCommand.Add(ThresholdStatementsOpt);
            RootCommand.Add(ThresholdCommentsOpt);
            RootCommand.Add(ThresholdRepeatedStringsOpt);
            RootCommand.Add(ThresholdStringOccurrencesOpt);
            RootCommand.Add(ThresholdXmlSummaryOpt);
            RootCommand.Add(ThresholdXmlParamOpt);
            RootCommand.Add(ThresholdSubMethodStatementsOpt);
            RootCommand.Add(ContextSizeOpt);
            RootCommand.Add(MaxLineLengthOpt);

            RootCommand.Add(ModelOpt);
            RootCommand.Add(MaxRetriesOpt);
            RootCommand.Add(TokenEstimatorOpt);
            RootCommand.Add(EndpointOpt);
            RootCommand.Add(ConfigOpt);
            RootCommand.Add(GlobalConfigOpt);
            RootCommand.Add(NoGlobalConfigOpt);
            RootCommand.Add(SummarizedCommentsOpt);
            RootCommand.Add(OutputOpt);
            RootCommand.Add(IncludeOpt);
            RootCommand.Add(ExcludeOpt);
            RootCommand.Add(FormatOpt);
            RootCommand.Add(SkillNameOpt);
            RootCommand.Add(SkillsDirOpt);

            // Path argument for root command
            RootCommand.Add(CreatePathArgument());

            // Subcommands
            AnalyzeCommand = new Command(CliOptions.CommandAnalyze, "Analyze C# codebase in dry-run mode (default)");
            AnalyzeCommand.Add(CreatePathArgument());
            AnalyzeCommand.SetAction(_ => 0);
            RootCommand.Add(AnalyzeCommand);

            ApplyCommand = new Command(CliOptions.CommandApply, "Apply summarized comments directly to source files on disk");
            ApplyCommand.Add(CreatePathArgument());
            ApplyCommand.SetAction(_ => 0);
            RootCommand.Add(ApplyCommand);

            SnapshotCommand = new Command(CliOptions.CommandSnapshot, "Create or refresh the snapshot baseline");
            SnapshotCommand.Add(CreatePathArgument());
            SnapshotCommand.SetAction(_ => 0);
            RootCommand.Add(SnapshotCommand);

            SkillsCommand = new Command(CliOptions.CommandSkills, "Install or update AI agent skills in the workspace (.agents/skills)");
            SkillsCommand.Aliases.Add("install-skills");
            SkillsCommand.Aliases.Add("install-skill");
            SkillsCommand.Add(CreatePathArgument());
            SkillsCommand.SetAction(_ => 0);

            var skillsInstallSub = new Command("install", "Install skills");
            skillsInstallSub.Add(CreatePathArgument());
            skillsInstallSub.SetAction(_ => 0);
            SkillsCommand.Add(skillsInstallSub);

            var skillsUpdateSub = new Command("update", "Update skills");
            skillsUpdateSub.Add(CreatePathArgument());
            skillsUpdateSub.SetAction(_ => 0);
            SkillsCommand.Add(skillsUpdateSub);

            RootCommand.Add(SkillsCommand);

            MeasureCommand = new Command(CliOptions.CommandMeasure, "Measure code in terms of estimated LLM tokens (total, comments, code)");
            MeasureCommand.Add(CreatePathArgument());
            MeasureCommand.SetAction(_ => 0);
            RootCommand.Add(MeasureCommand);
        }

        private static Argument<string[]> CreatePathArgument()
        {
            return new Argument<string[]>("path")
            {
                Arity = ArgumentArity.ZeroOrMore,
                CustomParser = r =>
                {
                    var list = new List<string>();
                    foreach (var t in r.Tokens)
                    {
                        if (t.Value.StartsWith('-'))
                        {
                            r.AddError($"Unrecognized option: '{t.Value}'. Use --help to view available options.");
                            return Array.Empty<string>();
                        }
                        list.Add(t.Value);
                    }
                    return list.ToArray();
                }
            };
        }

        private static bool WasSpecified(ParseResult pr, Option opt)
        {
            var res = pr.GetResult(opt);
            return res != null && !res.Implicit;
        }

        public bool TryParse(string[] args, out CliOptions options, out string? errorMessage)
        {
            options = new CliOptions();
            errorMessage = null;

            var pr = RootCommand.Parse(args);

            if (pr.Action is HelpAction ||
                args.Any(a => string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(a, "-?", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(a, "/?", StringComparison.OrdinalIgnoreCase)))
            {
                options.Help = true;
                return true;
            }

            if ((pr.Action != null && pr.Action.GetType().Name == "VersionOptionAction") ||
                args.Any(a => string.Equals(a, "--version", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(a, "-v", StringComparison.OrdinalIgnoreCase)))
            {
                options.Version = true;
                return true;
            }

            if (pr.Errors.Count > 0)
            {
                var unrecognized = pr.UnmatchedTokens.FirstOrDefault(t => t.StartsWith('-'));
                if (unrecognized != null)
                {
                    errorMessage = $"Unrecognized option: '{unrecognized}'. Use --help to view available options.";
                    return false;
                }

                var missingValError = pr.Errors.FirstOrDefault(e => e.SymbolResult is OptionResult optRes && optRes.Tokens.Count == 0);
                if (missingValError != null && missingValError.SymbolResult is OptionResult optRes)
                {
                    var optToken = optRes.IdentifierToken?.Value ?? optRes.Option.Name;
                    errorMessage = $"Missing required value for option '{optToken}'.";
                    return false;
                }

                errorMessage = pr.Errors[0].Message;
                return false;
            }

            // Command and TargetPath resolution
            var activeCommand = pr.CommandResult.Command;
            bool explicitCommand = activeCommand != RootCommand;
            string command = CliOptions.CommandAnalyze;
            if (explicitCommand)
            {
                var cmdName = activeCommand.Name;
                var parentName = (pr.CommandResult.Parent as CommandResult)?.Command.Name;
                if (cmdName == "skills" || parentName == "skills")
                {
                    command = CliOptions.CommandSkills;
                }
                else if (cmdName is CliOptions.CommandAnalyze or CliOptions.CommandApply or CliOptions.CommandSnapshot or CliOptions.CommandMeasure)
                {
                    command = cmdName;
                }
            }

            var pathArgResult = pr.CommandResult.Children.OfType<ArgumentResult>().FirstOrDefault();
            var paths = pathArgResult?.GetValueOrDefault<string[]>();
            if (paths != null && paths.Length > 0)
            {
                var nonNull = paths.FirstOrDefault(p => !string.IsNullOrEmpty(p));
                options.TargetPath = nonNull ?? paths[0] ?? ".";
            }
            else
            {
                options.TargetPath = ".";
            }

            // Flag values
            options.Apply = WasSpecified(pr, ApplyOpt) && pr.GetValue(ApplyOpt);
            options.Incremental = WasSpecified(pr, IncrementalOpt) && pr.GetValue(IncrementalOpt);
            options.UpdateSnapshot = WasSpecified(pr, UpdateSnapshotOpt) && pr.GetValue(UpdateSnapshotOpt);
            options.NoColor = WasSpecified(pr, NoColorOpt) && pr.GetValue(NoColorOpt);
            options.ExcludeTests = WasSpecified(pr, ExcludeTestsOpt) && pr.GetValue(ExcludeTestsOpt);
            options.EnforceTestDuplication = WasSpecified(pr, EnforceTestDuplicationOpt) && pr.GetValue(EnforceTestDuplicationOpt);
            options.IncludeGenerated = WasSpecified(pr, IncludeGeneratedOpt) && pr.GetValue(IncludeGeneratedOpt);
            options.SubMethodClones = WasSpecified(pr, SubMethodClonesOpt) && pr.GetValue(SubMethodClonesOpt);
            options.DryRun = WasSpecified(pr, DryRunOpt) && pr.GetValue(DryRunOpt);
            options.Global = WasSpecified(pr, GlobalOpt) && pr.GetValue(GlobalOpt);
            options.Force = WasSpecified(pr, ForceOpt) && pr.GetValue(ForceOpt);
            options.Clean = WasSpecified(pr, CleanOpt) && pr.GetValue(CleanOpt);
            options.NoGlobalConfig = WasSpecified(pr, NoGlobalConfigOpt) && pr.GetValue(NoGlobalConfigOpt);

            // Command override logic (flags take precedence if set)
            if (options.Apply)
            {
                options.Command = CliOptions.CommandApply;
            }
            else if (options.UpdateSnapshot && !explicitCommand)
            {
                options.Command = CliOptions.CommandSnapshot;
            }
            else
            {
                options.Command = command;
            }

            // Options with values
            if (WasSpecified(pr, DuplicationToleranceOpt)) options.DuplicationTolerance = pr.GetValue(DuplicationToleranceOpt);
            if (WasSpecified(pr, CommentToleranceOpt)) options.CommentTolerance = pr.GetValue(CommentToleranceOpt);
            if (WasSpecified(pr, RepeatedStringToleranceOpt)) options.RepeatedStringTolerance = pr.GetValue(RepeatedStringToleranceOpt);
            if (WasSpecified(pr, ThresholdStatementsOpt)) options.ThresholdStatements = pr.GetValue(ThresholdStatementsOpt);
            if (WasSpecified(pr, ThresholdCommentsOpt)) options.ThresholdComments = pr.GetValue(ThresholdCommentsOpt);
            if (WasSpecified(pr, ThresholdRepeatedStringsOpt)) options.ThresholdRepeatedStrings = pr.GetValue(ThresholdRepeatedStringsOpt);
            if (WasSpecified(pr, ThresholdStringOccurrencesOpt)) options.ThresholdStringOccurrences = pr.GetValue(ThresholdStringOccurrencesOpt);
            if (WasSpecified(pr, ThresholdXmlSummaryOpt)) options.ThresholdXmlSummary = pr.GetValue(ThresholdXmlSummaryOpt);
            if (WasSpecified(pr, ThresholdXmlParamOpt)) options.ThresholdXmlParam = pr.GetValue(ThresholdXmlParamOpt);
            if (WasSpecified(pr, ThresholdSubMethodStatementsOpt)) options.ThresholdSubMethodStatements = pr.GetValue(ThresholdSubMethodStatementsOpt);
            if (WasSpecified(pr, ContextSizeOpt)) options.ContextSize = pr.GetValue(ContextSizeOpt);
            if (WasSpecified(pr, MaxLineLengthOpt)) options.MaxLineLength = pr.GetValue(MaxLineLengthOpt);

            if (WasSpecified(pr, ModelOpt)) options.Model = pr.GetValue(ModelOpt);
            if (WasSpecified(pr, MaxRetriesOpt)) options.MaxSummarizationRetries = pr.GetValue(MaxRetriesOpt);
            if (WasSpecified(pr, TokenEstimatorOpt)) options.TokenEstimator = pr.GetValue(TokenEstimatorOpt);
            if (WasSpecified(pr, EndpointOpt)) options.Endpoint = pr.GetValue(EndpointOpt);
            if (WasSpecified(pr, ConfigOpt)) options.ConfigPath = pr.GetValue(ConfigOpt);
            if (WasSpecified(pr, GlobalConfigOpt)) options.GlobalConfigPath = pr.GetValue(GlobalConfigOpt);
            if (WasSpecified(pr, SummarizedCommentsOpt)) options.SummarizedCommentsPath = pr.GetValue(SummarizedCommentsOpt);
            if (WasSpecified(pr, OutputOpt)) options.OutputFilePath = pr.GetValue(OutputOpt);
            if (WasSpecified(pr, SkillNameOpt)) options.SkillName = pr.GetValue(SkillNameOpt);
            if (WasSpecified(pr, SkillsDirOpt)) options.SkillsDirectory = pr.GetValue(SkillsDirOpt);

            if (WasSpecified(pr, IncludeOpt))
            {
                var includes = pr.GetValue(IncludeOpt);
                if (includes != null) options.Include.AddRange(includes);
            }

            if (WasSpecified(pr, ExcludeOpt))
            {
                var excludes = pr.GetValue(ExcludeOpt);
                if (excludes != null) options.Exclude.AddRange(excludes);
            }

            // Comment and duplication tri-state flags
            if (WasSpecified(pr, NoCommentsOpt))
            {
                options.EnableCommentSummarization = false;
                options.SummaryMode = Core.Configuration.SummaryMode.Disabled;
            }
            else if (WasSpecified(pr, ReportOnlyOpt))
            {
                options.EnableCommentSummarization = true;
                options.SummaryMode = Core.Configuration.SummaryMode.Report;
            }
            else if (WasSpecified(pr, EnableCommentsOpt))
            {
                options.EnableCommentSummarization = true;
            }

            if (WasSpecified(pr, SummaryOpt))
            {
                var summaryVal = pr.GetValue(SummaryOpt);
                if (summaryVal == "disabled")
                {
                    options.SummaryMode = Core.Configuration.SummaryMode.Disabled;
                    options.EnableCommentSummarization = false;
                }
                else if (summaryVal == "summarize")
                {
                    options.SummaryMode = Core.Configuration.SummaryMode.Summarize;
                    options.EnableCommentSummarization = true;
                }
                else if (summaryVal == "report")
                {
                    options.SummaryMode = Core.Configuration.SummaryMode.Report;
                    options.EnableCommentSummarization = true;
                }
            }

            if (WasSpecified(pr, NoDuplicationOpt))
            {
                options.EnableDuplicationDetection = false;
            }
            else if (WasSpecified(pr, EnableDuplicationOpt))
            {
                options.EnableDuplicationDetection = true;
            }

            if (WasSpecified(pr, NoStringsOpt))
            {
                options.EnableRepeatedStrings = false;
            }
            else if (WasSpecified(pr, EnableStringsOpt))
            {
                options.EnableRepeatedStrings = true;
            }

            // Output format resolution
            bool formatExplicitlySet = WasSpecified(pr, FormatOpt);
            if (formatExplicitlySet)
            {
                options.Format = pr.GetValue(FormatOpt)!.Value;
            }
            else if (!string.IsNullOrWhiteSpace(options.OutputFilePath))
            {
                if (options.OutputFilePath.EndsWith(".sarif", StringComparison.OrdinalIgnoreCase))
                {
                    options.Format = OutputFormat.Sarif;
                }
                else if (options.OutputFilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    options.Format = OutputFormat.Json;
                }
            }

            return true;
        }
    }
}
