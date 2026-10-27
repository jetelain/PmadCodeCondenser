using AwesomeAssertions;
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
        var settings = new CodeCondenserSettings
        {
            RepeatedStringLengthThreshold = 50,
            ExistingConstantStringLengthThreshold = 50
        };

        var occurrences = _detector.ExtractStringLiterals(tree, "src/Service.cs", settings);

        occurrences.Should().BeEmpty();
    }

    [Fact]
    public void ExtractStringLiterals_InConstDeclaration_ExtractsConstantMetadata()
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

        occurrences.Should().HaveCount(1);
        var occ = occurrences[0];
        occ.IsConstDeclaration.Should().BeTrue();
        occ.ConstantName.Should().Be("ExistingConstant");
        occ.ConstantTypeName.Should().Be("MyConstants");
        occ.StringValue.Should().Be(longString);

        // When only constant declarations exist without any inline occurrences, no groups are formed
        var groups = _detector.GroupRepeatedStrings(occurrences, settings);
        groups.Should().BeEmpty();
    }

    [Fact]
    public void GroupRepeatedStrings_WhenExistingConstantExists_RecommendsExistingConstant()
    {
        var constCode = """
            namespace MyApp;

            public static class AppConstants
            {
                public const string ApiEndpoint = "https://api.example.com/v1/users";
            }
            """;
        var usageCode = """
            namespace MyApp;

            public class UserService
            {
                public void Fetch()
                {
                    var url = "https://api.example.com/v1/users";
                }
            }
            """;
        var constTree = CSharpSyntaxTree.ParseText(constCode);
        var usageTree = CSharpSyntaxTree.ParseText(usageCode);
        var settings = new CodeCondenserSettings
        {
            RepeatedStringLengthThreshold = 100, // String is 35 chars, well below 100!
            ExistingConstantStringLengthThreshold = 20 // Above existing constant threshold
        };

        var constOccs = _detector.ExtractStringLiterals(constTree, "src/AppConstants.cs", settings);
        var usageOccs = _detector.ExtractStringLiterals(usageTree, "src/UserService.cs", settings);

        var allOccs = constOccs.Concat(usageOccs).ToList();
        var groups = _detector.GroupRepeatedStrings(allOccs, settings);

        groups.Should().HaveCount(1);
        var group = groups[0];
        group.HasExistingConstant.Should().BeTrue();
        group.SuggestedConstantName.Should().Be("ApiEndpoint");
        group.SuggestedClassName.Should().Be("AppConstants");
        group.GenerateTypicalUsage().Should().Be("AppConstants.ApiEndpoint");
        group.InlineOccurrences.Should().HaveCount(1);
        group.InlineOccurrences[0].FilePath.Should().Be("src/UserService.cs");
        group.ExistingConstantDeclaration.Should().NotBeNull();
        group.ExistingConstantDeclaration!.FilePath.Should().Be("src/AppConstants.cs");
        group.ExistingConstantDeclaration.ConstantName.Should().Be("ApiEndpoint");
    }

    [Fact]
    public void GroupRepeatedStrings_WithoutExistingConstant_BelowNormalThreshold_IsIgnored()
    {
        // Two inline occurrences of a 35-char string without any constant declaration
        var code = """
            public class Service
            {
                public void M1() { var a = "https://api.example.com/v1/users"; }
                public void M2() { var b = "https://api.example.com/v1/users"; }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings
        {
            RepeatedStringLengthThreshold = 100,
            ExistingConstantStringLengthThreshold = 20
        };

        var occs = _detector.ExtractStringLiterals(tree, "src/Service.cs", settings);
        var groups = _detector.GroupRepeatedStrings(occs, settings);

        // Since no constant declaration exists and 35 < 100, no group is formed!
        groups.Should().BeEmpty();
    }

    [Fact]
    public void ExtractStringLiterals_NestedTypeConstant_CapturesFullTypeName()
    {
        var code = """
            public class OuterClass
            {
                public class InnerClass
                {
                    public const string NestedConst = "A string constant inside nested type hierarchy.";
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { ExistingConstantStringLengthThreshold = 20 };

        var occs = _detector.ExtractStringLiterals(tree, "src/Outer.cs", settings);
        occs.Should().HaveCount(1);
        occs[0].IsConstDeclaration.Should().BeTrue();
        occs[0].ConstantName.Should().Be("NestedConst");
        occs[0].ConstantTypeName.Should().Be("OuterClass.InnerClass");
    }

    [Fact]
    public void ExtractStringLiterals_StaticReadOnlyField_ExtractsAsConstant()
    {
        var code = """
            public class Config
            {
                public static readonly string DefaultKey = "This is a static readonly default key string.";
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { ExistingConstantStringLengthThreshold = 20 };

        var occs = _detector.ExtractStringLiterals(tree, "src/Config.cs", settings);
        occs.Should().HaveCount(1);
        occs[0].IsConstDeclaration.Should().BeTrue();
        occs[0].ConstantName.Should().Be("DefaultKey");
        occs[0].ConstantTypeName.Should().Be("Config");
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

    [Fact]
    public void ExtractStringLiterals_WithInlineTrailingComment_AreFilteredOut()
    {
        var longString = "This is a very long string that should be ignored by the trailing inline comment.";
        var code = $$"""
            public class Service
            {
                public void DoWork()
                {
                    var msg = "{{longString}}"; // codecondenser:ignore-string
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { RepeatedStringLengthThreshold = 50 };

        var occurrences = _detector.ExtractStringLiterals(tree, "src/Service.cs", settings);

        occurrences.Should().BeEmpty();
    }

    [Fact]
    public void ExtractStringLiterals_WithInlineFieldComment_AreFilteredOut()
    {
        var longString = "This is a very long string in a field that should be ignored by the inline nocondense comment.";
        var code = $$"""
            public class Service
            {
                private string _msg = "{{longString}}"; // nocondense
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { RepeatedStringLengthThreshold = 50 };

        var occurrences = _detector.ExtractStringLiterals(tree, "src/Service.cs", settings);

        occurrences.Should().BeEmpty();
    }

    [Fact]
    public void ExtractStringLiterals_WithInlineBlockComment_AreFilteredOut()
    {
        var longString = "This is a very long string with a trailing block comment that suppresses analysis.";
        var code = $$"""
            public class Service
            {
                public void DoWork()
                {
                    var msg = "{{longString}}"; /* codecondenser:ignore-string */
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { RepeatedStringLengthThreshold = 50 };

        var occurrences = _detector.ExtractStringLiterals(tree, "src/Service.cs", settings);

        occurrences.Should().BeEmpty();
    }

    [Fact]
    public void ExtractStringLiterals_CapturesEnclosingNamespace()
    {
        var longString = "This is a very long string inside a specific namespace declaration.";
        var code = $$"""
            namespace Company.Product.Feature;

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

        var occurrences = _detector.ExtractStringLiterals(tree, "src/Service.cs", settings);

        occurrences.Should().HaveCount(1);
        occurrences[0].Namespace.Should().Be("Company.Product.Feature");

        var groups = _detector.GroupRepeatedStrings([occurrences[0], occurrences[0]], settings);
        groups.Should().HaveCount(1);
        groups[0].TargetNamespace.Should().Be("Company.Product.Feature");
    }

    [Fact]
    public void Group_GenerateMinimalDeclaration_EscapesControlCharacters()
    {
        var specialString = "Line1\r\nLine2\tTabbed\0Null\bBack\fForm\vVert\"Quotes\"\\Backslash";
        var occ1 = CreateOccurrence("src/A.cs", specialString, "M1");
        var occ2 = CreateOccurrence("src/B.cs", specialString, "M2");

        var group = new RepeatedStringGroup
        {
            Value = specialString,
            Occurrences = [occ1, occ2],
            Category = CloneCategory.Production,
            SuggestedConstantName = "SpecialConstant",
            SuggestedClassName = "MyConstants",
            TargetNamespace = "TestNamespace"
        };

        var decl = group.GenerateMinimalDeclaration();
        decl.Should().Contain("\\0");
        decl.Should().Contain("\\b");
        decl.Should().Contain("\\f");
        decl.Should().Contain("\\v");
        decl.Should().Contain("\\r\\n");
        decl.Should().Contain("\\t");
        decl.Should().Contain("\\\"Quotes\\\"");
        decl.Should().Contain("\\\\Backslash");
    }
}
