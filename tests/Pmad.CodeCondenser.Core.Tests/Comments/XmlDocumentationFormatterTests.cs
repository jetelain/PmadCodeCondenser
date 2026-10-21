using FluentAssertions;
using Pmad.CodeCondenser.Core.Comments;

namespace Pmad.CodeCondenser.Core.Tests.Comments;

public class XmlDocumentationFormatterTests
{
    [Fact]
    public void Format_MissingSummaryTags_AddsSummaryTags()
    {
        // Example 2 from ComicGenCLI
        var input = """
            /// Probes connectivity for LLM, image, wallet, and pricing services.
            /// Used by CLI and GUI connection/capability tests.
            """;

        var formatted = XmlDocumentationFormatter.Format(input, indentation: "", maxLineLength: 120);

        formatted.Should().StartWith("/// <summary>");
        formatted.Should().Contain("/// Probes connectivity for LLM, image, wallet, and pricing services.");
        formatted.Should().Contain("/// Used by CLI and GUI connection/capability tests.");
        formatted.Should().EndWith("/// </summary>");

        foreach (var line in formatted.Split("\r\n"))
        {
            line.Length.Should().BeLessThanOrEqualTo(120);
        }
    }

    [Fact]
    public void Format_Exceeding120Columns_WrapsToStayOn120Columns()
    {
        // Example 1 from ComicGenCLI
        var input = """
            /// <summary>Checks wallet before run. Warns in <see cref="WalletEnforcement.Warn"/> mode or throws <see cref="RunBudgetException"/> in <see cref="WalletEnforcement.Block"/> mode if projected cost exceeds balance or session cap. Returns estimated post-run balance.</summary>
            """;

        var indentation = "    "; // 4 spaces
        var formatted = XmlDocumentationFormatter.Format(input, indentation: indentation, maxLineLength: 120);

        var lines = formatted.Split("\r\n");
        // Line 0 does not have leading indentation in formatted return string (Roslyn trivia convention)
        lines[0].Should().Be("/// <summary>");

        // Verify every line combined with indentation is <= 120 columns
        for (int i = 0; i < lines.Length; i++)
        {
            var fullLine = (i == 0 ? indentation : "") + lines[i];
            fullLine.Length.Should().BeLessThanOrEqualTo(120, $"line {i} exceeded 120 columns: '{fullLine}'");
        }

        // Verify inline tags were not broken
        formatted.Should().Contain("<see cref=\"WalletEnforcement.Warn\"");
        formatted.Should().Contain("<see cref=\"RunBudgetException\"");
        formatted.Should().Contain("<see cref=\"WalletEnforcement.Block\"");
        formatted.Should().EndWith("/// </summary>");
    }

    [Fact]
    public void Format_Under120Columns_StaysOnSingleLine()
    {
        var input = """
            /// <summary>Formats a USD unit rate (up to 4 decimals, trims trailing zeros after 2).</summary>
            """;

        var formatted = XmlDocumentationFormatter.Format(input, indentation: "    ", maxLineLength: 120);

        formatted.Should().Be("/// <summary>Formats a USD unit rate (up to 4 decimals, trims trailing zeros after 2).</summary>");
        ("    " + formatted).Length.Should().BeLessThanOrEqualTo(120);
    }

    [Fact]
    public void Format_PreservesAlreadyMultilineSummary()
    {
        var input = """
            /// <summary>
            /// Compact summary line.
            /// </summary>
            """;

        var formatted = XmlDocumentationFormatter.Format(input, indentation: "    ", maxLineLength: 120);

        var lines = formatted.Split("\r\n");
        lines.Should().HaveCount(3);
        lines[0].Should().Be("/// <summary>");
        lines[1].Should().Be("    /// Compact summary line.");
        lines[2].Should().Be("    /// </summary>");
    }

