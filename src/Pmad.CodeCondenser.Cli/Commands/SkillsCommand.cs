using System.Text.Json;
using Pmad.CodeCondenser.Cli.Formatting;
using Pmad.CodeCondenser.Cli.Skills;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;
using Pmad.CodeCondenser.Core.Filtering;

namespace Pmad.CodeCondenser.Cli.Commands;

/// <summary>
/// Installs or updates bundled AI agent skills in a workspace's .agents/skills directory.
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

        var targetSkillsDir = WorkspaceSkillManager.ResolveTargetSkillsDirectory(options.TargetPath, options.SkillsDirectory);
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

        var result = WorkspaceSkillManager.InstallSkills(targetSkillsDir, installOptions);

        if (options.Format == OutputFormat.Json)
        {
            var json = JsonSerializer.Serialize(new
            {
                targetDirectory = result.TargetDirectory,
                dryRun = options.DryRun,
                created = result.CreatedCount,
                updated = result.UpdatedCount,
                upToDate = result.UpToDateCount,
                removed = result.RemovedCount,
                files = result.Files.Select(f => new
                {
                    relativePath = f.RelativePath,
                    fullPath = f.FullPath,
                    status = f.Status.ToString()
                })
            }, new JsonSerializerOptions { WriteIndented = true });

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

        formatter.PrintInfo(output, $"Workspace base directory: {baseDir}");
        formatter.PrintInfo(output, $"Target skills directory:  {targetSkillsDir}");
        output.WriteLine();

        foreach (var fileResult in result.Files)
        {
            formatter.PrintSkillFile(output, fileResult, baseDir);
        }

        formatter.PrintSkillsSummary(output, result, options.DryRun);

        return ExitCodes.Success;
    }
}
