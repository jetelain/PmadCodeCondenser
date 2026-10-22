using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Core.Strings;

namespace Pmad.CodeCondenser.Core.Tests.Strings;

public class RepeatedStringDetectorTests
{
    private readonly RepeatedStringDetector _detector = new();

    private static RepeatedStringOccurrence CreateOccurrence(string path, string val, string member) =>
        new()
        {
            FilePath = path,
            StartLine = 10,
            EndLine = 10,
            StartColumn = 15,
            EndColumn = val.Length + 17,
            StringValue = val,
            EnclosingMember = member,
            ProjectId = "Project1",
            Category = FileCategory.Production
        };

    [Fact]
    public void ExtractStringLiterals_QualifyingString_ExtractsAccurateCoordinatesAndMetadata()
    {
        var longString = "This is a very long string that easily exceeds fifty characters in total length for testing.";
        var code = $$"""
            namespace MyApp;

            public class Service
            {
                public void DoWork()
                {
                    var msg = "{{longString}}";
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { RepeatedStringLengthThreshold = 50 };

        var occurrences = _detector.ExtractStringLiterals(tree, "src/MyApp/Service.cs", settings);

        occurrences.Should().HaveCount(1);
        var occ = occurrences[0];
        occ.FilePath.Should().Be("src/MyApp/Service.cs");
        occ.StringValue.Should().Be(longString);
        occ.EnclosingMember.Should().Be("DoWork");
        occ.Category.Should().Be(FileCategory.Production);
        occ.StartLine.Should().Be(7);
        occ.StartColumn.Should().BeGreaterThan(0);
        occ.EndColumn.Should().BeGreaterThan(occ.StartColumn);
    }

    [Fact]
    public void ExtractStringLiterals_BelowThreshold_AreFilteredOut()
    {
        var code = """
            public class Service
            {
                public void DoWork()
                {
                    var shortStr = "Short string under fifty characters.";
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { RepeatedStringLengthThreshold = 50 };

        var occurrences = _detector.ExtractStringLiterals(tree, "src/Service.cs", settings);

        occurrences.Should().BeEmpty();
    }

    [Fact]
    public void ExtractStringLiterals_InConstDeclaration_AreFilteredOut()
    {
        var longString = "This is a very long constant string that exceeds fifty characters in length.";
        var code = $$"""
            public static class MyConstants
            {
                public const string ExistingConstant = "{{longString}}";
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { RepeatedStringLengthThreshold = 50 };

        var occurrences = _detector.ExtractStringLiterals(tree, "src/MyConstants.cs", settings);

        occurrences.Should().BeEmpty();
    }

    [Fact]
    public void ExtractStringLiterals_WithIgnoreComment_AreFilteredOut()
    {
        var longString = "This is a very long string that should be ignored by the analyzer comment.";
        var code = $$"""
            public class Service
            {
                public void DoWork()
                {
                    // codecondenser:ignore-string
                    var msg = "{{longString}}";
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { RepeatedStringLengthThreshold = 50 };

        var occurrences = _detector.ExtractStringLiterals(tree, "src/Service.cs", settings);

        occurrences.Should().BeEmpty();
    }

    [Fact]
    public void ExtractStringLiterals_WithSuppressMessageAttribute_AreFilteredOut()
    {
        var longString = "This is a very long string inside a method suppressed by SuppressMessage.";
        var code = $$"""
            using System.Diagnostics.CodeAnalysis;

            public class Service
            {
                [SuppressMessage("CodeCondenser", "RepeatedString")]
                public void DoWork()
                {
                    var msg = "{{longString}}";
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { RepeatedStringLengthThreshold = 50 };

        var occurrences = _detector.ExtractStringLiterals(tree, "src/Service.cs", settings);

        occurrences.Should().BeEmpty();
    }

    [Fact]
    public void ExtractStringLiterals_RawStringLiteral_IsExtracted()
    {
        var code = """"
            public class Service
            {
                public void DoWork()
                {
                    var msg = """
                        Line 1 of long text
                        Line 2 of long text that exceeds fifty chars.
                        """;
                }
            }
            """";
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { RepeatedStringLengthThreshold = 50 };

        var occurrences = _detector.ExtractStringLiterals(tree, "src/Service.cs", settings);

        occurrences.Should().HaveCount(1);
        occurrences[0].StringValue.Length.Should().BeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public void GroupRepeatedStrings_GroupsIdenticalStringsAcrossFiles()
    {
        var longString = "Repeated long string across multiple locations that exceeds fifty characters.";
        var occ1 = CreateOccurrence("src/FileA.cs", longString, "MethodA");
        var occ2 = CreateOccurrence("src/FileB.cs", longString, "MethodB");
        var uniqueString = "Another unique string exceeding fifty characters in length here!";
        var occ3 = CreateOccurrence("src/FileC.cs", uniqueString, "MethodC");

        var settings = new CodeCondenserSettings
        {
            RepeatedStringLengthThreshold = 50,
            RepeatedStringMinOccurrences = 2
        };

        var groups = _detector.GroupRepeatedStrings([occ1, occ2, occ3], settings);

        groups.Should().HaveCount(1);
        var group = groups[0];
        group.Value.Should().Be(longString);
        group.Occurrences.Should().HaveCount(2);
        group.Category.Should().Be(CloneCategory.Production);
        group.SuggestedClassName.Should().Be("MyConstants");
        group.SuggestedConstantName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Group_GenerateMinimalDeclaration_MatchesUserSpecification()
    {
        var longString = "This is a long test string for generating the declaration.";
        var occ1 = CreateOccurrence("src/A.cs", longString, "M1");
        var occ2 = CreateOccurrence("src/B.cs", longString, "M2");

        var group = new RepeatedStringGroup
        {
            Value = longString,
            Occurrences = [occ1, occ2],
            Category = CloneCategory.Production,
            SuggestedConstantName = "TestConstant",
            SuggestedClassName = "MyConstants",
            TargetNamespace = "ApplicationNamespace"
        };

        var decl = group.GenerateMinimalDeclaration();
        decl.Should().Contain("namespace ApplicationNamespace;");
        decl.Should().Contain("static class MyConstants");
        decl.Should().Contain("internal const string TestConstant = \"");

        var usage = group.GenerateTypicalUsage();
        usage.Should().Be("MyConstants.TestConstant");
    }
}
