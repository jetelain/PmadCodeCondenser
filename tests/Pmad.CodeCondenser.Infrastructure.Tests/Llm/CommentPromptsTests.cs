using FluentAssertions;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Infrastructure.Llm.Prompts;

namespace Pmad.CodeCondenser.Infrastructure.Tests.Llm;

public class CommentPromptsTests
{
    [Fact]
    public void BuildRegularCommentPrompt_ContainsOriginalComment()
    {
        var comment = "// This is a very long explanatory comment.";
        var prompt = CommentPrompts.BuildRegularCommentPrompt(comment);

        prompt.Should().Contain(comment);
        prompt.Should().Contain("Summarize the following verbose C# comment");
    }

    [Fact]
    public void BuildXmlDocumentationPrompt_ContainsOriginalComment()
    {
        var comment = "/// <summary>This is a detailed summary.</summary>";
        var prompt = CommentPrompts.BuildXmlDocumentationPrompt(comment);

        prompt.Should().Contain(comment);
        prompt.Should().Contain("Condense the following verbose C# XML documentation comment");
    }

    [Fact]
    public void CleanModelResponse_StripsMarkdownCodeFences()
    {
        var response = """
            ```csharp
            // This is the clean comment
            ```
            """;

        var cleaned = CommentPrompts.CleanModelResponse(response, CommentKind.SingleLine);
        cleaned.Should().Be("// This is the clean comment");
    }

    [Fact]
    public void CleanModelResponse_WithConversationalPreamble_ExtractsInsideCodeFence()
    {
        var response = """
            Here is the condensed comment:
            ```csharp
            // This is the clean comment
            ```
            Hope this helps!
            """;

        var cleaned = CommentPrompts.CleanModelResponse(response, CommentKind.SingleLine);
        cleaned.Should().Be("// This is the clean comment");
    }

    [Fact]
    public void CleanModelResponse_SingleLineFenceWithoutNewlines_StripsFencesCorrectly()
    {
        var response = "```// Single line comment inside fence```";

        var cleaned = CommentPrompts.CleanModelResponse(response, CommentKind.SingleLine);
        cleaned.Should().Be("// Single line comment inside fence");
    }

    [Fact]
    public void CleanModelResponse_XmlDoc_EnsuresSlashesOnEveryLine()
    {
        var response = """
            <summary>
            Concise explanation.
            </summary>
            """;

        var cleaned = CommentPrompts.CleanModelResponse(response, CommentKind.XmlDocumentation);
        cleaned.Should().Contain("/// <summary>");
        cleaned.Should().Contain("/// Concise explanation.");
        cleaned.Should().Contain("/// </summary>");
    }

    [Fact]
    public void CleanModelResponse_WithThinkingTags_StripsThinkingMonologue()
    {
        var response = """
            <think>
            The user wants me to summarize this comment.
            I should be concise and remove fluff.
            </think>
            // Concise summary of the code.
            """;

        var cleaned = CommentPrompts.CleanModelResponse(response, CommentKind.SingleLine);
        cleaned.Should().Be("// Concise summary of the code.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CleanModelResponse_EmptyOrWhitespace_ReturnsEmptyString(string? raw)
    {
        var cleaned = CommentPrompts.CleanModelResponse(raw!, CommentKind.SingleLine);
        cleaned.Should().BeEmpty();
    }
}
