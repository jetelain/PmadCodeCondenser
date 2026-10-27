using System.Text.Json;
using AwesomeAssertions;
using Pmad.CodeCondenser.Cli.Formatting.Json;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Cli.Tests;

public class JsonReportFormatterTests
{
    private readonly JsonReportFormatter _formatter = new();
    private readonly CodeCondenserSettings _settings = new();

    [Fact]
    public async Task FormatAsync_FullAnalysisResult_EmitsConformingJson()
    {
        var methods = new List<MethodCloneInfo>
        {
            new("c:/repo/src/A.cs", "DoSomething", 10, 20, 10, "astHashA", "void DoSomething()"),
            new("c:/repo/src/B.cs", "DoSomethingElse", 30, 40, 10, "astHashA", "void DoSomethingElse()")
        };
        var cloneGroup = new CodeCloneGroup("astHashA", methods, CloneCategory.Production, ImpactScore: 10);

        var occurrences = new List<SubMethodCloneOccurrence>
        {
            new("c:/repo/src/SubA.cs", "MethodA", 50, 65, 15),
            new("c:/repo/src/SubB.cs", "MethodB", 70, 85, 15)
        };
        var subGroup = new SubMethodCloneGroup("subHash1", occurrences, 15, CloneCategory.Production, ImpactScore: 15);

        var comment = new VerboseComment(
            FilePath: "c:/repo/src/A.cs",
            StartLine: 1,
            EndLine: 5,
            SpanStart: 0,
            SpanLength: 120,
            Kind: CommentKind.SingleLine,
            OriginalText: new string('c', 120),
            LeadingWhitespace: "");

        var summaries = new Dictionary<string, string>
        {
            [comment.ContentHash] = "Summarized comment text."
        };

        var result = new AnalysisResult
        {
            AnalyzedFilesCount = 4,
            ProductionFilesCount = 4,
            TestFilesCount = 0,
            SkippedGeneratedFilesCount = 1,
            TotalMethodsScanned = 15,
            CloneGroups = [cloneGroup],
            SubMethodCloneGroups = [subGroup],
            VerboseComments = [comment],
            SummarizedComments = summaries
        };

        using var writer = new StringWriter();
        await _formatter.FormatAsync(result, _settings, writer, "c:/repo");

        var json = writer.ToString();
        var report = JsonSerializer.Deserialize<CodeCondenserJsonReport>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        report.Should().NotBeNull();
        report!.Schema.Should().Be("https://raw.githubusercontent.com/jetelain/PmadCodeCondenser/main/schemas/codecondenser-report.json");
        report.SchemaVersion.Should().Be("1.0.0");
        report.Timestamp.Should().NotBeNullOrWhiteSpace();

        // Summary verification
        report.Summary.FilesAnalyzed.Should().Be(4);
        report.Summary.ProductionFiles.Should().Be(4);
        report.Summary.TestFiles.Should().Be(0);
        report.Summary.GeneratedFilesSkipped.Should().Be(1);
        report.Summary.MethodsScanned.Should().Be(15);
        report.Summary.CloneClustersCount.Should().Be(1);
        report.Summary.ProductionClustersCount.Should().Be(1);
        report.Summary.TestClustersCount.Should().Be(0);
        report.Summary.CrossBoundaryClustersCount.Should().Be(0);
        report.Summary.SubMethodClustersCount.Should().Be(1);
        report.Summary.VerboseCommentsCount.Should().Be(1);
        report.Summary.TotalCommentGainCharacters.Should().Be(96);
        report.Summary.TotalCommentReductionPercent.Should().Be(80);
        report.Summary.TotalCommentGainTokens.Should().NotBeNull();
        report.Summary.TotalCommentGainTokens.Should().BeGreaterThan(0);

        // Clone Clusters
        report.CloneClusters.Should().HaveCount(1);
        var cluster = report.CloneClusters[0];
        cluster.ClusterId.Should().Be(1);
        cluster.Category.Should().Be("Production");
        cluster.ImpactScore.Should().Be(10);
        cluster.AstHash.Should().Be("astHashA");
        cluster.StatementCount.Should().Be(10);
        cluster.Methods.Should().HaveCount(2);
        cluster.Methods[0].FilePath.Should().Be("src/A.cs");
        cluster.Methods[0].MethodName.Should().Be("DoSomething");
        cluster.Methods[0].StartLine.Should().Be(10);
        cluster.Methods[0].EndLine.Should().Be(20);
        cluster.Recommendation.Should().Contain("Extract duplicate structural logic");

        // SubMethod Clusters
        report.SubMethodClusters.Should().HaveCount(1);
        var subCluster = report.SubMethodClusters[0];
        subCluster.ClusterId.Should().Be(1);
        subCluster.Category.Should().Be("Production");
        subCluster.StatementCount.Should().Be(15);
        subCluster.Occurrences.Should().HaveCount(2);
        subCluster.Occurrences[0].FilePath.Should().Be("src/SubA.cs");
        subCluster.Occurrences[0].MethodName.Should().Be("MethodA");
        subCluster.Occurrences[0].StartLine.Should().Be(50);
        subCluster.Occurrences[0].EndLine.Should().Be(65);
        subCluster.Recommendation.Should().Contain("Extract duplicate 15-statement block");

        // Verbose Comments
        report.VerboseComments.Should().HaveCount(1);
        var verbComment = report.VerboseComments[0];
        verbComment.FilePath.Should().Be("src/A.cs");
        verbComment.StartLine.Should().Be(1);
        verbComment.EndLine.Should().Be(5);
        verbComment.CharacterCount.Should().Be(120);
        verbComment.EstimatedTokens.Should().BeGreaterThan(0);
        verbComment.Kind.Should().Be("SingleLine");
        verbComment.Summary.Should().Be("Summarized comment text.");
        verbComment.GainCharacters.Should().Be(96);
        verbComment.GainTokens.Should().Be(report.Summary.TotalCommentGainTokens);
    }

