using Microsoft.CodeAnalysis.CSharp;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Core.Types;
using Xunit;

namespace Pmad.CodeCondenser.Core.Tests.Types;

public class TypeFileSplitterTests
{
    private readonly TypeFileSplitter _splitter = new();

    [Fact]
    public void AnalyzeFile_SingleMatchingClass_ReturnsNoAction()
    {
        var code = """
            namespace MyNamespace;

            public class OrderService
            {
                public void Process() { }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings();

        var result = _splitter.AnalyzeFile(tree, "OrderService.cs", settings);

        Assert.Equal(TypeFileAction.None, result.Action);
        Assert.False(result.HasViolation);
        Assert.Single(result.Types);
        Assert.True(result.Types[0].IsPrimary);
    }

    [Fact]
    public void AnalyzeFile_SingleMismatchedClass_ReturnsRenameAction()
    {
        var code = """
            namespace MyNamespace;

            public class OrderService
            {
                public void Process() { }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings();

        var result = _splitter.AnalyzeFile(tree, "ServiceHelpers.cs", settings);

        Assert.Equal(TypeFileAction.Rename, result.Action);
        Assert.True(result.HasViolation);
        Assert.Equal("OrderService.cs", result.SuggestedFileName);
        Assert.Empty(result.TypesToExtract);
    }

    [Fact]
    public void AnalyzeFile_MultipleClasses_OneMatchingFileName_ReturnsSplitAction()
    {
        var code = """
            namespace MyNamespace;

            public class OrderService
            {
                public void Process() { }
            }

            public class CustomerService
            {
                public void Help() { }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings();

        var result = _splitter.AnalyzeFile(tree, "OrderService.cs", settings);

        Assert.Equal(TypeFileAction.Split, result.Action);
        Assert.True(result.HasViolation);
        Assert.Equal(2, result.Types.Count);
        Assert.Equal("OrderService", result.PrimaryType?.TypeName);
        var extract = Assert.Single(result.TypesToExtract);
        Assert.Equal("CustomerService", extract.TypeName);
        Assert.Equal("CustomerService.cs", extract.TargetFileName);
    }

    [Fact]
    public void AnalyzeFile_MultipleClasses_NoneMatchingFileName_ReturnsSplitAndRenameAction()
    {
        var code = """
            namespace MyNamespace;

            public class OrderService
            {
                public void Process() { }
            }

            public class CustomerService
            {
                public void Help() { }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings();

        var result = _splitter.AnalyzeFile(tree, "CommonServices.cs", settings);

        Assert.Equal(TypeFileAction.SplitAndRename, result.Action);
        Assert.True(result.HasViolation);
        Assert.Equal("OrderService.cs", result.SuggestedFileName);
        var extract = Assert.Single(result.TypesToExtract);
        Assert.Equal("CustomerService", extract.TypeName);
    }

    [Fact]
    public void AnalyzeFile_AuxiliaryOneLineDelegate_IsTolerated()
    {
        var code = """
            namespace MyNamespace;

            public delegate void OrderCompletedHandler(int orderId);

            public class OrderService
            {
                public void Process() { }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings { TypeLineTolerance = 1 };

        var result = _splitter.AnalyzeFile(tree, "OrderService.cs", settings);

        Assert.Equal(TypeFileAction.None, result.Action);
        Assert.False(result.HasViolation);
        Assert.Empty(result.TypesToExtract);
        var delegateType = result.Types.First(t => t.Kind == "delegate");
        Assert.True(delegateType.IsTolerated);
        Assert.Equal(1, delegateType.LinesOfCode);
    }

    [Fact]
    public void AnalyzeFile_SuppressedAuxiliaryType_IsIgnored()
    {
        var code = """
            namespace MyNamespace;

            public class OrderService
            {
                public void Process() { }
            }

            // codecondenser:ignore-type-split
            public class InternalOrderPayload
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings();

        var result = _splitter.AnalyzeFile(tree, "OrderService.cs", settings);

        Assert.Equal(TypeFileAction.None, result.Action);
        Assert.False(result.HasViolation);
        Assert.Empty(result.TypesToExtract);
        var secondary = result.Types.First(t => t.TypeName == "InternalOrderPayload");
        Assert.True(secondary.IsSuppressed);
    }

    [Fact]
    public void SplitFile_ExtractsSecondaryClass_PreservesNamespaceAndUsings()
    {
        var code = """
            // Copyright 2026
            #nullable enable

            using System;
            using System.Collections.Generic;

            namespace MyNamespace;

            public class OrderService
            {
                public void Process() { }
            }

            /// <summary>
            /// Customer service docs.
            /// </summary>
            public class CustomerService
            {
                public void Help() { }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings();
        var baseDir = Path.Combine(Path.GetTempPath(), "MyProject");
        var filePath = Path.Combine(baseDir, "OrderService.cs");

        var analysis = _splitter.AnalyzeFile(tree, filePath, settings);
        var split = _splitter.SplitFile(tree, filePath, analysis);

        Assert.Equal(filePath, split.PrimaryFilePath);
        Assert.False(split.IsRenamed);

        // Primary file should no longer contain CustomerService
        Assert.Contains("public class OrderService", split.PrimaryFileContent);
        Assert.DoesNotContain("public class CustomerService", split.PrimaryFileContent);

        // New file should be CustomerService.cs
        var newCustomerFile = Path.Combine(baseDir, "CustomerService.cs");
        Assert.True(split.NewFiles.ContainsKey(newCustomerFile));
        var customerContent = split.NewFiles[newCustomerFile];

        Assert.Contains("namespace MyNamespace;", customerContent);
        Assert.Contains("using System;", customerContent);
        Assert.Contains("using System.Collections.Generic;", customerContent);
        Assert.Contains("#nullable enable", customerContent);
        Assert.Contains("Customer service docs.", customerContent);
        Assert.Contains("public class CustomerService", customerContent);
        Assert.DoesNotContain("public class OrderService", customerContent);
    }

    [Fact]
    public void AnalyzeFile_PartialClassWithAspectFileName_ReturnsNoAction()
    {
        var code = """
            namespace MyNamespace;

            public partial class OrderService
            {
                public void Validate() { }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings();

        var result = _splitter.AnalyzeFile(tree, "OrderService.Validation.cs", settings);

        Assert.Equal(TypeFileAction.None, result.Action);
        Assert.False(result.HasViolation);
    }

    [Fact]
    public void AnalyzeFile_GenericClassWithArityFileName_ReturnsNoAction()
    {
        var code = """
            namespace MyNamespace;

            public class MyClass<T>
            {
                public T? Value { get; set; }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings();

        var result = _splitter.AnalyzeFile(tree, "MyClass`1.cs", settings);

        Assert.Equal(TypeFileAction.None, result.Action);
        Assert.False(result.HasViolation);
    }

    [Fact]
    public void AnalyzeFile_SuppressedPrimaryTypeWithCC007_DoesNotRename()
    {
        var code = """
            using System.Diagnostics.CodeAnalysis;

            namespace MyNamespace;

            [SuppressMessage("CodeCondenser", "CC007")]
            public class OrderService
            {
                public void Process() { }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings();

        var result = _splitter.AnalyzeFile(tree, "ServiceHelpers.cs", settings);

        Assert.Equal(TypeFileAction.None, result.Action);
        Assert.False(result.HasViolation);
    }

    [Fact]
    public void SplitFile_GenericOverloadsInSameFile_ExtractsCorrectNodeWithArity()
    {
        var code = """
            namespace MyNamespace;

            public class Serializer
            {
                public void SerializeNonGeneric() { }
            }

            public class Serializer<T>
            {
                public void SerializeGeneric(T item) { }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings();
        var baseDir = Path.Combine(Path.GetTempPath(), "MyProject");
        var filePath = Path.Combine(baseDir, "Serializer.cs");

        var analysis = _splitter.AnalyzeFile(tree, filePath, settings);
        Assert.Equal(TypeFileAction.Split, analysis.Action);
        Assert.Single(analysis.TypesToExtract);
        Assert.Equal(1, analysis.TypesToExtract[0].Arity);

        var split = _splitter.SplitFile(tree, filePath, analysis);

        // Primary should retain non-generic Serializer
        Assert.Contains("public class Serializer", split.PrimaryFileContent);
        Assert.DoesNotContain("public class Serializer<T>", split.PrimaryFileContent);

        // Extracted file should contain generic Serializer<T>
        var newFile = Path.Combine(baseDir, "Serializer`1.cs");
        Assert.True(split.NewFiles.ContainsKey(newFile));
        Assert.Contains("public class Serializer<T>", split.NewFiles[newFile]);
        Assert.DoesNotContain("public class Serializer\n", split.NewFiles[newFile].Replace("\r\n", "\n"));
    }

    [Fact]
    public void SplitFile_DistinctUsingAliases_PreservesAllAliases()
    {
        var code = """
            using ModelA = System.String;
            using ModelB = System.String;

            namespace MyNamespace;

            public class PrimaryService
            {
                public void Run() { }
            }

            public class SecondaryService
            {
                public void Execute() { }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var settings = new CodeCondenserSettings();
        var baseDir = Path.Combine(Path.GetTempPath(), "MyProject");
        var filePath = Path.Combine(baseDir, "PrimaryService.cs");

        var analysis = _splitter.AnalyzeFile(tree, filePath, settings);
        var split = _splitter.SplitFile(tree, filePath, analysis);

        var newFile = Path.Combine(baseDir, "SecondaryService.cs");
        Assert.True(split.NewFiles.ContainsKey(newFile));
        var content = split.NewFiles[newFile];
        Assert.Contains("using ModelA = System.String;", content);
        Assert.Contains("using ModelB = System.String;", content);
    }
}
