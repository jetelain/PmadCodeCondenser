using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Cli.Formatting.Sarif;

/// <summary>
/// Formats analysis findings into standard OASIS SARIF 2.1.0 documents.
/// Implements symmetric PR diff annotations and partial fingerprints for GitHub Code Scanning deduplication.
/// </summary>
public sealed class SarifReportFormatter : IReportFormatter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public Task FormatAsync(
        AnalysisResult result,
        CodeCondenserSettings settings,
        TextWriter output,
        string baseDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(output);

        var sarifLog = BuildSarifLog(result, baseDirectory);
        var json = JsonSerializer.Serialize(sarifLog, SerializerOptions);

        output.WriteLine(json);
        return Task.CompletedTask;
    }

    public static SarifLog BuildSarifLog(AnalysisResult result, string baseDirectory)
    {
        var driverVersion = GetDriverVersion();

        var run = new SarifRun
        {
            Tool = new SarifTool
            {
                Driver = new SarifDriver
                {
                    Name = "Pmad.CodeCondenser",
                    Version = driverVersion,
                    InformationUri = "https://github.com/jetelain/PmadCodeCondenser",
                    Rules = SarifRules.AllRules.ToList()
                }
            },
            Results = []
        };

        // 1. Method Code Clones (CC001, CC002, CC004)
        foreach (var group in result.CloneGroups)
        {
            var ruleId = group.Category switch
            {
                CloneCategory.Test => SarifRules.TestCodeDuplicationRuleId,
                CloneCategory.CrossBoundary => SarifRules.CrossBoundaryDuplicationRuleId,
                _ => SarifRules.ProductionCodeDuplicationRuleId
            };

            var level = group.Category == CloneCategory.Test ? "note" : "warning";
            var correlationGuid = DeterministicGuid.GenerateCorrelationGuid(group.NormalizedHash).ToString("D");
            var shortHash = group.NormalizedHash.Length > 12 ? group.NormalizedHash[..12] : group.NormalizedHash;

            var normalizedPaths = group.Methods
                .Select(m => ReportPathHelper.NormalizeRelativePath(m.FilePath, baseDirectory))
                .ToArray();

            // Symmetrical PR annotations: emit a result for EACH participating method
            for (int i = 0; i < group.Methods.Count; i++)
            {
                var method = group.Methods[i];
                var relPath = normalizedPaths[i];

                var relatedLocations = new List<SarifRelatedLocation>();
                int relatedId = 1;
                for (int j = 0; j < group.Methods.Count; j++)
                {
                    if (j == i) continue;
                    var other = group.Methods[j];
                    var otherRelPath = normalizedPaths[j];

                    relatedLocations.Add(new SarifRelatedLocation
                    {
                        Id = relatedId++,
                        PhysicalLocation = new SarifPhysicalLocation
                        {
                            ArtifactLocation = new SarifArtifactLocation { Uri = otherRelPath },
                            Region = new SarifRegion
                            {
                                StartLine = other.StartLine,
                                EndLine = other.EndLine
                            }
                        },
                        Message = new SarifMessage
                        {
                            Text = $"Duplicate implementation in {Path.GetFileName(other.FilePath)}:'{other.MethodName}' (line {other.StartLine})"
                        }
                    });
                }

                run.Results.Add(new SarifResult
                {
                    RuleId = ruleId,
                    Level = level,
                    CorrelationGuid = correlationGuid,
                    PartialFingerprints = new Dictionary<string, string>
                    {
                        ["astHash/v1"] = group.NormalizedHash,
                        ["locationKey/v1"] = $"{relPath}:{method.MethodName}"
                    },
                    Message = new SarifMessage
                    {
                        Text = $"Method '{method.MethodName}' shares {method.StatementCount} duplicate statements with {group.Methods.Count - 1} other method(s) (AST Hash: {shortHash}...). Extract common logic into a shared helper."
                    },
                    Locations =
                    [
                        new SarifLocation
                        {
                            PhysicalLocation = new SarifPhysicalLocation
                            {
                                ArtifactLocation = new SarifArtifactLocation { Uri = relPath },
                                Region = new SarifRegion
                                {
                                    StartLine = method.StartLine,
                                    EndLine = method.EndLine
                                }
                            }
                        }
                    ],
                    RelatedLocations = relatedLocations.Count > 0 ? relatedLocations : null
                });
            }
        }

        // 2. Sub-Method Code Clones (CC005)
        foreach (var subGroup in result.SubMethodCloneGroups)
        {
            var level = subGroup.Category == CloneCategory.Test ? "note" : "warning";
            var correlationGuid = DeterministicGuid.GenerateCorrelationGuid(subGroup.NormalizedHash).ToString("D");
            var shortHash = subGroup.NormalizedHash.Length > 12 ? subGroup.NormalizedHash[..12] : subGroup.NormalizedHash;

            var normalizedOccPaths = subGroup.Occurrences
                .Select(occ => ReportPathHelper.NormalizeRelativePath(occ.FilePath, baseDirectory))
                .ToArray();

            for (int i = 0; i < subGroup.Occurrences.Count; i++)
            {
                var occ = subGroup.Occurrences[i];
                var relPath = normalizedOccPaths[i];

                var relatedLocations = new List<SarifRelatedLocation>();
                int relatedId = 1;
                for (int j = 0; j < subGroup.Occurrences.Count; j++)
                {
                    if (j == i) continue;
                    var other = subGroup.Occurrences[j];
                    var otherRelPath = normalizedOccPaths[j];

                    relatedLocations.Add(new SarifRelatedLocation
                    {
                        Id = relatedId++,
                        PhysicalLocation = new SarifPhysicalLocation
                        {
                            ArtifactLocation = new SarifArtifactLocation { Uri = otherRelPath },
                            Region = new SarifRegion
                            {
                                StartLine = other.StartLine,
                                EndLine = other.EndLine
                            }
                        },
                        Message = new SarifMessage
                        {
                            Text = $"Duplicate statement block in {Path.GetFileName(other.FilePath)}:'{other.MethodName}' (lines {other.StartLine}-{other.EndLine})"
                        }
                    });
                }

                run.Results.Add(new SarifResult
                {
                    RuleId = SarifRules.SubMethodCodeDuplicationRuleId,
                    Level = level,
                    CorrelationGuid = correlationGuid,
                    PartialFingerprints = new Dictionary<string, string>
                    {
                        ["astHash/v1"] = subGroup.NormalizedHash,
                        ["locationKey/v1"] = $"{relPath}:{occ.MethodName}:{occ.StartLine}"
                    },
                    Message = new SarifMessage
                    {
                        Text = $"Method '{occ.MethodName}' contains {occ.StatementCount} duplicate statements shared across {subGroup.Occurrences.Count} locations (AST Hash: {shortHash}...). Extract duplicate statement sequence into a shared helper."
                    },
                    Locations =
                    [
                        new SarifLocation
                        {
                            PhysicalLocation = new SarifPhysicalLocation
                            {
                                ArtifactLocation = new SarifArtifactLocation { Uri = relPath },
                                Region = new SarifRegion
                                {
                                    StartLine = occ.StartLine,
                                    EndLine = occ.EndLine
                                }
                            }
                        }
                    ],
                    RelatedLocations = relatedLocations.Count > 0 ? relatedLocations : null
                });
            }
        }

        // 3. Verbose Comments (CC003)
        foreach (var comment in result.VerboseComments)
        {
            var relPath = ReportPathHelper.NormalizeRelativePath(comment.FilePath, baseDirectory);
            var correlationGuid = DeterministicGuid.GenerateCorrelationGuid(comment.ContentHash).ToString("D");

            run.Results.Add(new SarifResult
            {
                RuleId = SarifRules.VerboseCommentRuleId,
                Level = "note",
                CorrelationGuid = correlationGuid,
                PartialFingerprints = new Dictionary<string, string>
                {
                    ["commentHash/v1"] = comment.ContentHash,
                    ["locationKey/v1"] = $"{relPath}:{comment.StartLine}"
                },
                Message = new SarifMessage
                {
                    Text = $"Comment of {comment.OriginalText.Length} characters exceeds verbosity threshold. Consider condensing or using XML doc tags."
                },
                Locations =
                [
                    new SarifLocation
                    {
                        PhysicalLocation = new SarifPhysicalLocation
                        {
                            ArtifactLocation = new SarifArtifactLocation { Uri = relPath },
                            Region = new SarifRegion
                            {
                                StartLine = comment.StartLine,
                                EndLine = comment.EndLine
                            }
                        }
                    }
                ]
            });
        }

        // 4. Repeated Inline Strings (CC006)
        foreach (var strGroup in result.RepeatedStringGroups)
        {
            var level = strGroup.Category == CloneCategory.Test ? "note" : "warning";
            var correlationGuid = DeterministicGuid.GenerateCorrelationGuid(strGroup.Value).ToString("D");
            var stringHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(strGroup.Value)));

            var occurrencesToReport = strGroup.HasExistingConstant
                ? strGroup.InlineOccurrences
                : strGroup.Occurrences;

            var normalizedOccPaths = occurrencesToReport
                .Select(occ => ReportPathHelper.NormalizeRelativePath(occ.FilePath, baseDirectory))
                .ToArray();

            for (int i = 0; i < occurrencesToReport.Count; i++)
            {
                var occ = occurrencesToReport[i];
                var relPath = normalizedOccPaths[i];

                var relatedLocations = new List<SarifRelatedLocation>();
                int relatedId = 1;

                if (strGroup.HasExistingConstant && strGroup.ExistingConstantDeclaration != null)
                {
                    var constDecl = strGroup.ExistingConstantDeclaration;
                    var constRelPath = ReportPathHelper.NormalizeRelativePath(constDecl.FilePath, baseDirectory);
                    relatedLocations.Add(new SarifRelatedLocation
                    {
                        Id = relatedId++,
                        PhysicalLocation = new SarifPhysicalLocation
                        {
                            ArtifactLocation = new SarifArtifactLocation { Uri = constRelPath },
                            Region = new SarifRegion
                            {
                                StartLine = constDecl.StartLine,
                                EndLine = constDecl.EndLine,
                                StartColumn = constDecl.StartColumn,
                                EndColumn = constDecl.EndColumn
                            }
                        },
                        Message = new SarifMessage
                        {
                            Text = $"Existing constant '{strGroup.SuggestedClassName}.{strGroup.SuggestedConstantName}' declared in {Path.GetFileName(constDecl.FilePath)} (line {constDecl.StartLine})"
                        }
                    });
                }

                for (int j = 0; j < occurrencesToReport.Count; j++)
                {
                    if (j == i) continue;
                    var other = occurrencesToReport[j];
                    var otherRelPath = normalizedOccPaths[j];

                    relatedLocations.Add(new SarifRelatedLocation
                    {
                        Id = relatedId++,
                        PhysicalLocation = new SarifPhysicalLocation
                        {
                            ArtifactLocation = new SarifArtifactLocation { Uri = otherRelPath },
                            Region = new SarifRegion
                            {
                                StartLine = other.StartLine,
                                EndLine = other.EndLine,
                                StartColumn = other.StartColumn,
                                EndColumn = other.EndColumn
                            }
                        },
                        Message = new SarifMessage
                        {
                            Text = $"Duplicate string occurrence in {Path.GetFileName(other.FilePath)} (line {other.StartLine})"
                        }
                    });
                }

                var preview = strGroup.Value.Length > 40 ? strGroup.Value[..37] + "..." : strGroup.Value;
                var escapedPreview = preview.Replace("\r", " ").Replace("\n", " ");

                var promoNote = (strGroup.IsExistingConstantPrivate && strGroup.InlineOccurrences.Any(o => o.FilePath != strGroup.ExistingConstantDeclaration?.FilePath))
                    ? " (promote to internal/public if needed)"
                    : string.Empty;

                var messageText = strGroup.HasExistingConstant
                    ? $"String literal \"{escapedPreview}\" ({strGroup.Length} chars) matches existing constant '{strGroup.SuggestedClassName}.{strGroup.SuggestedConstantName}'. Use the constant instead of inline value{promoNote}."
                    : $"String literal \"{escapedPreview}\" ({strGroup.Length} chars) repeated {strGroup.Occurrences.Count} times. Extract into shared constant '{strGroup.SuggestedClassName}.{strGroup.SuggestedConstantName}'.";

                run.Results.Add(new SarifResult
                {
                    RuleId = SarifRules.RepeatedInlineStringRuleId,
                    Level = level,
                    CorrelationGuid = correlationGuid,
                    PartialFingerprints = new Dictionary<string, string>
                    {
                        ["stringHash/v1"] = stringHash,
                        ["locationKey/v1"] = $"{relPath}:{occ.StartLine}:{occ.StartColumn}"
                    },
                    Message = new SarifMessage
                    {
                        Text = messageText
                    },
                    Locations =
                    [
                        new SarifLocation
                        {
                            PhysicalLocation = new SarifPhysicalLocation
                            {
                                ArtifactLocation = new SarifArtifactLocation { Uri = relPath },
                                Region = new SarifRegion
                                {
                                    StartLine = occ.StartLine,
                                    EndLine = occ.EndLine,
                                    StartColumn = occ.StartColumn,
                                    EndColumn = occ.EndColumn
                                }
                            }
                        }
                    ],
                    RelatedLocations = relatedLocations.Count > 0 ? relatedLocations : null
                });
            }
        }

        // 5. Single Type Per File (CC007)
        foreach (var issue in result.TypeFileIssues.Where(i => i.HasViolation))
        {
            var relPath = ReportPathHelper.NormalizeRelativePath(issue.FilePath, baseDirectory);
            var primaryType = issue.PrimaryType;
            var targetType = ((issue.Action is TypeFileAction.Split or TypeFileAction.SplitAndRename) && issue.TypesToExtract.Count > 0)
                ? issue.TypesToExtract[0]
                : primaryType;
            var startLine = targetType?.StartLine ?? 1;
            var endLine = targetType?.EndLine ?? 1;

            string message = issue.Action switch
            {
                TypeFileAction.Rename => $"File '{relPath}' contains a single type '{primaryType?.TypeName}' and should be renamed to '{issue.SuggestedFileName}'.",
                TypeFileAction.SplitAndRename => $"File '{relPath}' contains {issue.Types.Count} types and should be renamed to '{issue.SuggestedFileName}' with auxiliary types extracted.",
                _ => $"File '{relPath}' contains {issue.Types.Count} types ({issue.TypesToExtract.Count} non-tolerated types to extract)."
            };

            run.Results.Add(new SarifResult
            {
                RuleId = SarifRules.SingleTypePerFileRuleId,
                Level = issue.Category == FileCategory.Test ? "note" : "warning",
                Message = new SarifMessage
                {
                    Text = message
                },
                Locations =
                [
                    new SarifLocation
                    {
                        PhysicalLocation = new SarifPhysicalLocation
                        {
                            ArtifactLocation = new SarifArtifactLocation
                            {
                                Uri = relPath
                            },
                            Region = new SarifRegion
                            {
                                StartLine = startLine,
                                EndLine = endLine
                            }
                        }
                    }
                ]
            });
        }

        return new SarifLog
        {
            Runs = [run]
        };
    }

    private static string GetDriverVersion()
    {
        var assembly = typeof(SarifReportFormatter).Assembly;
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            return infoVersion.Split('+')[0];
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.1.0-preview";
    }
}