    [Fact]
    public async Task FormatAsync_WithExistingFiles_PopulatesClonePotentialGainTokens()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "JsonGainTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var srcDir = Path.Combine(tempDir, "src");
            Directory.CreateDirectory(srcDir);

            var fileA = Path.Combine(srcDir, "A.cs");
            var fileB = Path.Combine(srcDir, "B.cs");
            File.WriteAllLines(fileA, Enumerable.Range(1, 10).Select(i => $"    var val{i} = ExecuteStep({i});"));
            File.WriteAllLines(fileB, Enumerable.Range(1, 10).Select(i => $"    var val{i} = ExecuteStep({i});"));

            var methods = new List<MethodCloneInfo>
            {
                new("src/A.cs", "DoStep", 1, 10, 10, "astHashA", "void DoStep()"),
                new("src/B.cs", "DoStep", 1, 10, 10, "astHashA", "void DoStep()")
            };
            var cloneGroup = new CodeCloneGroup("astHashA", methods, CloneCategory.Production, ImpactScore: 10);

            var occurrences = new List<SubMethodCloneOccurrence>
            {
                new("src/A.cs", "DoStep", 1, 5, 5),
                new("src/B.cs", "DoStep", 1, 5, 5)
            };
            var subGroup = new SubMethodCloneGroup("subHash1", occurrences, 5, CloneCategory.Production, ImpactScore: 5);

            var result = new AnalysisResult
            {
                AnalyzedFilesCount = 2,
                ProductionFilesCount = 2,
                TestFilesCount = 0,
                SkippedGeneratedFilesCount = 0,
                TotalMethodsScanned = 2,
                CloneGroups = [cloneGroup],
                SubMethodCloneGroups = [subGroup],
                VerboseComments = [],
                SummarizedComments = new Dictionary<string, string>()
            };

