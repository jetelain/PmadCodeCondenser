using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Patterns;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Patterns;

public class PatternDetectorAndRewriterTests
{
    private readonly PatternDetector _detector = new();
    private readonly PatternRewriter _rewriter = new();
    private readonly CodeCondenserSettings _settings = new();

    [Fact]
    public void DetectCandidates_FindsMultiplePatternsInSingleFile()
    {
        var code = """
            namespace Demo;

            public class Service
            {
                public void Process(string input, List<string> items)
                {
                    if (input == null)
                        throw new ArgumentNullException(nameof(input));

                    var clean = input.Replace("\r\n", "\n").Replace("\r", "\n");

                    if (string.IsNullOrEmpty(clean.Trim()))
                        return;

                    var hasItems = items.Where(x => x.Length > 0).Any();
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var candidates = _detector.DetectCandidates(tree, "Service.cs", _settings, targetDotNetVersion: new Version(10, 0));

        Assert.Equal(4, candidates.Count);
        Assert.Contains(candidates, c => c.RuleId == "PCC404");
        Assert.Contains(candidates, c => c.RuleId == "PCC401");
        Assert.Contains(candidates, c => c.RuleId == "PCC402");
        Assert.Contains(candidates, c => c.RuleId == "PCC403");

        var charGain = PatternGainEstimator.EstimateTotalGainCharacters(candidates);
        var tokenGain = PatternGainEstimator.EstimateTotalGainTokens(candidates);
        Assert.True(charGain > 0);
        Assert.True(tokenGain > 0);
    }

    [Fact]
    public void Rewrite_AppliesAllPatternsSafelyAndPreservesStructure()
    {
        var code = """
            namespace Demo;

            public class Service
            {
                public void Process(string input, List<string> items)
                {
                    if (input == null)
                        throw new ArgumentNullException(nameof(input));

                    var clean = input.Replace("\r\n", "\n").Replace("\r", "\n");

                    if (string.IsNullOrEmpty(clean.Trim()))
                        return;

                    var hasItems = items.Where(x => x.Length > 0).Any();
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var rewritten = _rewriter.Rewrite(tree, "Service.cs", _settings, targetDotNetVersion: new Version(10, 0));
        var newCode = rewritten.GetRoot().ToFullString();

        Assert.Contains("ArgumentNullException.ThrowIfNull(input);", newCode);
        Assert.Contains("input.ReplaceLineEndings(\"\\n\");", newCode);
        Assert.Contains("string.IsNullOrWhiteSpace(clean)", newCode);
        Assert.Contains("items.Any(x => x.Length > 0)", newCode);
    }

    [Fact]
    public void DetectCandidates_WithIgnoreMarker_SuppressesRule()
    {
        var code = """
            namespace Demo;

            public class Service
            {
                public void Process(string input)
                {
                    // codecondenser:ignore
                    if (input == null)
                        throw new ArgumentNullException(nameof(input));

                    var clean = input.Replace("\r\n", "\n").Replace("\r", "\n");
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var candidates = _detector.DetectCandidates(tree, "Service.cs", _settings, targetDotNetVersion: new Version(10, 0));

        Assert.Single(candidates);
        Assert.Equal("PCC401", candidates[0].RuleId);
    }
}
