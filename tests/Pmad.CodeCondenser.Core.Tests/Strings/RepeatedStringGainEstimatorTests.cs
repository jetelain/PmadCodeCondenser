using AwesomeAssertions;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Core.Strings;

namespace Pmad.CodeCondenser.Core.Tests.Strings;

public class RepeatedStringGainEstimatorTests
{
    private static RepeatedStringOccurrence CreateOccurrence(string path, string val) =>
        new()
        {
            FilePath = path,
            StartLine = 1,
            EndLine = 1,
            StartColumn = 1,
            EndColumn = val.Length + 2,
            StringValue = val,
            EnclosingMember = "M",
            ProjectId = "P",
            Category = FileCategory.Production
        };

    [Fact]
    public void EstimateGainCharacters_PreExistingClass_ComputesExpectedFormula()
    {
        var str = new string('A', 50); // 50 chars
        var occ1 = CreateOccurrence("a.cs", str);
        var occ2 = CreateOccurrence("b.cs", str);

        var group = new RepeatedStringGroup
        {
            Value = str,
            Occurrences = [occ1, occ2],
            Category = CloneCategory.Production,
            SuggestedConstantName = "ConstA"
        };

        // count = 2
        // currentChars = 2 * (50 + 2) = 104
        // newCost = 60 + 50 + 2 + (2 * 30) = 112 + 60 = 172
        // currentChars - newCost = 104 - 172 = -68
        int gain2 = group.EstimateGainCharacters(preExistingClass: true);
        gain2.Should().Be(104 - 172);

        var occ3 = CreateOccurrence("c.cs", str);
        var occ4 = CreateOccurrence("d.cs", str);
        var occ5 = CreateOccurrence("e.cs", str);
        var group5 = new RepeatedStringGroup
        {
            Value = str,
            Occurrences = [occ1, occ2, occ3, occ4, occ5],
            Category = CloneCategory.Production,
            SuggestedConstantName = "ConstA"
        };
        // count = 5
        // currentChars = 5 * 52 = 260
        // newCost = 60 + 52 + (5 * 30) = 262
        group5.EstimateGainCharacters(preExistingClass: true).Should().Be(260 - 262);
    }

    [Fact]
    public void EstimateGainCharacters_WithoutPreExistingClass_Adds120Overhead()
    {
        var str = new string('B', 100);
        var occurrences = Enumerable.Range(1, 5)
            .Select(i => CreateOccurrence($"file{i}.cs", str))
            .ToList();

        var group = new RepeatedStringGroup
        {
            Value = str,
            Occurrences = occurrences,
            Category = CloneCategory.Production,
            SuggestedConstantName = "ConstB"
        };

        // count = 5, len = 100
        // currentChars = 5 * (100 + 2) = 510
        // with pre-existing class: 60 + 102 + (5 * 30) = 312 -> gain = 198
        // without pre-existing class: 120 + 102 + (5 * 30) = 372 -> gain = 138
        group.EstimateGainCharacters(preExistingClass: true).Should().Be(510 - 312);
        group.EstimateGainCharacters(preExistingClass: false).Should().Be(510 - 372);
    }

