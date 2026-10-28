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

    public List<JsonRepeatedStringCluster> RepeatedStrings { get; init; } = [];

    public List<JsonVerboseComment> VerboseComments { get; init; } = [];

    public List<JsonTypeFileIssue> TypeFileIssues { get; init; } = [];

    public List<JsonLinesOfCodeViolation> LinesOfCodeViolations { get; init; } = [];

    public List<JsonPredicateCluster> PredicateClusters { get; init; } = [];
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

    public int RepeatedStringsCount { get; init; }

    public int TypeFileIssuesCount { get; init; }

    public int LinesOfCodeViolationsCount { get; init; }

    public int PredicateClustersCount { get; init; }

    public int VerboseCommentsCount { get; init; }

    public string SummaryMode { get; init; } = string.Empty;

    public int? TotalCommentGainCharacters { get; init; }

    public int? TotalCommentReductionPercent { get; init; }

    public int? TotalCommentGainTokens { get; init; }

    public int? TotalCloneGainTokens { get; init; }

    public int? TotalRepeatedStringGainCharacters { get; init; }

    public int? TotalRepeatedStringGainTokens { get; init; }

    public int? TotalPredicateGainCharacters { get; init; }

    public int? TotalPredicateGainTokens { get; init; }
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

/// <summary>
/// Repeated inline string cluster details.
/// </summary>
public sealed record JsonRepeatedStringCluster
{
    public int ClusterId { get; init; }

    public string Value { get; init; } = string.Empty;

    public int Length { get; init; }

    public int OccurrenceCount { get; init; }

    public string Category { get; init; } = string.Empty;

    public string SuggestedConstantName { get; init; } = string.Empty;

    public string SuggestedClassName { get; init; } = "Constants";

    public bool HasExistingConstant { get; init; }

    public string? ExistingConstant { get; init; }

    public bool IsExistingConstantPrivate { get; init; }

    public List<JsonRepeatedStringOccurrence> Occurrences { get; init; } = [];

    public string Recommendation { get; init; } = string.Empty;

    public int? PotentialGainCharacters { get; init; }

    public int? PotentialGainTokens { get; init; }
}

/// <summary>
/// Single occurrence of an inline string literal in a repeated string cluster.
/// </summary>
public sealed record JsonRepeatedStringOccurrence
{
    public string FilePath { get; init; } = string.Empty;

    public int StartLine { get; init; }

    public int EndLine { get; init; }

    public int StartColumn { get; init; }

    public int EndColumn { get; init; }

    public string? EnclosingMember { get; init; }

    public bool IsConstDeclaration { get; init; }

    public bool IsPrivateConstant { get; init; }
}

/// <summary>
/// Details of a source file requiring type splitting or renaming.
/// </summary>
public sealed record JsonTypeFileIssue
{
    public string FilePath { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string? SuggestedFileName { get; init; }

    public string? PrimaryType { get; init; }

    public List<JsonExtractedType> TypesToExtract { get; init; } = [];
}

/// <summary>
/// Information about a secondary type to be extracted into a separate file.
/// </summary>
public sealed record JsonExtractedType
{
    public string TypeName { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public int LinesOfCode { get; init; }

    public int StartLine { get; init; }

    public int EndLine { get; init; }

    public string TargetFileName { get; init; } = string.Empty;
}

/// <summary>
/// Method or class exceeding lines of code threshold.
/// </summary>
public sealed record JsonLinesOfCodeViolation
{
    public string FilePath { get; init; } = string.Empty;

    public string Identifier { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public int LinesOfCode { get; init; }

    public int Threshold { get; init; }

    public int StartLine { get; init; }

    public int EndLine { get; init; }

    public string Category { get; init; } = string.Empty;
}

/// <summary>
/// Predicate / boolean condition clone cluster details.
/// </summary>
public sealed record JsonPredicateCluster
{
    public int ClusterId { get; init; }

    public string Category { get; init; } = string.Empty;

    public string NormalizedSignature { get; init; } = string.Empty;

    public ulong NormalizedHash { get; init; }

    public bool HasExistingHelper { get; init; }

    public string SuggestedHelperName { get; init; } = string.Empty;

    public string SuggestedClassName { get; init; } = string.Empty;

    public string? ExistingHelperName { get; init; }

    public string? ExistingHelperDeclaringType { get; init; }

    public bool? ExistingHelperIsStatic { get; init; }

    public string TypicalUsage { get; init; } = string.Empty;

    public List<JsonPredicateOccurrence> Occurrences { get; init; } = [];

    public int? PotentialGainCharacters { get; init; }

    public int? PotentialGainTokens { get; init; }
}

/// <summary>
/// Individual predicate clone occurrence details.
/// </summary>
public sealed record JsonPredicateOccurrence
{
    public string FilePath { get; init; } = string.Empty;

    public int StartLine { get; init; }

    public int EndLine { get; init; }

    public int StartColumn { get; init; }

    public int EndColumn { get; init; }

    public string ExpressionText { get; init; } = string.Empty;

    public string? EnclosingMember { get; init; }

    public bool IsHelperDeclaration { get; init; }

    public string? ReceiverExpression { get; init; }

    public List<string> ArgumentExpressions { get; init; } = [];

    public bool IsNegatedAtCallSite { get; init; }

    public string TypicalReplacement { get; init; } = string.Empty;
}

