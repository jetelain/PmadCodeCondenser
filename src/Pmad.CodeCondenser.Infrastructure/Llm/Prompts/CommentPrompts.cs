using Pmad.CodeCondenser.Core.Comments;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Infrastructure.Llm.Prompts;

/// <summary>
/// Provides system and user prompt definitions for condensing verbose C# comments and XML documentation.
/// </summary>
public static class CommentPrompts
{
    public const string RegularCommentSystemPrompt =
        """
        You are a concise C# code condenser. Your task is to summarize verbose code comments into concise, high-value explanations.
        Rules:
        1. Focus strictly on the WHY: explain the underlying rationale, non-obvious constraints, edge-case assumptions, or workarounds.
        2. Never restate the WHAT: do not describe what the code syntax already makes obvious.
        3. Be extremely concise: aim for a single punchy, high-density sentence (ideally under 100 characters).
        4. Output ONLY the summarized comment text. Do NOT wrap in markdown code blocks, and do NOT include preamble or conversational commentary.
        """;

    public const string XmlDocumentationSystemPrompt =
        """
        You are a concise C# code condenser specialized in XML documentation comments.
        Your task is to condense verbose XML documentation comments while strictly preserving valid XML documentation comment structure.
        Rules:
        1. Focus strictly on the WHAT: clearly and compactly describe what the symbol does, its public contract, or its return behavior.
        2. Be extremely concise: <summary> should ideally be a single, direct sentence in active present tense (e.g., "Calculates...", "Resolves...", "Represents...").
        3. Always wrap the summary description inside <summary> and </summary>. Never emit untagged text or inline tags outside of top-level tags.
        4. If critical caveats or technical details are necessary beyond the summary, place them in <remarks>.
        5. Preserve other XML tags (such as <param name="...">, <returns>, <typeparam>, <exception>).
        6. Condense the text payload inside each tag to be direct, precise, and succinct.
        7. Format each line of the output starting with C# XML doc comment slashes (///) and wrap lines so that lines do not exceed 100 columns.
        8. Do not add whitespace inside inline tags such as <c> (e.g. write <c>.github/copilot-instructions.md</c>, never <c> .github... </c> or <c>. github... </c>).
        9. Output ONLY the condensed XML documentation comment. Do NOT wrap in markdown code blocks, and do NOT include preamble or conversational commentary.
        """;

    /// <summary>
    /// Builds the user prompt for a single-line or multi-line comment.
    /// </summary>
    public static string BuildRegularCommentPrompt(string originalComment)
    {
        ArgumentNullException.ThrowIfNull(originalComment);

        return $"""
            Summarize the following verbose C# comment, focusing on the underlying rationale or constraint (the 'why'):

            {originalComment.Trim()}
            """;
    }

    /// <summary>
    /// Builds the user prompt for an XML documentation comment.
    /// </summary>
    public static string BuildXmlDocumentationPrompt(string originalComment)
    {
        ArgumentNullException.ThrowIfNull(originalComment);

        return $"""
            Condense the following verbose C# XML documentation comment, focusing on what the symbol does (the 'what'), ensuring <summary> tags are included and /// formatting is preserved:

            {originalComment.Trim()}
            """;
    }

    /// <summary>
    /// Cleans and sanitizes raw model output, removing markdown code fences and ensuring proper formatting.
    /// </summary>
    public static string CleanModelResponse(string rawResponse, CommentKind kind)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return string.Empty;
        }

        var text = rawResponse.Trim();

        // Strip thinking monologue if present (e.g. <think>...</think> or ...</think>)
        int thinkEnd = text.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
        if (thinkEnd >= 0)
        {
            text = text[(thinkEnd + "</think>".Length)..].Trim();
        }
        else if (text.StartsWith("<think>", StringComparison.OrdinalIgnoreCase))
        {
            text = text["<think>".Length..].Trim();
        }

        // Strip markdown code fences if present (e.g. ```csharp ... ``` or ```xml ... ``` or ``` ... ```)
        int firstFence = text.IndexOf("```", StringComparison.Ordinal);
        if (firstFence >= 0)
        {
            int lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence > firstFence)
            {
                var inside = text[(firstFence + 3)..lastFence].Trim();
                int firstNewline = inside.IndexOf('\n');
                if (firstNewline >= 0)
                {
                    var firstLine = inside[..firstNewline].Trim();
                    if (firstLine.Equals("csharp", StringComparison.OrdinalIgnoreCase) ||
                        firstLine.Equals("cs", StringComparison.OrdinalIgnoreCase) ||
                        firstLine.Equals("xml", StringComparison.OrdinalIgnoreCase) ||
                        firstLine.Equals("c#", StringComparison.OrdinalIgnoreCase))
                    {
                        inside = inside[(firstNewline + 1)..].Trim();
                    }
                }
                text = inside;
            }
            else
            {
                text = text.Replace("```", string.Empty).Trim();
            }
        }

        if (kind == CommentKind.XmlDocumentation)
        {
            return XmlDocumentationFormatter.Format(text, indentation: string.Empty, maxLineLength: XmlDocumentationFormatter.DefaultMaxLineLength);
        }

        return text;
    }
}
