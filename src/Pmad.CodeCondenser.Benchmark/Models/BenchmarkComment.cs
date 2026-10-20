using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Benchmark.Models;

/// <summary>
/// Represents a comment extracted from a reference project for benchmarking.
/// </summary>
public sealed record BenchmarkComment(
    string Id,
    string Project,
    string FilePath,
    int StartLine,
    int EndLine,
    CommentKind Kind,
    string OriginalText,
    string ContentHash,
    int CharacterCount,
    int LineCount);