    [Fact]
    public void Format_MultipleTags_FormatsEachCorrectly()
    {
        var input = """
            /// <summary>Checks wallet before run.</summary>
            /// <param name="reporter">The progress reporter to notify about run cost and budget status.</param>
            /// <param name="estimatedCost">The estimated cost.</param>
            /// <returns>Estimated post-run balance.</returns>
            """;

        var formatted = XmlDocumentationFormatter.Format(input, indentation: "    ", maxLineLength: 120);

        var lines = formatted.Split("\r\n");
        for (int i = 0; i < lines.Length; i++)
        {
            var fullLine = (i == 0 ? "    " : "") + lines[i];
            fullLine.Length.Should().BeLessThanOrEqualTo(120, $"line {i} exceeded 120: '{fullLine}'");
        }

        formatted.Should().Contain("<summary>Checks wallet before run.</summary>");
        formatted.Should().Contain("<param name=\"reporter\">");
        formatted.Should().Contain("<param name=\"estimatedCost\">The estimated cost.</param>");
        formatted.Should().Contain("<returns>Estimated post-run balance.</returns>");
    }

    [Fact]
    public void Format_MissingSummaryWithParam_WrapsUntaggedTextInSummary()
    {
        var input = """
            /// Calculates total price.
            /// <param name="count">The quantity.</param>
            """;

        var formatted = XmlDocumentationFormatter.Format(input, indentation: "", maxLineLength: 120);

        formatted.Should().Contain("<summary>Calculates total price.</summary>");
        formatted.Should().Contain("<param name=\"count\">The quantity.</param>");
    }

    [Fact]
    public void Format_TypeparamOrInheritdocWithoutSummary_DoesNotWrapInSummaryTag()
    {
        var input = """
            /// <typeparam name="T">The item type.</typeparam>
            /// <param name="item">The item to add.</param>
            /// <returns>True if added.</returns>
            """;

        var formatted = XmlDocumentationFormatter.Format(input, indentation: "", maxLineLength: 120);

        formatted.Should().NotContain("<summary><typeparam");
        formatted.Should().Contain("<typeparam name=\"T\">The item type.</typeparam>");
        formatted.Should().Contain("<param name=\"item\">The item to add.</param>");
        formatted.Should().Contain("<returns>True if added.</returns>");
    }

    [Fact]
    public void Format_EmptyTag_EmitsSelfClosingSyntax()
    {
        var input = """
            /// <inheritdoc />
            /// <seealso cref="System.String" />
            """;

        var formatted = XmlDocumentationFormatter.Format(input, indentation: "", maxLineLength: 120);

        formatted.Should().Contain("/// <inheritdoc />");
        formatted.Should().Contain("/// <seealso cref=\"System.String\" />");
        formatted.Should().NotContain("<inheritdoc></inheritdoc>");
        formatted.Should().NotContain("<seealso cref=\"System.String\"></seealso>");
    }

    [Fact]
    public void Format_MultilineRemarks_PreservesParagraphSeparators()
    {
        var input = """
            /// <remarks>
            /// First paragraph of remarks text.
            ///
            /// Second paragraph of remarks text.
            /// </remarks>
            """;

        var formatted = XmlDocumentationFormatter.Format(input, indentation: "", maxLineLength: 120);

        var lines = formatted.Split("\r\n");
        lines.Should().Contain("///");
        formatted.Should().Contain("/// First paragraph of remarks text.\r\n///\r\n/// Second paragraph of remarks text.");
    }

    [Fact]
    public void Format_LooseTextAndInlineTagsAfterSummary_WrapsInRemarksWithoutMangledSummaryTags()
    {
        var input = """
            /// <summary>
            /// Implements <see cref="IAgentSurfaceService" /> using embedded resources.
            /// </summary>
            /// Every file is read once into immutable, process-wide state from the assembly's
            /// embedded resources. This prevents divergence between hosts (CLI, GUI).
            /// Canonical front-matter supplies
            /// <see cref="SlashCommandPrompt.Name" />
            /// /
            /// <see cref="SlashCommandPrompt.Description" />
            /// ;
            /// skill front-matter supplies the same for
            /// <see cref="AgentSkill" />
            /// .
            """;

        var formatted = XmlDocumentationFormatter.Format(input, indentation: "", maxLineLength: 120);

        formatted.Should().NotContain("<summary>/</summary>");
        formatted.Should().NotContain("<summary>.</summary>");
        formatted.Should().NotContain("<summary>;");
        // Exactly one <summary> tag
        formatted.Split("<summary>").Length.Should().Be(2);
        formatted.Should().Contain("<remarks>");
        formatted.Should().Contain("</remarks>");
        formatted.Should().Contain("<see cref=\"SlashCommandPrompt.Name\"");
        formatted.Should().Contain("<see cref=\"SlashCommandPrompt.Description\"");
        formatted.Should().Contain("<see cref=\"AgentSkill\"");
    }