            using var writer = new StringWriter();
            await _formatter.FormatAsync(result, _settings, writer, tempDir);

            var json = writer.ToString();
            var report = JsonSerializer.Deserialize<CodeCondenserJsonReport>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            report.Should().NotBeNull();
            report!.Summary.TotalCloneGainTokens.Should().NotBeNull();
            report.Summary.TotalCloneGainTokens.Should().BeGreaterThan(0);

            report.CloneClusters.Should().HaveCount(1);
            report.CloneClusters[0].PotentialGainTokens.Should().NotBeNull();
            report.CloneClusters[0].PotentialGainTokens.Should().BeGreaterThan(0);

            report.SubMethodClusters.Should().HaveCount(1);
            report.SubMethodClusters[0].PotentialGainTokens.Should().NotBeNull();
            report.SubMethodClusters[0].PotentialGainTokens.Should().BeGreaterThan(0);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FormatAsync_RepeatedStrings_EmitsRepeatedStringClusters()
    {
        var longStr = "This is a repeated string that exceeds fifty characters in total length.";
        var occurrences = new List<RepeatedStringOccurrence>
        {
            new()
            {
                FilePath = "c:/repo/src/FileA.cs",
                StartLine = 10,
                EndLine = 10,
                StartColumn = 15,
                EndColumn = 87,
                StringValue = longStr,
                Category = FileCategory.Production
            },
            new()
            {
                FilePath = "c:/repo/src/FileB.cs",
                StartLine = 20,
                EndLine = 20,
                StartColumn = 15,
                EndColumn = 87,
                StringValue = longStr,
                Category = FileCategory.Production
            }
        };

        var group = new RepeatedStringGroup
        {
            Value = longStr,
            Occurrences = occurrences,
            Category = CloneCategory.Production,
            SuggestedConstantName = "TestMessage",
            SuggestedClassName = "MyConstants",
            TargetNamespace = "ApplicationNamespace"
        };

        var result = new AnalysisResult
        {
            AnalyzedFilesCount = 2,
            TotalMethodsScanned = 0,
            CloneGroups = [],
            VerboseComments = [],
            SummarizedComments = new Dictionary<string, string>(),
            RepeatedStringGroups = [group]
        };

        using var writer = new StringWriter();
        await _formatter.FormatAsync(result, _settings, writer, "c:/repo");

        var json = writer.ToString();
        var report = JsonSerializer.Deserialize<CodeCondenserJsonReport>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        report.Should().NotBeNull();
        report!.Summary.RepeatedStringsCount.Should().Be(1);
        report.Summary.TotalRepeatedStringGainCharacters.Should().Be(group.EstimateGainCharacters());
        report.RepeatedStrings.Should().HaveCount(1);

        var cluster = report.RepeatedStrings[0];
        cluster.ClusterId.Should().Be(1);
        cluster.Value.Should().Be(longStr);
        cluster.Length.Should().Be(longStr.Length);
        cluster.Occurrences.Should().HaveCount(2);
        cluster.Occurrences[0].FilePath.Should().Be("src/FileA.cs");
        cluster.Occurrences[0].StartLine.Should().Be(10);
        cluster.Occurrences[0].StartColumn.Should().Be(15);
        cluster.SuggestedConstantName.Should().Be("TestMessage");
        cluster.SuggestedClassName.Should().Be("MyConstants");
        cluster.HasExistingConstant.Should().BeFalse();
        cluster.ExistingConstant.Should().BeNull();
        cluster.Recommendation.Should().Contain("Extract repeated inline string literal");
    }

