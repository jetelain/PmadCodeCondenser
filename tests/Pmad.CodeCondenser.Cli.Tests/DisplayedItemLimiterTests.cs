using AwesomeAssertions;
using Pmad.AI.Tokens;
using Pmad.CodeCondenser.Cli.Formatting;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Cli.Tests;

public class DisplayedItemLimiterTests
{
    private static VerboseComment CreateComment(string text, string file = "A.cs", int startLine = 1) =>
        new(file, startLine, startLine + 2, 0, text.Length, CommentKind.SingleLine, text, "");

    private static CodeCloneGroup CreateClone(string hash, int statementCount, int methodCount)
    {
        var methods = new List<MethodCloneInfo>();
        for (int i = 0; i < methodCount; i++)
        {
            methods.Add(new($"File{i}.cs", $"Method{i}", 10, 10 + statementCount, statementCount, hash, $"void Method{i}()"));
        }
        return new CodeCloneGroup(hash, methods, CloneCategory.Production, ImpactScore: statementCount * methodCount);
    }

    private static RepeatedStringGroup CreateStringGroup(string value, int count)
    {
        var occurrences = new List<RepeatedStringOccurrence>();
        for (int i = 0; i < count; i++)
        {
            occurrences.Add(new RepeatedStringOccurrence
            {
                FilePath = $"StringFile{i}.cs",
                StringValue = value,
                StartLine = i * 10,
                EndLine = i * 10,
                StartColumn = 1,
                EndColumn = 10,
                EnclosingMember = "EnclosingMethod"
            });
        }
        return new RepeatedStringGroup
        {
            Value = value,
            Occurrences = occurrences,
            SuggestedConstantName = "TestConst"
        };
    }

    [Fact]
    public void SelectItems_WhenNoLimit_ReturnsAllItemsSortedByBenefit()
    {
        var smallComment = CreateComment("Small comment content that is somewhat long but smaller.", startLine: 1);
        var largeComment = CreateComment(new string('X', 500), startLine: 10);
        var clone = CreateClone("hash1", 10, 2);

        var result = new AnalysisResult
        {
            AnalyzedFilesCount = 2,
            TotalMethodsScanned = 5,
            CloneGroups = [clone],
            VerboseComments = [smallComment, largeComment],
            SummarizedComments = new Dictionary<string, string>()
        };

        var settings = new CodeCondenserSettings { Limit = null };
        var limited = DisplayedItemLimiter.SelectItems(result, settings, ".");

        limited.IsLimited.Should().BeFalse();
        limited.TotalItemCount.Should().Be(3);
        limited.DisplayedItemCount.Should().Be(3);
        limited.CloneGroups.Should().HaveCount(1);
        limited.VerboseComments.Should().HaveCount(2);
        // Comments should be sorted descending by estimated benefit
        limited.VerboseComments[0].Should().Be(largeComment);
        limited.VerboseComments[1].Should().Be(smallComment);
    }

    [Fact]
    public void SelectItems_WhenLimitApplied_TakesTopItemsByLargestEstimatedBenefit()
    {
        // small comment: ~50 chars (~12 tokens)
        var smallComment = CreateComment("Small comment that exceeds minimum threshold but is short.");
        // large comment: 600 chars (~150 tokens)
        var largeComment = CreateComment(new string('C', 600));
        // massive repeated string: 200 chars * 5 occurrences (~250 tokens gain)
        var massiveString = CreateStringGroup(new string('S', 200), 5);
        // tiny repeated string: 30 chars * 2 occurrences (~15 tokens gain)
        var tinyString = CreateStringGroup(new string('T', 30), 2);
        // clone: 15 statements across 3 methods (~300 tokens fallback)
        var clone = CreateClone("clone1", 15, 3);

        var result = new AnalysisResult
        {
            AnalyzedFilesCount = 5,
            TotalMethodsScanned = 10,
            CloneGroups = [clone],
            RepeatedStringGroups = [massiveString, tinyString],
            VerboseComments = [smallComment, largeComment],
            SummarizedComments = new Dictionary<string, string>()
        };

        var settings = new CodeCondenserSettings { Limit = 2 };
        var limited = DisplayedItemLimiter.SelectItems(result, settings, ".");

        limited.IsLimited.Should().BeTrue();
        limited.TotalItemCount.Should().Be(5);
        limited.DisplayedItemCount.Should().Be(2);

        // The top 2 items across all categories should be clone and massiveString
        limited.CloneGroups.Should().HaveCount(1);
        limited.RepeatedStringGroups.Should().HaveCount(1);
        limited.RepeatedStringGroups[0].Should().Be(massiveString);
        limited.VerboseComments.Should().BeEmpty();
    }