    [Fact]
    public void Format_MultipleSummaryTags_ConsolidatesIntoSingleSummaryTag()
    {
        var input = """
            /// <summary>First paragraph of summary.</summary>
            /// <summary>Second paragraph of summary with <see cref="System.String"/>.</summary>
            """;

        var formatted = XmlDocumentationFormatter.Format(input, indentation: "", maxLineLength: 120);

        // Exactly one <summary> tag
        formatted.Split("<summary>").Length.Should().Be(2);
        formatted.Should().Contain("First paragraph of summary.");
        formatted.Should().Contain("Second paragraph of summary with <see cref=\"System.String\"");
    }

    [Fact]
    public void Format_PreviouslyMangledSummaryWithPunctuationTags_ConsolidatesCleanly()
    {
        var input = """
            /// <summary>
            /// Implements <see cref="IAgentSurfaceService" /> using embedded resources.
            /// </summary>
            /// <summary>
            /// Every file is read once into immutable, process-wide state.
            /// </summary>
            /// <see cref="SlashCommandPrompt.Name" />
            /// <summary>/</summary>
            /// <see cref="SlashCommandPrompt.Description" />
            /// <summary>.</summary>
            """;

        var formatted = XmlDocumentationFormatter.Format(input, indentation: "", maxLineLength: 120);

        formatted.Should().NotContain("<summary>/</summary>");
        formatted.Should().NotContain("<summary>.</summary>");
        formatted.Split("<summary>").Length.Should().Be(2);
        formatted.Should().Contain("<remarks>");
        formatted.Should().Contain("</remarks>");
    }

    [Fact]
    public void Format_InlineCodeTags_DoesNotAddExtraSpacesInsideCodeTag()
    {
        var input = """
            /// <summary>GitHub Copilot in VS Code — reads <c>.github/prompts/*.prompt.md</c>; the discovery document is
            /// <c>.github/copilot-instructions.md</c>.</summary>
            """;

        var formatted = XmlDocumentationFormatter.Format(input, indentation: "    ", maxLineLength: 120);

        formatted.Should().NotContain("<c>. ");
        formatted.Should().NotContain(" </c>");
        formatted.Should().Contain("<c>.github/prompts/*.prompt.md</c>;");
        formatted.Should().Contain("<c>.github/copilot-instructions.md</c>.");
    }

    [Fact]
    public void Format_PreviouslyMangledSpacesInsideCodeTags_CleansExtraSpaces()
    {
        var input = """
            /// <summary>
            /// Copilot in VS Code reads <c>. github/prompts/*.prompt.md </c>; the
            /// discovery document is <c>. github/copilot-instructions.md </c>.
            /// </summary>
            """;

        var formatted = XmlDocumentationFormatter.Format(input, indentation: "    ", maxLineLength: 120);

        formatted.Should().NotContain("<c>. ");
        formatted.Should().NotContain(" </c>");
        formatted.Should().Contain("<c>.github/prompts/*.prompt.md</c>;");
        formatted.Should().Contain("<c>.github/copilot-instructions.md</c>.");
    }

    [Fact]
    public void Format_VariousInlineTags_PreservesNoSpacePadding()
    {
        var input = """
            /// <summary>Press <c> Esc </c> or <c> Enter </c> to browse <c> .candidates/ </c>.</summary>
            """;

        var formatted = XmlDocumentationFormatter.Format(input, indentation: "", maxLineLength: 120);

        formatted.Should().Contain("<c>Esc</c>");
        formatted.Should().Contain("<c>Enter</c>");
        formatted.Should().Contain("<c>.candidates/</c>");
    }
}