    [Fact]
    public async Task FormatAsync_ExistingConstant_EmitsExpectedJsonWithExistingConstantMetadata()
    {
        var str = "https://api.example.com/v1/resource";
        var constOcc = new RepeatedStringOccurrence
        {
            FilePath = "c:/repo/src/Routes.cs",
            StartLine = 8,
            EndLine = 8,
            StartColumn = 25,
            EndColumn = 25 + str.Length,
            StringValue = str,
            IsConstDeclaration = true,
            ConstantName = "ResourceUrl",
            ConstantTypeName = "Routes"
        };
        var inlineOcc = new RepeatedStringOccurrence
        {
            FilePath = "c:/repo/src/Client.cs",
            StartLine = 42,
            EndLine = 42,
            StartColumn = 15,
            EndColumn = 15 + str.Length,
            StringValue = str,
            IsConstDeclaration = false
        };

        var group = new RepeatedStringGroup
        {
            Value = str,
            Occurrences = [constOcc, inlineOcc],
            ExistingConstantDeclarations = [constOcc],
            Category = CloneCategory.Production,
            SuggestedConstantName = "ResourceUrl",
            SuggestedClassName = "Routes"
        };

        var result = new AnalysisResult
        {
            AnalyzedFilesCount = 2,
            TotalMethodsScanned = 0,
            CloneGroups = [],
            VerboseComments = [],
            SummarizedComments = new Dictionary<string, string>(),
            RepeatedStringGroups = [group]
        };

        using var writer = new StringWriter();
        await _formatter.FormatAsync(result, _settings, writer, "c:/repo");

        var json = writer.ToString();
        var report = JsonSerializer.Deserialize<CodeCondenserJsonReport>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        report.Should().NotBeNull();
        report!.RepeatedStrings.Should().HaveCount(1);

        var cluster = report.RepeatedStrings[0];
        cluster.HasExistingConstant.Should().BeTrue();
        cluster.ExistingConstant.Should().Be("Routes.ResourceUrl");
        cluster.Recommendation.Should().Be("Use existing constant 'Routes.ResourceUrl' instead of inline value.");
        cluster.Occurrences.Should().HaveCount(2);
        cluster.Occurrences[0].IsConstDeclaration.Should().BeTrue();
        cluster.Occurrences[1].IsConstDeclaration.Should().BeFalse();
    }

    [Fact]
    public async Task FormatAsync_TypeFileIssues_EmitsExpectedJson()
    {
        var primaryType = new ExtractedTypeInfo { TypeName = "Order", Kind = "class", LinesOfCode = 15, StartLine = 1, EndLine = 20, TargetFileName = "Order.cs", IsPrimary = true };
        var secondaryType = new ExtractedTypeInfo { TypeName = "OrderItem", Kind = "class", LinesOfCode = 8, StartLine = 22, EndLine = 35, TargetFileName = "OrderItem.cs" };

        var issue = new TypeFileAnalysis
        {
            FilePath = "c:/repo/src/Order.cs",
            Action = TypeFileAction.Split,
            SuggestedFileName = "Order.cs",
            Types = [primaryType, secondaryType],
            Category = FileCategory.Production
        };

        var result = new AnalysisResult
        {
            AnalyzedFilesCount = 1,
            TotalMethodsScanned = 0,
            CloneGroups = [],
            VerboseComments = [],
            SummarizedComments = new Dictionary<string, string>(),
            TypeFileIssues = [issue]
        };

        using var writer = new StringWriter();
        await _formatter.FormatAsync(result, _settings, writer, "c:/repo");

        var json = writer.ToString();
        var report = JsonSerializer.Deserialize<CodeCondenserJsonReport>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        report.Should().NotBeNull();
        report!.Summary.TypeFileIssuesCount.Should().Be(1);
        report.TypeFileIssues.Should().HaveCount(1);

        var jsonIssue = report.TypeFileIssues[0];
        jsonIssue.FilePath.Should().Be("src/Order.cs");
        jsonIssue.Action.Should().Be("Split");
        jsonIssue.SuggestedFileName.Should().Be("Order.cs");
        jsonIssue.PrimaryType.Should().Be("Order");
        jsonIssue.TypesToExtract.Should().HaveCount(1);
        jsonIssue.TypesToExtract[0].TypeName.Should().Be("OrderItem");
    }
}
