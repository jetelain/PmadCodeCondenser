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
        var filePath = @"C:\repos\MyProject\OrderService.cs";

        var analysis = _splitter.AnalyzeFile(tree, filePath, settings);
        var split = _splitter.SplitFile(tree, filePath, analysis);

        Assert.Equal(filePath, split.PrimaryFilePath);
        Assert.False(split.IsRenamed);

        // Primary file should no longer contain CustomerService
        Assert.Contains("public class OrderService", split.PrimaryFileContent);
        Assert.DoesNotContain("public class CustomerService", split.PrimaryFileContent);

        // New file should be CustomerService.cs
        var newCustomerFile = @"C:\repos\MyProject\CustomerService.cs";
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
}
