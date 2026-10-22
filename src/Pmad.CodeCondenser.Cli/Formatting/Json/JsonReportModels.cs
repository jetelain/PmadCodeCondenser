using System.Text.Json.Serialization;

namespace Pmad.CodeCondenser.Cli.Formatting.Json;

/// <summary>
/// Root schema container for CodeCondenser structured JSON export reports.
/// </summary>
public sealed record CodeCondenserJsonReport
{
    [JsonPropertyName("$schema")]
    public string Schema { get; init; } = "https://raw.githubusercontent.com/jetelain/PmadCodeCondenser/main/schemas/codecondenser-report.json";

    public string SchemaVersion { get; init; } = "1.0.0";

    public string Timestamp { get; init; } = DateTimeOffset.UtcNow.ToString("O");

    public JsonReportSummary Summary { get; init; } = new();

    public List<JsonCloneCluster> CloneClusters { get; init; } = [];

    public List<JsonSubMethodCluster> SubMethodClusters { get; init; } = [];

    public List<JsonVerboseComment> VerboseComments { get; init; } = [];
}

/// <summary>
/// Aggregated metrics summary for the analysis run.
/// </summary>
public sealed record JsonReportSummary
{
    public int FilesAnalyzed { get; init; }

    public int ProductionFiles { get; init; }

    public int TestFiles { get; init; }

    public int GeneratedFilesSkipped { get; init; }

    public int MethodsScanned { get; init; }

    public int CloneClustersCount { get; init; }

    public int ProductionClustersCount { get; init; }

    public int TestClustersCount { get; init; }

    public int CrossBoundaryClustersCount { get; init; }

    public int SubMethodClustersCount { get; init; }

    public int VerboseCommentsCount { get; init; }

    public string SummaryMode { get; init; } = string.Empty;

    public int? TotalCommentGainCharacters { get; init; }

    public int? TotalCommentReductionPercent { get; init; }

    public int? TotalCommentGainTokens { get; init; }

    public int? TotalCloneGainTokens { get; init; }
}

/// <summary>
/// Method-level structural clone cluster details.
/// </summary>
public sealed record JsonCloneCluster
{
    public int ClusterId { get; init; }

    public string Category { get; init; } = string.Empty;

    public int ImpactScore { get; init; }

    public string AstHash { get; init; } = string.Empty;

    public int StatementCount { get; init; }

    public List<JsonCloneMethod> Methods { get; init; } = [];

    public string Recommendation { get; init; } = string.Empty;

    public int? PotentialGainTokens { get; init; }
}

/// <summary>
/// Method entry within a clone cluster.
/// </summary>
public sealed record JsonCloneMethod
{
    public string FilePath { get; init; } = string.Empty;

    public string MethodName { get; init; } = string.Empty;

    public int StartLine { get; init; }

    public int EndLine { get; init; }

    public string Signature { get; init; } = string.Empty;
}

/// <summary>
/// Sub-method statement sequence clone cluster details.
/// </summary>
public sealed record JsonSubMethodCluster
{
    public int ClusterId { get; init; }

    public string Category { get; init; } = string.Empty;

    public int StatementCount { get; init; }

    public List<JsonSubMethodOccurrence> Occurrences { get; init; } = [];

    public string Recommendation { get; init; } = string.Empty;

    public int? PotentialGainTokens { get; init; }
}

/// <summary>
/// Single occurrence of a sub-method statement sequence clone.
/// </summary>
public sealed record JsonSubMethodOccurrence
{
    public string FilePath { get; init; } = string.Empty;

    public string MethodName { get; init; } = string.Empty;

    public int StartLine { get; init; }

    public int EndLine { get; init; }
}

/// <summary>
/// Verbose comment finding details.
/// </summary>
public sealed record JsonVerboseComment
{
    public string FilePath { get; init; } = string.Empty;

    public int StartLine { get; init; }

    public int EndLine { get; init; }

    public int CharacterCount { get; init; }

    public int EstimatedTokens { get; init; }

    public string Kind { get; init; } = string.Empty;

    public string? Summary { get; init; }

    public string? Recommendation { get; init; }

    public int? GainCharacters { get; init; }

    public int? GainTokens { get; init; }
}