    [Fact]
    public void SelectItems_WhenFeatureDisabled_ExcludesFromCandidates()
    {
        var comment = CreateComment(new string('C', 500));
        var clone = CreateClone("clone1", 10, 2);

        var result = new AnalysisResult
        {
            AnalyzedFilesCount = 2,
            TotalMethodsScanned = 5,
            CloneGroups = [clone],
            VerboseComments = [comment],
            SummarizedComments = new Dictionary<string, string>()
        };

        var settings = new CodeCondenserSettings
        {
            Limit = 5,
            EnableDuplicationDetection = false,
            EnableCommentSummarization = true
        };

        var limited = DisplayedItemLimiter.SelectItems(result, settings, ".");

        limited.TotalItemCount.Should().Be(1);
        limited.CloneGroups.Should().BeEmpty();
        limited.VerboseComments.Should().HaveCount(1);
    }

    [Fact]
    public void SelectItems_WhenLimitExceedsTotal_DoesNotMarkAsLimited()
    {
        var comment = CreateComment(new string('C', 200));
        var result = new AnalysisResult
        {
            AnalyzedFilesCount = 1,
            TotalMethodsScanned = 1,
            CloneGroups = [],
            VerboseComments = [comment],
            SummarizedComments = new Dictionary<string, string>()
        };

        var settings = new CodeCondenserSettings { Limit = 10 };
        var limited = DisplayedItemLimiter.SelectItems(result, settings, ".");

        limited.IsLimited.Should().BeFalse();
        limited.TotalItemCount.Should().Be(1);
        limited.DisplayedItemCount.Should().Be(1);
        limited.VerboseComments.Should().HaveCount(1);
    }

    [Fact]
    public void SelectItems_WhenLimitZero_ReturnsZeroDisplayedItemsAndIsMarkedLimited()
    {
        var comment = CreateComment(new string('C', 200));
        var clone = CreateClone("clone1", 10, 2);
        var result = new AnalysisResult
        {
            AnalyzedFilesCount = 2,
            TotalMethodsScanned = 5,
            CloneGroups = [clone],
            VerboseComments = [comment],
            SummarizedComments = new Dictionary<string, string>()
        };

        var settings = new CodeCondenserSettings { Limit = 0 };
        var limited = DisplayedItemLimiter.SelectItems(result, settings, ".");

        limited.IsLimited.Should().BeTrue();
        limited.TotalItemCount.Should().Be(2);
        limited.DisplayedItemCount.Should().Be(0);
        limited.CloneGroups.Should().BeEmpty();
        limited.VerboseComments.Should().BeEmpty();
    }

    [Fact]
    public void SelectItems_WhenIncludeCommentsFalse_ExcludesCommentsFromSelection()
    {
        var comment = CreateComment(new string('C', 1000));
        var clone = CreateClone("clone1", 10, 2);
        var result = new AnalysisResult
        {
            AnalyzedFilesCount = 2,
            TotalMethodsScanned = 5,
            CloneGroups = [clone],
            VerboseComments = [comment],
            SummarizedComments = new Dictionary<string, string>()
        };

        var settings = new CodeCondenserSettings { Limit = 1 };
        var limited = DisplayedItemLimiter.SelectItems(result, settings, ".", includeComments: false);

        limited.IsLimited.Should().BeFalse();
        limited.TotalItemCount.Should().Be(1);
        limited.DisplayedItemCount.Should().Be(1);
        limited.CloneGroups.Should().HaveCount(1);
        limited.VerboseComments.Should().BeEmpty();
    }
}
