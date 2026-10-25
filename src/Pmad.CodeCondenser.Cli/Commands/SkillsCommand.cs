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

            var json = JsonSerializer.Serialize(jsonModel, new JsonSerializerOptions { WriteIndented = true });

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
