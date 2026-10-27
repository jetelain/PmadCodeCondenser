using System.CommandLine;
using System.Text.Json;
using Pmad.CodeCondenser.Cli.Formatting;
using Pmad.CodeCondenser.Cli.Skills;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;
using Pmad.CodeCondenser.Core.Filtering;

namespace Pmad.CodeCondenser.Cli.Commands;

/// <summary>
/// Installs or updates bundled AI agent skills in a workspace's .agents/skills directory or globally.
/// </summary>
public sealed class SkillsCommand : ICommand
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static Command Create(GlobalOptions globals, CommandServices services)
    {
        ArgumentNullException.ThrowIfNull(globals);
        ArgumentNullException.ThrowIfNull(services);

        var globalOpt = new Option<bool>("--global", "-g")
        {
            Description = "Install skills globally into user configuration directories (~/.gemini/config/skills/ and ~/.agents/skills/)",
            Recursive = true
        };

        var skillOpt = new Option<string?>("--skill", "--skill-name")
        {
            Description = "Specify a specific skill to install or update (e.g. 'codecondenser')",
            Recursive = true
        };

        var dirOpt = new Option<string?>("--dir", "-d", "--skills-dir", "--skills-directory")
        {
            Description = "Override target skills directory",
            Recursive = true
        };

        var forceOpt = new Option<bool>("--force")
        {
            Description = "Force overwrite skill files even if up to date",
            Recursive = true
        };

        var cleanOpt = new Option<bool>("--clean")
        {
            Description = "Remove obsolete files from target skill directories",
            Recursive = true
        };

        var rootPathArg = new Argument<string?>("path")
        {
            Description = "Target directory or workspace root (default: current directory)",
            Arity = ArgumentArity.ZeroOrOne
        };

        var installPathArg = new Argument<string?>("path")
        {
            Description = "Target directory or workspace root (default: current directory)",
            Arity = ArgumentArity.ZeroOrOne
        };

        var updatePathArg = new Argument<string?>("path")
        {
            Description = "Target directory or workspace root (default: current directory)",
            Arity = ArgumentArity.ZeroOrOne
        };

        Task<int> HandleAction(ParseResult parseResult, Argument<string?> arg, CancellationToken ct)
        {
            var path = parseResult.GetValue(arg) ?? ".";
            var options = globals.BindCliOptions(parseResult, CliOptions.CommandSkills, path);

            options.Global = parseResult.GetValue(globalOpt);
            options.SkillName = parseResult.GetValue(skillOpt);
            options.SkillsDirectory = parseResult.GetValue(dirOpt);
            options.Force = parseResult.GetValue(forceOpt);
            options.Clean = parseResult.GetValue(cleanOpt);

            return services.RunCommandAsync(options, new SkillsCommand(), ct);
        }

        var cmd = new Command("skills", "Install or update AI agent skills in the workspace (.agents/skills)");
        cmd.Aliases.Add("install-skills");
        cmd.Aliases.Add("install-skill");

        cmd.Add(globalOpt);
        cmd.Add(skillOpt);
        cmd.Add(dirOpt);
        cmd.Add(forceOpt);
        cmd.Add(cleanOpt);
        cmd.Add(rootPathArg);

        cmd.SetAction((parseResult, ct) => HandleAction(parseResult, rootPathArg, ct));

        var installSub = new Command("install", "Install AI agent skills in the workspace or globally");
        installSub.Add(installPathArg);
        installSub.SetAction((parseResult, ct) => HandleAction(parseResult, installPathArg, ct));
        cmd.Add(installSub);

        var updateSub = new Command("update", "Update existing AI agent skills to the latest bundled version");
        updateSub.Add(updatePathArg);
        updateSub.SetAction((parseResult, ct) => HandleAction(parseResult, updatePathArg, ct));
        cmd.Add(updateSub);

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

        var targetSkillsDirs = WorkspaceSkillManager.ResolveTargetSkillsDirectories(
            options.TargetPath,
            options.SkillsDirectory,
            options.Global);

        if (targetSkillsDirs.Count == 0)
        {
            formatter.PrintError(error, "Could not determine target directory for skills installation.");
            return ExitCodes.Error;
        }

        var baseDir = PathFilter.ResolveBaseDirectory(options.TargetPath);

        var availableSkills = WorkspaceSkillManager.GetAvailableSkillNames();
        if (availableSkills.Count == 0)
        {
            formatter.PrintError(error, "No bundled skills found in the application binary.");
            return ExitCodes.Error;
        }

        if (!string.IsNullOrWhiteSpace(options.SkillName) &&
            !availableSkills.Contains(options.SkillName, StringComparer.OrdinalIgnoreCase))
        {
            formatter.PrintError(error, $"Skill '{options.SkillName}' not found. Available skill(s): {string.Join(", ", availableSkills)}.");
            return ExitCodes.Error;
        }

        var installOptions = new SkillInstallOptions
        {
            DryRun = options.DryRun,
            Force = options.Force,
            Clean = options.Clean,
            SkillName = options.SkillName
        };

        var results = WorkspaceSkillManager.InstallSkillsToDirectories(targetSkillsDirs, installOptions);

        if (options.Format == OutputFormat.Json)
        {
            object jsonModel;
            if (results.Count == 1 && !options.Global)
            {
                var r = results[0];
                jsonModel = new
                {
                    targetDirectory = r.TargetDirectory,
                    dryRun = options.DryRun,
                    created = r.CreatedCount,
                    updated = r.UpdatedCount,
                    upToDate = r.UpToDateCount,
                    removed = r.RemovedCount,
                    files = r.Files.Select(f => new
                    {
                        relativePath = f.RelativePath,
                        fullPath = f.FullPath,
                        status = f.Status.ToString()
                    })
                };
            }
            else
            {
                jsonModel = new
                {
                    isGlobal = options.Global,
                    dryRun = options.DryRun,
                    created = results.Sum(r => r.CreatedCount),
                    updated = results.Sum(r => r.UpdatedCount),
                    upToDate = results.Sum(r => r.UpToDateCount),
                    removed = results.Sum(r => r.RemovedCount),
                    targets = results.Select(r => new
                    {
                        targetDirectory = r.TargetDirectory,
                        created = r.CreatedCount,
                        updated = r.UpdatedCount,
                        upToDate = r.UpToDateCount,
                        removed = r.RemovedCount,
                        files = r.Files.Select(f => new
                        {
                            relativePath = f.RelativePath,
                            fullPath = f.FullPath,
                            status = f.Status.ToString()
                        })
                    })
                };
            }

            var json = JsonSerializer.Serialize(jsonModel, JsonOptions);

            if (!string.IsNullOrWhiteSpace(options.OutputFilePath))
            {
                var fullOutputPath = Path.GetFullPath(options.OutputFilePath);
                var outDir = Path.GetDirectoryName(fullOutputPath);
                if (!string.IsNullOrEmpty(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }

                await File.WriteAllTextAsync(fullOutputPath, json, cancellationToken).ConfigureAwait(false);
                formatter.PrintSuccess(output, $"Skills report saved to: {fullOutputPath}");
            }
            else
            {
                output.WriteLine(json);
            }

            return ExitCodes.Success;
        }

        if (results.Count == 1 && !options.Global)
        {
            var singleResult = results[0];
            formatter.PrintInfo(output, $"Workspace base directory: {baseDir}");
            formatter.PrintInfo(output, $"Target skills directory:  {singleResult.TargetDirectory}");
            output.WriteLine();

            foreach (var fileResult in singleResult.Files)
            {
                formatter.PrintSkillFile(output, fileResult, baseDir);
            }

            formatter.PrintSkillsSummary(output, singleResult, options.DryRun);
            return ExitCodes.Success;
        }

        formatter.PrintInfo(output, "Installing skills at global scope.");
        output.WriteLine();

        for (int i = 0; i < results.Count; i++)
        {
            var res = results[i];
            if (i > 0)
            {
                output.WriteLine();
            }

            formatter.PrintInfo(output, $"Target skills directory:  {res.TargetDirectory}");

            var printBase = Path.GetDirectoryName(res.TargetDirectory) ?? res.TargetDirectory;
            foreach (var fileResult in res.Files)
            {
                formatter.PrintSkillFile(output, fileResult, printBase);
            }

            formatter.PrintSkillsSummary(output, res, options.DryRun);
        }

        return ExitCodes.Success;
    }
}
