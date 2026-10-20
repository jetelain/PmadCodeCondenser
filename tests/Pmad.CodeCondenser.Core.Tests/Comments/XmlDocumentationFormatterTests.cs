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
}
