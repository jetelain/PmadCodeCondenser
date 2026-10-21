using System.Text.Json;
using FluentAssertions;
using Pmad.CodeCondenser.Cli.Formatting.Sarif;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Cli.Tests;

public class SarifReportFormatterTests
{
    private readonly SarifReportFormatter _formatter = new();
    private readonly CodeCondenserSettings _settings = new();

    [Fact]
    public async Task FormatAsync_EmptyResult_EmitsValidSarifWithToolRules()
    {
        var result = new AnalysisResult
        {
            AnalyzedFilesCount = 5,
            TotalMethodsScanned = 20,
            ProductionFilesCount = 4,
            TestFilesCount = 1,
            SkippedGeneratedFilesCount = 0,
            CloneGroups = [],
            SubMethodCloneGroups = [],
            VerboseComments = [],
            SummarizedComments = new Dictionary<string, string>()
        };

        using var writer = new StringWriter();
        await _formatter.FormatAsync(result, _settings, writer, "c:/repo");

        var json = writer.ToString();
        var sarif = JsonSerializer.Deserialize<SarifLog>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        sarif.Should().NotBeNull();
        sarif!.Schema.Should().Be("https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json");
        sarif.Version.Should().Be("2.1.0");
        sarif.Runs.Should().HaveCount(1);

        var run = sarif.Runs[0];
        run.Tool.Driver.Name.Should().Be("Pmad.CodeCondenser");
        run.Tool.Driver.Version.Should().NotBeNullOrWhiteSpace();
        run.Tool.Driver.InformationUri.Should().Be("https://github.com/jetelain/PmadCodeCondenser");
        run.Tool.Driver.Rules.Should().HaveCount(5);
        run.Tool.Driver.Rules.Select(r => r.Id).Should().Contain(["CC001", "CC002", "CC003", "CC004", "CC005"]);
        run.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task FormatAsync_ProductionCloneGroup_EmitsSymmetricalResultsWithFingerprints()
    {
        var methods = new List<MethodCloneInfo>
        {
            new("c:/repo/src/Services/OrderService.cs", "SaveOrderAsync", 10, 25, 12, "hash123", "void SaveOrderAsync()"),
            new("c:/repo/src/Services/InvoiceService.cs", "SaveInvoiceAsync", 30, 45, 12, "hash123", "void SaveInvoiceAsync()")
        };
        var cloneGroup = new CodeCloneGroup("hash123", methods, CloneCategory.Production, ImpactScore: 12);

        var result = new AnalysisResult
        {
            AnalyzedFilesCount = 2,
            TotalMethodsScanned = 10,
            CloneGroups = [cloneGroup],
            SubMethodCloneGroups = [],
            VerboseComments = [],
            SummarizedComments = new Dictionary<string, string>()
        };

        using var writer = new StringWriter();
        await _formatter.FormatAsync(result, _settings, writer, "c:/repo");

        var sarif = JsonSerializer.Deserialize<SarifLog>(writer.ToString(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        sarif!.Runs[0].Results.Should().HaveCount(2);

        var res1 = sarif.Runs[0].Results[0];
        var res2 = sarif.Runs[0].Results[1];

        // Symmetrical Rule and Level
        res1.RuleId.Should().Be("CC001");
        res1.Level.Should().Be("warning");
        res2.RuleId.Should().Be("CC001");
        res2.Level.Should().Be("warning");

        // Symmetrical Correlation GUID
        res1.CorrelationGuid.Should().NotBeNullOrWhiteSpace();
        res1.CorrelationGuid.Should().Be(res2.CorrelationGuid);

        // Location 1 -> OrderService, related -> InvoiceService
        res1.Locations[0].PhysicalLocation.ArtifactLocation.Uri.Should().Be("src/Services/OrderService.cs");
        res1.Locations[0].PhysicalLocation.Region.StartLine.Should().Be(10);
        res1.Locations[0].PhysicalLocation.Region.EndLine.Should().Be(25);
        res1.RelatedLocations.Should().HaveCount(1);
        res1.RelatedLocations![0].PhysicalLocation.ArtifactLocation.Uri.Should().Be("src/Services/InvoiceService.cs");
        res1.RelatedLocations[0].Message.Text.Should().Contain("InvoiceService.cs");

        // Partial fingerprints for alert deduplication
        res1.PartialFingerprints.Should().ContainKey("astHash/v1").WhoseValue.Should().Be("hash123");
        res1.PartialFingerprints.Should().ContainKey("locationKey/v1").WhoseValue.Should().Be("src/Services/OrderService.cs:SaveOrderAsync");

        // Location 2 -> InvoiceService, related -> OrderService
        res2.Locations[0].PhysicalLocation.ArtifactLocation.Uri.Should().Be("src/Services/InvoiceService.cs");
        res2.Locations[0].PhysicalLocation.Region.StartLine.Should().Be(30);
        res2.Locations[0].PhysicalLocation.Region.EndLine.Should().Be(45);
        res2.RelatedLocations.Should().HaveCount(1);
        res2.RelatedLocations![0].PhysicalLocation.ArtifactLocation.Uri.Should().Be("src/Services/OrderService.cs");
        res2.RelatedLocations[0].Message.Text.Should().Contain("OrderService.cs");
        res2.PartialFingerprints!["locationKey/v1"].Should().Be("src/Services/InvoiceService.cs:SaveInvoiceAsync");
    }

    [Fact]
    public async Task FormatAsync_TestAndCrossBoundaryClones_MapToCorrectRulesAndLevels()
    {
        var testMethods = new List<MethodCloneInfo>
        {
            new("c:/repo/tests/TestA.cs", "Test1", 5, 15, 8, "testHash", "void Test1()"),
            new("c:/repo/tests/TestB.cs", "Test2", 5, 15, 8, "testHash", "void Test2()")
        };
        var crossMethods = new List<MethodCloneInfo>
        {
            new("c:/repo/src/App.cs", "Run", 1, 10, 8, "crossHash", "void Run()"),
            new("c:/repo/tests/AppTests.cs", "RunTest", 1, 10, 8, "crossHash", "void RunTest()")
        };

        var result = new AnalysisResult
        {
            AnalyzedFilesCount = 4,
            TotalMethodsScanned = 10,
            CloneGroups =
            [
                new CodeCloneGroup("testHash", testMethods, CloneCategory.Test),
                new CodeCloneGroup("crossHash", crossMethods, CloneCategory.CrossBoundary)
            ],
            SubMethodCloneGroups = [],
            VerboseComments = [],
            SummarizedComments = new Dictionary<string, string>()
        };

        using var writer = new StringWriter();
        await _formatter.FormatAsync(result, _settings, writer, "c:/repo");

        var sarif = JsonSerializer.Deserialize<SarifLog>(writer.ToString(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var results = sarif!.Runs[0].Results;

        // 2 results for test cluster + 2 results for cross cluster = 4 results
        results.Should().HaveCount(4);

        results.Where(r => r.RuleId == "CC002").Should().AllSatisfy(r => r.Level.Should().Be("note"));
        results.Where(r => r.RuleId == "CC004").Should().AllSatisfy(r => r.Level.Should().Be("warning"));
    }

    [Fact]
    public async Task FormatAsync_SubMethodClones_EmitsCC005Results()
    {
        var occurrences = new List<SubMethodCloneOccurrence>
        {
            new("c:/repo/src/Service.cs", "ProcessA", 100, 115, 15),
            new("c:/repo/src/Service.cs", "ProcessB", 200, 215, 15)
        };
        var subGroup = new SubMethodCloneGroup("subHash", occurrences, 15, CloneCategory.Production, ImpactScore: 15);

        var result = new AnalysisResult
        {
            AnalyzedFilesCount = 1,
            TotalMethodsScanned = 2,
            CloneGroups = [],
            SubMethodCloneGroups = [subGroup],
            VerboseComments = [],
            SummarizedComments = new Dictionary<string, string>()
        };

        using var writer = new StringWriter();
        await _formatter.FormatAsync(result, _settings, writer, "c:/repo");

        var sarif = JsonSerializer.Deserialize<SarifLog>(writer.ToString(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        sarif!.Runs[0].Results.Should().HaveCount(2);

        var res = sarif.Runs[0].Results[0];
        res.RuleId.Should().Be("CC005");
        res.Level.Should().Be("warning");
        res.Locations[0].PhysicalLocation.Region.StartLine.Should().Be(100);
        res.Locations[0].PhysicalLocation.Region.EndLine.Should().Be(115);
        res.PartialFingerprints.Should().ContainKey("locationKey/v1").WhoseValue.Should().Be("src/Service.cs:ProcessA:100");
        res.RelatedLocations.Should().HaveCount(1);
        res.RelatedLocations![0].PhysicalLocation.Region.StartLine.Should().Be(200);
        res.RelatedLocations[0].Message.Text.Should().Be("Duplicate statement block in Service.cs:'ProcessB' (lines 200-215)");
    }

    [Fact]
    public async Task FormatAsync_VerboseComments_EmitsCC003Results()
    {
        var comment = new VerboseComment(
            FilePath: "c:/repo/src/Helper.cs",
            StartLine: 42,
            EndLine: 50,
            SpanStart: 100,
            SpanLength: 300,
            Kind: CommentKind.SingleLine,
            OriginalText: new string('x', 250),
            LeadingWhitespace: "    ");

        var result = new AnalysisResult
        {
            AnalyzedFilesCount = 1,
            TotalMethodsScanned = 1,
            CloneGroups = [],
            SubMethodCloneGroups = [],
            VerboseComments = [comment],
            SummarizedComments = new Dictionary<string, string>()
        };

        using var writer = new StringWriter();
        await _formatter.FormatAsync(result, _settings, writer, "c:/repo");

        var sarif = JsonSerializer.Deserialize<SarifLog>(writer.ToString(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        sarif!.Runs[0].Results.Should().HaveCount(1);

        var res = sarif.Runs[0].Results[0];
        res.RuleId.Should().Be("CC003");
        res.Level.Should().Be("note");
        res.Locations[0].PhysicalLocation.ArtifactLocation.Uri.Should().Be("src/Helper.cs");
        res.Locations[0].PhysicalLocation.Region.StartLine.Should().Be(42);
        res.Locations[0].PhysicalLocation.Region.EndLine.Should().Be(50);
        res.PartialFingerprints.Should().ContainKey("commentHash/v1");
        res.PartialFingerprints.Should().ContainKey("locationKey/v1");
    }
}
