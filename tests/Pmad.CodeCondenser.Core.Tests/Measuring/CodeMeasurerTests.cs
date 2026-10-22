using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Pmad.AI.Tokens;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Engine;
using Pmad.CodeCondenser.Core.Measuring;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Tests.Measuring;

public class CodeMeasurerTests
{
    private readonly CodeMeasurer _measurer = new();

    [Fact]
    public void MeasureFile_EmptyOrWhitespace_ReturnsZeroTokens()
    {
        var resultEmpty = _measurer.MeasureFile("Empty.cs", FileCategory.Production, "", HeuristicTokenEstimator.Default);
        resultEmpty.CodeTokens.Should().Be(0);
        resultEmpty.CommentTokens.Should().Be(0);
        resultEmpty.TotalTokens.Should().Be(0);

        var resultWhitespace = _measurer.MeasureFile("Whitespace.cs", FileCategory.Production, "   \r\n\t  ", HeuristicTokenEstimator.Default);
        resultWhitespace.CodeTokens.Should().Be(0);
        resultWhitespace.CommentTokens.Should().Be(0);
        resultWhitespace.TotalTokens.Should().Be(0);
    }

    [Fact]
    public void MeasureFile_CodeOnly_MeasuresCodeTokensAndZeroCommentTokens()
    {
        var code = """
            namespace SampleApp;

            public class Calculator
            {
                public int Add(int a, int b)
                {
                    return a + b;
                }
            }
            """;

        var result = _measurer.MeasureFile("Calculator.cs", FileCategory.Production, code, HeuristicTokenEstimator.Default);

        result.FilePath.Should().Be("Calculator.cs");
        result.Category.Should().Be(FileCategory.Production);
        result.CodeTokens.Should().BeGreaterThan(0);
        result.CommentTokens.Should().Be(0);
        result.TotalTokens.Should().Be(result.CodeTokens);
    }

    [Fact]
    public void MeasureFile_CommentsOnly_MeasuresCommentTokensAndZeroCodeTokens()
    {
        var comments = """
            // This is a file header comment.
            /* Multi-line explanation
               of intentional design. */
            /// <summary>
            /// Top level doc comment
            /// </summary>
            """;

        var result = _measurer.MeasureFile("CommentsOnly.cs", FileCategory.Production, comments, HeuristicTokenEstimator.Default);

        result.CodeTokens.Should().Be(0);
        result.CommentTokens.Should().BeGreaterThan(0);
        result.TotalTokens.Should().Be(result.CommentTokens);
    }

    [Fact]
    public void MeasureFile_BothCodeAndComments_MeasuresBothCorrectly()
    {
        var code = """
            // File header
            namespace SampleApp;

            /// <summary>
            /// Represents a basic user service.
            /// </summary>
            public class UserService
            {
                // Internal counter
                private int _counter;

                /* Multi-line comment
                   about process method */
                public void Process()
                {
                    _counter++; // Increment
                }
            }
            """;

        var result = _measurer.MeasureFile("UserService.cs", FileCategory.Production, code, HeuristicTokenEstimator.Default);

        result.CodeTokens.Should().BeGreaterThan(0);
        result.CommentTokens.Should().BeGreaterThan(0);
        result.TotalTokens.Should().Be(result.CodeTokens + result.CommentTokens);
    }

    [Fact]
    public void MeasureFile_InlineComments_DoesNotMergeAdjacentTokens()
    {
        var code = "int/*c*/x = 1;";
        var result = _measurer.MeasureFile("Inline.cs", FileCategory.Production, code, HeuristicTokenEstimator.Default);

        result.CodeTokens.Should().BeGreaterThan(0);
        result.CommentTokens.Should().BeGreaterThan(0);
        result.TotalTokens.Should().Be(result.CodeTokens + result.CommentTokens);
    }

    [Fact]
    public void MeasureFile_EndOfFileComment_IsCapturedInCommentTokens()
    {
        var code = """
            public class Foo
            {
            }
            // Trailing comment at end of file
            """;

        var result = _measurer.MeasureFile("Trailing.cs", FileCategory.Test, code, HeuristicTokenEstimator.Default);

        result.Category.Should().Be(FileCategory.Test);
        result.CodeTokens.Should().BeGreaterThan(0);
        result.CommentTokens.Should().BeGreaterThan(0);
        result.TotalTokens.Should().Be(result.CodeTokens + result.CommentTokens);
    }

    [Fact]
    public void MeasureFile_DifferentEstimators_ProduceDifferentEstimates()
    {
        var code = """
            public class Sample
            {
                // Verbose comment explaining algorithm
                public int Run(int x) => x * 2;
            }
            """;

        var defaultResult = _measurer.MeasureFile("Sample.cs", FileCategory.Production, code, HeuristicTokenEstimator.Default);
        var gemmaResult = _measurer.MeasureFile("Sample.cs", FileCategory.Production, code, HeuristicTokenEstimator.Gemma);
        var qwenResult = _measurer.MeasureFile("Sample.cs", FileCategory.Production, code, HeuristicTokenEstimator.Qwen);

        defaultResult.TotalTokens.Should().BeGreaterThan(0);
        gemmaResult.TotalTokens.Should().BeGreaterThan(0);
        qwenResult.TotalTokens.Should().BeGreaterThan(0);

        // Gemma has higher word factor (1.43 vs 1.25), so token count should reflect that
        gemmaResult.TotalTokens.Should().BeGreaterThanOrEqualTo(defaultResult.TotalTokens);
    }

    [Theory]
    [InlineData("gemma4:e4b", 1.43, 0.72)]
    [InlineData("gemma", 1.43, 0.72)]
    [InlineData("qwen2.5-coder:7b", 1.36, 0.66)]
    [InlineData("qwen", 1.36, 0.66)]
    [InlineData("default", 1.25, 0.8)]
    [InlineData("", 1.25, 0.8)]
    public void ResolveEstimator_SelectsAppropriatePreset(string estimatorName, double expectedWordFactor, double expectedSymbolFactor)
    {
        var settings = new CodeCondenserSettings
        {
            TokenEstimator = estimatorName,
            Ollama = new OllamaSettings { Model = "different-model-for-summarization" }
        };

        var estimator = CodeCondenserEngine.ResolveEstimator(settings);
        estimator.WordFactor.Should().Be(expectedWordFactor);
        estimator.SymbolFactor.Should().Be(expectedSymbolFactor);
    }

}
