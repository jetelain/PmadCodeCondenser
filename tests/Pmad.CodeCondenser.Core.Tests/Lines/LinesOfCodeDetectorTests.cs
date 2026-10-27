using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Lines;
using Pmad.CodeCondenser.Core.Models;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Lines;

public class LinesOfCodeDetectorTests
{
    private readonly LinesOfCodeDetector _detector = new();

    private static (Microsoft.CodeAnalysis.SyntaxTree tree, Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax root) Parse(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return (tree, tree.GetCompilationUnitRoot());
    }

    [Fact]
    public void DetectViolations_MethodBelowThreshold_ReturnsEmpty()
    {
        var code = """
            public class Service
            {
                public void DoWork()
                {
                    int x = 1;
                    int y = 2;
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 10,
            ClassLinesOfCodeThreshold = 100
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/Service.cs", settings, FileCategory.Production);

        Assert.Empty(violations);
    }

    [Fact]
    public void DetectViolations_MethodExceedingThreshold_ReturnsViolation()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 15).Select(i => $"        int x{i} = {i};"));
        var code = $$"""
            public class Service
            {
                public void BigMethod()
                {
            {{lines}}
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 10,
            ClassLinesOfCodeThreshold = 100
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/Service.cs", settings, FileCategory.Production);

        Assert.Single(violations);
        var v = violations[0];
        Assert.Equal(LinesOfCodeKind.Method, v.Kind);
        Assert.Equal("Service.BigMethod()", v.Identifier);
        Assert.Equal("c:/repo/Service.cs", v.FilePath);
        Assert.True(v.LinesOfCode > 10);
        Assert.Equal(10, v.Threshold);
        Assert.Equal("c:/repo/Service.cs::Method::Service.BigMethod()", v.Key);
    }

    [Fact]
    public void DetectViolations_ExcludesCommentsAndBlankLinesFromLocCount()
    {
        // 4 lines of code, but 20 lines of comments and blank lines
        var code = """
            public class Service
            {
                /// <summary>
                /// Doc comment line 1
                /// Doc comment line 2
                /// </summary>
                public void CommentedMethod()
                {
                    // Regular comment 1
                    // Regular comment 2
                    /*
                       Block comment
                    */

                    int a = 1;

                    // Another comment
                    int b = 2;
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 6,
            ClassLinesOfCodeThreshold = 100
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/Service.cs", settings, FileCategory.Production);

        // Actual LOC inside method: '{', 'int a = 1;', 'int b = 2;', '}' -> 4-5 lines of code <= threshold 6
        Assert.Empty(violations);
    }

    [Fact]
    public void DetectViolations_ClassExceedingThreshold_ReturnsViolation()
    {
        var methods = string.Join("\n\n", Enumerable.Range(1, 10).Select(i => $"    public void Method{i}() {{ int x = {i}; }}"));
        var code = $$"""
            public class LargeClass
            {
            {{methods}}
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 50,
            ClassLinesOfCodeThreshold = 10
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/LargeClass.cs", settings, FileCategory.Production);

        Assert.Single(violations);
        var v = violations[0];
        Assert.Equal(LinesOfCodeKind.Class, v.Kind);
        Assert.Equal("LargeClass", v.Identifier);
        Assert.Equal("c:/repo/LargeClass.cs", v.FilePath);
        Assert.True(v.LinesOfCode > 10);
        Assert.Equal(10, v.Threshold);
    }

    [Fact]
    public void DetectViolations_RecordClassExceedingThreshold_ReturnsViolation()
    {
        var methods = string.Join("\n\n", Enumerable.Range(1, 10).Select(i => $"    public void Method{i}() {{ int x = {i}; }}"));
        var code = $$"""
            public record class LargeRecord
            {
            {{methods}}
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 50,
            ClassLinesOfCodeThreshold = 10
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/LargeRecord.cs", settings, FileCategory.Production);

        Assert.Single(violations);
        var v = violations[0];
        Assert.Equal(LinesOfCodeKind.Class, v.Kind);
        Assert.Equal("LargeRecord", v.Identifier);
    }

    [Fact]
    public void DetectViolations_ConstructorExceedingThreshold_ReturnsViolation()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 15).Select(i => $"        int x{i} = {i};"));
        var code = $$"""
            public class Service
            {
                public Service()
                {
            {{lines}}
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 10,
            ClassLinesOfCodeThreshold = 100
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/Service.cs", settings, FileCategory.Production);

        Assert.Single(violations);
        var v = violations[0];
        Assert.Equal(LinesOfCodeKind.Method, v.Kind);
        Assert.Equal("Service.Service()", v.Identifier);
    }

    [Fact]
    public void DetectViolations_PropertyAccessorExceedingThreshold_ReturnsViolation()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 15).Select(i => $"            int x{i} = {i};"));
        var code = $$"""
            public class Service
            {
                public int Value
                {
                    get
                    {
            {{lines}}
                        return 0;
                    }
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 10,
            ClassLinesOfCodeThreshold = 100
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/Service.cs", settings, FileCategory.Production);

        Assert.Single(violations);
        var v = violations[0];
        Assert.Equal(LinesOfCodeKind.Method, v.Kind);
        Assert.Equal("Service.Value.get", v.Identifier);
    }

    [Theory]
    [InlineData("// codecondenser:ignore-loc")]
    [InlineData("// codecondenser:ignore-length")]
    [InlineData("// codecondenser:ignore-lines")]
    [InlineData("// noloc")]
    public void DetectViolations_CommentSuppressionOnMethod_Ignored(string suppressionComment)
    {
        var lines = string.Join("\n", Enumerable.Range(1, 15).Select(i => $"        int x{i} = {i};"));
        var code = $$"""
            public class Service
            {
                {{suppressionComment}}
                public void BigMethod()
                {
            {{lines}}
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 10,
            ClassLinesOfCodeThreshold = 100
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/Service.cs", settings, FileCategory.Production);

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("// codecondenser:ignore-loc")]
    [InlineData("// codecondenser:ignore-length")]
    [InlineData("// noloc")]
    public void DetectViolations_CommentSuppressionOnClass_Ignored(string suppressionComment)
    {
        var methods = string.Join("\n\n", Enumerable.Range(1, 10).Select(i => $"    public void Method{i}() {{ int x = {i}; }}"));
        var code = $$"""
            {{suppressionComment}}
            public class LargeClass
            {
            {{methods}}
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 50,
            ClassLinesOfCodeThreshold = 10
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/LargeClass.cs", settings, FileCategory.Production);

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("MethodLength")]
    [InlineData("LinesOfCode")]
    [InlineData("MemberLength")]
    public void DetectViolations_SuppressMessageAttributeOnMethod_Ignored(string checkId)
    {
        var lines = string.Join("\n", Enumerable.Range(1, 15).Select(i => $"        int x{i} = {i};"));
        var code = $$"""
            using System.Diagnostics.CodeAnalysis;

            public class Service
            {
                [SuppressMessage("CodeCondenser", "{{checkId}}")]
                public void BigMethod()
                {
            {{lines}}
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 10,
            ClassLinesOfCodeThreshold = 100
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/Service.cs", settings, FileCategory.Production);

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("ClassLength")]
    [InlineData("TypeLength")]
    [InlineData("LinesOfCode")]
    public void DetectViolations_SuppressMessageAttributeOnClass_Ignored(string checkId)
    {
        var methods = string.Join("\n\n", Enumerable.Range(1, 10).Select(i => $"    public void Method{i}() {{ int x = {i}; }}"));
        var code = $$"""
            using System.Diagnostics.CodeAnalysis;

            [SuppressMessage("CodeCondenser", "{{checkId}}")]
            public class LargeClass
            {
            {{methods}}
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 50,
            ClassLinesOfCodeThreshold = 10
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/LargeClass.cs", settings, FileCategory.Production);

        Assert.Empty(violations);
    }

    [Fact]
    public void DetectViolations_GeneratedCodeFileHeader_ReturnsEmpty()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"        int x{i} = {i};"));
        var code = $$"""
            // <auto-generated>
            // This code was generated by a tool.
            // </auto-generated>

            public class GeneratedClass
            {
                public void BigMethod()
                {
            {{lines}}
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 10,
            ClassLinesOfCodeThreshold = 15
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/GeneratedClass.cs", settings, FileCategory.Production);

        Assert.Empty(violations);
    }

    [Fact]
    public void DetectViolations_GeneratedCodeAttributeOnClass_ReturnsEmpty()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"        int x{i} = {i};"));
        var code = $$"""
            using System.CodeDom.Compiler;

            [GeneratedCode("Tool", "1.0")]
            public class GeneratedClass
            {
                public void BigMethod()
                {
            {{lines}}
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 10,
            ClassLinesOfCodeThreshold = 15
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/GeneratedClass.cs", settings, FileCategory.Production);

        Assert.Empty(violations);
    }

    [Fact]
    public void DetectViolations_DisabledDetection_ReturnsEmpty()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"        int x{i} = {i};"));
        var code = $$"""
            public class BigClass
            {
                public void BigMethod()
                {
            {{lines}}
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            EnableLinesOfCodeDetection = false,
            MethodLinesOfCodeThreshold = 5,
            ClassLinesOfCodeThreshold = 5
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/BigClass.cs", settings, FileCategory.Production);

        Assert.Empty(violations);
    }

    [Fact]
    public void DetectViolations_PropertyAccessor_CommentSuppressionOnAccessor_Ignored()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 15).Select(i => $"            int x{i} = {i};"));
        var code = $$"""
            public class Service
            {
                public int Value
                {
                    // codecondenser:ignore-loc
                    get
                    {
            {{lines}}
                        return 0;
                    }
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 10,
            ClassLinesOfCodeThreshold = 100
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/Service.cs", settings, FileCategory.Production);

        Assert.Empty(violations);
    }

    [Fact]
    public void DetectViolations_PropertyAccessor_CommentSuppressionOnProperty_Ignored()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 15).Select(i => $"            int x{i} = {i};"));
        var code = $$"""
            public class Service
            {
                // codecondenser:ignore-loc
                public int Value
                {
                    get
                    {
            {{lines}}
                        return 0;
                    }
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 10,
            ClassLinesOfCodeThreshold = 100
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/Service.cs", settings, FileCategory.Production);

        Assert.Empty(violations);
    }

    [Fact]
    public void DetectViolations_PropertyAccessor_SuppressMessageOnProperty_Ignored()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 15).Select(i => $"            int x{i} = {i};"));
        var code = $$"""
            using System.Diagnostics.CodeAnalysis;

            public class Service
            {
                [SuppressMessage("CodeCondenser", "MethodLength")]
                public int Value
                {
                    get
                    {
            {{lines}}
                        return 0;
                    }
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 10,
            ClassLinesOfCodeThreshold = 100
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/Service.cs", settings, FileCategory.Production);

        Assert.Empty(violations);
    }

    [Fact]
    public void DetectViolations_PropertyAccessor_SuppressMessageOnAccessor_Ignored()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 15).Select(i => $"            int x{i} = {i};"));
        var code = $$"""
            using System.Diagnostics.CodeAnalysis;

            public class Service
            {
                public int Value
                {
                    [SuppressMessage("CodeCondenser", "MethodLength")]
                    get
                    {
            {{lines}}
                        return 0;
                    }
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 10,
            ClassLinesOfCodeThreshold = 100
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/Service.cs", settings, FileCategory.Production);

        Assert.Empty(violations);
    }

    [Fact]
    public void DetectViolations_LocalFunction_CommentSuppressionOnLocalFunction_Ignored()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 15).Select(i => $"            int x{i} = {i};"));
        var code = $$"""
            public class Service
            {
                public void DoWork()
                {
                    // codecondenser:ignore-loc
                    void Helper()
                    {
            {{lines}}
                    }
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 10,
            ClassLinesOfCodeThreshold = 100
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/Service.cs", settings, FileCategory.Production);

        Assert.Empty(violations);
    }

    [Fact]
    public void DetectViolations_LocalFunction_SuppressMessageOnLocalFunction_Ignored()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 15).Select(i => $"            int x{i} = {i};"));
        var code = $$"""
            using System.Diagnostics.CodeAnalysis;

            public class Service
            {
                public void DoWork()
                {
                    [SuppressMessage("CodeCondenser", "MethodLength")]
                    void Helper()
                    {
            {{lines}}
                    }
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 10,
            ClassLinesOfCodeThreshold = 100
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/Service.cs", settings, FileCategory.Production);

        // Helper() is suppressed; only DoWork() was flagged
        Assert.Single(violations);
        Assert.Equal("Service.DoWork()", violations[0].Identifier);
        Assert.DoesNotContain(violations, v => v.Identifier.Contains("Helper"));
    }

    [Fact]
    public void DetectViolations_LocalFunction_CommentSuppressionOnEnclosingMethod_Ignored()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 15).Select(i => $"            int x{i} = {i};"));
        var code = $$"""
            public class Service
            {
                // codecondenser:ignore-loc
                public void DoWork()
                {
                    void Helper()
                    {
            {{lines}}
                    }
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 10,
            ClassLinesOfCodeThreshold = 100
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/Service.cs", settings, FileCategory.Production);

        Assert.Empty(violations);
    }

    [Fact]
    public void DetectViolations_IndexerAccessor_EmitsCorrectIdentifier()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 15).Select(i => $"            int x{i} = {i};"));
        var code = $$"""
            public class Service
            {
                public int this[int index]
                {
                    get
                    {
            {{lines}}
                        return index;
                    }
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 10,
            ClassLinesOfCodeThreshold = 100
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/Service.cs", settings, FileCategory.Production);

        Assert.Single(violations);
        var v = violations[0];
        Assert.Equal("Service.this[].get", v.Identifier);
        Assert.Equal(LinesOfCodeKind.Method, v.Kind);
    }

    [Fact]
    public void DetectViolations_EventAccessor_EmitsCorrectIdentifier()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 15).Select(i => $"            int x{i} = {i};"));
        var code = $$"""
            using System;

            public class Service
            {
                public event EventHandler CustomEvent
                {
                    add
                    {
            {{lines}}
                    }
                    remove { }
                }
            }
            """;
        var (tree, root) = Parse(code);
        var settings = new CodeCondenserSettings
        {
            MethodLinesOfCodeThreshold = 10,
            ClassLinesOfCodeThreshold = 100
        };

        var violations = _detector.DetectViolations(tree, "c:/repo/Service.cs", settings, FileCategory.Production);

        Assert.Single(violations);
        var v = violations[0];
        Assert.Equal("Service.CustomEvent.add", v.Identifier);
        Assert.Equal(LinesOfCodeKind.Method, v.Kind);
    }
}