    [Fact]
    public void EstimateGainTokens_ComputesPositiveSavingsForLongRepeatedStrings()
    {
        var longText = "SELECT CustomerId, OrderId, OrderDate, TotalAmount, Status FROM Orders WHERE Status = 'Pending' ORDER BY OrderDate DESC";
        var occurrences = Enumerable.Range(1, 4)
            .Select(i => CreateOccurrence($"repo/file{i}.cs", longText))
            .ToList();

        var group = new RepeatedStringGroup
        {
            Value = longText,
            Occurrences = occurrences,
            Category = CloneCategory.Production,
            SuggestedConstantName = "OrdersQuery"
        };

        int tokenGain = group.EstimateGainTokens();
        tokenGain.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RepeatedStringGainEstimator_CalculatesAggregateGains()
    {
        var str1 = new string('X', 120);
        var occurrences1 = Enumerable.Range(1, 5)
            .Select(i => CreateOccurrence($"file{i}.cs", str1))
            .ToList();
        var group1 = new RepeatedStringGroup
        {
            Value = str1,
            Occurrences = occurrences1,
            Category = CloneCategory.Production,
            SuggestedConstantName = "Const1"
        };

        var str2 = new string('Y', 80);
        var occurrences2 = Enumerable.Range(1, 3)
            .Select(i => CreateOccurrence($"file{i}.cs", str2))
            .ToList();
        var group2 = new RepeatedStringGroup
        {
            Value = str2,
            Occurrences = occurrences2,
            Category = CloneCategory.Production,
            SuggestedConstantName = "Const2"
        };

        int totalChars = RepeatedStringGainEstimator.EstimateTotalGainCharacters([group1, group2]);
        int expectedChars = group1.EstimateGainCharacters() + group2.EstimateGainCharacters();
        totalChars.Should().Be(expectedChars);

        int totalTokens = RepeatedStringGainEstimator.EstimateTotalGainTokens([group1, group2]);
        int expectedTokens = group1.EstimateGainTokens() + group2.EstimateGainTokens();
        totalTokens.Should().Be(expectedTokens);
    }

    [Fact]
    public void EstimateGainCharacters_WithExistingConstant_CalculatesSavingsWithoutDeclarationOverhead()
    {
        var str = "https://api.example.com/v1/customers"; // 36 chars
        var constOcc = new RepeatedStringOccurrence
        {
            FilePath = "src/ApiRoutes.cs",
            StartLine = 5,
            EndLine = 5,
            StartColumn = 10,
            EndColumn = 46,
            StringValue = str,
            IsConstDeclaration = true,
            ConstantName = "CustomersEndpoint",
            ConstantTypeName = "ApiRoutes"
        };
        var inlineOcc1 = CreateOccurrence("src/ClientA.cs", str);
        var inlineOcc2 = CreateOccurrence("src/ClientB.cs", str);

        var group = new RepeatedStringGroup
        {
            Value = str,
            Occurrences = [constOcc, inlineOcc1, inlineOcc2],
            ExistingConstantDeclarations = [constOcc],
            SuggestedConstantName = "CustomersEndpoint",
            SuggestedClassName = "ApiRoutes"
        };

        group.HasExistingConstant.Should().BeTrue();
        // 2 inline occurrences
        // Current chars: 2 * (36 + 2) = 76
        // Usage: "ApiRoutes.CustomersEndpoint" = 27 chars -> 2 * 27 = 54
        // Expected gain: 76 - 54 = 22
        group.EstimateGainCharacters().Should().Be(22);
    }

    [Fact]
    public void EstimateGainTokens_WithExistingConstant_CalculatesTokensSavedWithoutDeclarationOverhead()
    {
        var longText = "SELECT CustomerId, OrderId, OrderDate, TotalAmount, Status FROM Orders WHERE Status = 'Pending' ORDER BY OrderDate DESC";
        var constOcc = new RepeatedStringOccurrence
        {
            FilePath = "src/Queries.cs",
            StartLine = 10,
            EndLine = 10,
            StartColumn = 10,
            EndColumn = 100,
            StringValue = longText,
            IsConstDeclaration = true,
            ConstantName = "PendingOrdersQuery",
            ConstantTypeName = "Queries"
        };
        var inlineOcc = CreateOccurrence("src/Service.cs", longText);

        var group = new RepeatedStringGroup
        {
            Value = longText,
            Occurrences = [constOcc, inlineOcc],
            ExistingConstantDeclarations = [constOcc],
            SuggestedConstantName = "PendingOrdersQuery",
            SuggestedClassName = "Queries"
        };

        int gainTokens = group.EstimateGainTokens();
        gainTokens.Should().BeGreaterThan(0);
    }
}
