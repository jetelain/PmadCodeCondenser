using FluentAssertions;
using Pmad.CodeCondenser.Core.Configuration;
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

    [Fact]
    public void BuildRetryPrompt_RegularComment_ContainsTargetAndAllowsDroppingDetails()
    {
        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100 };
        var previous = "// This is a somewhat verbose previous summary that exceeded the threshold";

        var prompt = CommentPrompts.BuildRetryPrompt(previous, "// original", CommentKind.SingleLine, settings);

        prompt.Should().Contain("too verbose");
        prompt.Should().Contain("100 characters");
        prompt.Should().Contain("drop secondary details");
    }

    [Fact]
    public void BuildRetryPrompt_XmlDoc_ContainsXmlSummaryInstructionsAndAllowsDroppingDetails()
    {
        var settings = new CodeCondenserSettings { XmlSummaryLengthThreshold = 80 };
        var previous = "/// <summary>\r\n/// Excessively detailed XML doc comment\r\n/// </summary>";

        var prompt = CommentPrompts.BuildRetryPrompt(previous, "/// <summary>\r\n/// original\r\n/// </summary>", CommentKind.XmlDocumentation, settings);

        prompt.Should().Contain("XML documentation");
        prompt.Should().Contain("80 characters");
        prompt.Should().Contain("<summary>");
        prompt.Should().Contain("drop secondary details");
    }

    [Fact]
    public void BuildRetryPrompt_EmptyPreviousSummary_ReturnsEmptyResponsePromptWithDropDetails()
    {
        var settings = new CodeCondenserSettings();

        var prompt = CommentPrompts.BuildRetryPrompt(null, "// original", CommentKind.SingleLine, settings);

        prompt.Should().Contain("empty or invalid");
        prompt.Should().Contain("drop secondary details");
    }

    [Theory]
    [InlineData("Comment is useless")]
    [InlineData("Comment is useless.")]
    [InlineData("Comment is usless")]
    [InlineData("Comment is usless.")]
    [InlineData("COMMENT IS USELESS")]
    [InlineData("\"Comment is useless\"")]
    [InlineData("`Comment is useless`")]
    [InlineData("// Comment is useless")]
    [InlineData("/// Comment is useless")]
    [InlineData("/* Comment is useless */")]
    public void IsUselessCommentResponse_ValidMarkers_ReturnsTrue(string response)
    {
        CommentPrompts.IsUselessCommentResponse(response).Should().BeTrue();
    }

    [Theory]
    [InlineData("This is a useful summary")]
    [InlineData("Calculates the total score")]
    [InlineData("// Comment explaining why we do this")]
    [InlineData("")]
    [InlineData(null)]
    public void IsUselessCommentResponse_OtherResponses_ReturnsFalse(string? response)
    {
        CommentPrompts.IsUselessCommentResponse(response).Should().BeFalse();
    }

    [Theory]
    [InlineData("Comment is useless")]
    [InlineData("Comment is usless")]
    [InlineData("```\nComment is useless\n```")]
    [InlineData("<think>This comment provides no why</think>\nComment is useless")]
    [InlineData("// Comment is useless")]
    [InlineData("/// Comment is usless")]
    public void CleanModelResponse_WhenUselessMarkerReturned_ReturnsExactMarker(string raw)
    {
        var cleaned = CommentPrompts.CleanModelResponse(raw, CommentKind.SingleLine);
        cleaned.Should().Be(CommentPrompts.UselessCommentMarker);
    }
}
