using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Pmad.CodeCondenser.Core.Comments;

/// <summary>
/// Formats XML documentation comments to ensure missing &lt;summary&gt; tags are added
/// and lines do not exceed the configured maximum column length (default: 120 columns).
/// </summary>
public static class XmlDocumentationFormatter
{
    public const int DefaultMaxLineLength = 120;

    /// <summary>
    /// Formats an XML documentation comment string, ensuring &lt;summary&gt; tags exist
    /// and all lines stay within the specified maximum column length when prefixed with indentation.
    /// </summary>
    /// <param name="commentText">The raw XML documentation comment text.</param>
    /// <param name="indentation">The leading whitespace indentation of the comment.</param>
    /// <param name="maxLineLength">The maximum allowed column length per line (default: 120).</param>
    /// <param name="lineEnding">The line ending to use (default: \r\n).</param>
    /// <returns>The formatted XML documentation comment.</returns>
    public static string Format(
        string commentText,
        string indentation = "",
        int maxLineLength = DefaultMaxLineLength,
        string lineEnding = "\r\n")
    {
        var lines = FormatLines(commentText, indentation, maxLineLength);
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        // Line 0 is returned without leading indentation because Roslyn rewriter attaches
        // the token's preceding whitespace trivia to line 0. Lines > 0 are prefixed with indentation.
        var formatted = new List<string>(lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (i > 0 && !string.IsNullOrEmpty(indentation))
            {
                line = indentation + line;
            }
            formatted.Add(line);
        }

        return string.Join(lineEnding, formatted);
    }

    /// <summary>
    /// Formats an XML documentation comment into individual lines starting with '/// ',
    /// guaranteeing each line (when combined with <paramref name="indentation"/>) does not exceed <paramref name="maxLineLength"/>.
    /// </summary>
    /// <param name="commentText">The raw comment text.</param>
    /// <param name="indentation">Leading whitespace indentation.</param>
    /// <param name="maxLineLength">The maximum column width.</param>
    /// <returns>A list of lines starting with '/// '.</returns>
    public static List<string> FormatLines(
        string commentText,
        string indentation = "",
        int maxLineLength = DefaultMaxLineLength)
    {
        if (string.IsNullOrWhiteSpace(commentText))
        {
            return [];
        }

        indentation ??= string.Empty;
        if (maxLineLength < 40)
        {
            maxLineLength = DefaultMaxLineLength;
        }

        int availableWidth = Math.Max(20, maxLineLength - indentation.Length - "/// ".Length);

        // 1. Clean comment text by stripping comment markers (///, //!, /**, *, */)
        var xmlContent = XmlDocumentationParser.ExtractXmlText(commentText).Trim();
        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            return ["/// <summary>", "/// </summary>"];
        }

        // 2. Ensure <summary> tag is present
        xmlContent = EnsureSummaryTag(xmlContent);

        // 3. Parse XML root elements and format each
        if (TryParseElements(xmlContent, out var elements) && elements.Count > 0)
        {
            var resultLines = new List<string>();

            foreach (var elem in elements)
            {
                FormatElement(elem, availableWidth, resultLines);
            }

            return resultLines;
        }

        // Fallback: line-by-line wrapping if XML parsing failed
        return FallbackFormatLines(xmlContent, availableWidth);
    }

    private static string EnsureSummaryTag(string xmlContent)
    {
        // Check if <summary> or <summary ...> is already present
        if (Regex.IsMatch(xmlContent, @"<\s*summary[\s>]", RegexOptions.IgnoreCase))
        {
            return xmlContent;
        }

        // Check if there are other root XML tags (e.g. <remarks>, <param>, <returns>)
        var firstTagMatch = Regex.Match(xmlContent, @"<\s*([a-zA-Z0-9_\-]+)[\s>]");
        if (firstTagMatch.Success)
        {
            int tagIndex = firstTagMatch.Index;
            var textBefore = xmlContent[..tagIndex].Trim();
            var remainder = xmlContent[tagIndex..].Trim();

            if (!string.IsNullOrWhiteSpace(textBefore))
            {
                return $"<summary>{textBefore}</summary>\r\n{remainder}";
            }

            // If there's no text before the first tag, wrap the first tag if it's not a known standard tag or wrap entire remainder
            var tagName = firstTagMatch.Groups[1].Value;
            if (string.Equals(tagName, "remarks", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tagName, "param", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tagName, "returns", StringComparison.OrdinalIgnoreCase))
            {
                // Has standard tags but missing summary; don't break existing tags
                return xmlContent;
            }
        }

        // No root XML tags found, wrap entire text in <summary>...</summary>
        return $"<summary>{xmlContent}</summary>";
    }

    private static bool TryParseElements(string xmlContent, out List<XElement> elements)
    {
        elements = new List<XElement>();
        try
        {
            var wrapped = $"<root>{xmlContent}</root>";
            var doc = XDocument.Parse(wrapped, LoadOptions.PreserveWhitespace);
            if (doc.Root == null)
            {
                return false;
            }

            foreach (var node in doc.Root.Nodes())
            {
                if (node is XElement elem)
                {
                    elements.Add(elem);
                }
                else if (node is XText text && !string.IsNullOrWhiteSpace(text.Value))
                {
                    elements.Add(new XElement("summary", text.Value.Trim()));
                }
            }

            return elements.Count > 0;
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    private static void FormatElement(XElement elem, int availableWidth, List<string> resultLines)
    {
        var tagName = elem.Name.LocalName;
        var attributes = elem.Attributes().ToList();
        var attrString = attributes.Count > 0
            ? " " + string.Join(" ", attributes.Select(a => $"{a.Name.LocalName}=\"{a.Value}\""))
            : string.Empty;

        var openTag = $"<{tagName}{attrString}>";
        var closeTag = $"</{tagName}>";

        // Read inner XML preserving inline tags like <see cref="..."/> and <c>...</c>
        string innerXml;
        using (var reader = elem.CreateReader())
        {
            reader.MoveToContent();
            innerXml = reader.ReadInnerXml();
        }

        bool wasMultiline = innerXml.Contains('\n') || innerXml.Contains('\r');
        innerXml = innerXml.Trim();
        var tokens = TokenizeXmlContent(innerXml);

        // Check if single line fits within available width
        var singleLineContent = string.Join(" ", tokens);
        var singleLineTag = $"{openTag}{singleLineContent}{closeTag}";

        if (!wasMultiline && singleLineTag.Length <= availableWidth)
        {
            resultLines.Add($"/// {singleLineTag}");
            return;
        }

        // Multi-line formatting: tag on own line, wrapped lines inside, closing tag on own line
        resultLines.Add($"/// {openTag}");

        var innerLines = innerXml.Replace("\r\n", "\n").Split('\n');
        foreach (var rawLine in innerLines)
        {
            var lineTrimmed = rawLine.Trim();
            if (string.IsNullOrEmpty(lineTrimmed))
            {
                continue;
            }

            var lineTokens = TokenizeXmlContent(lineTrimmed);
            var currentLine = new StringBuilder();

            foreach (var token in lineTokens)
            {
                if (currentLine.Length == 0)
                {
                    currentLine.Append(token);
                }
                else if (currentLine.Length + 1 + token.Length <= availableWidth)
                {
                    currentLine.Append(' ').Append(token);
                }
                else
                {
                    resultLines.Add($"/// {currentLine}");
                    currentLine.Clear();
                    currentLine.Append(token);
                }
            }

            if (currentLine.Length > 0)
            {
                resultLines.Add($"/// {currentLine}");
            }
        }

        resultLines.Add($"/// {closeTag}");
    }

    private static List<string> FallbackFormatLines(string content, int availableWidth)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var result = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (!line.StartsWith("///"))
            {
                line = "/// " + line;
            }

            var text = line[3..].Trim();
            var tokens = TokenizeXmlContent(text);
            var currentLine = new StringBuilder();

            foreach (var token in tokens)
            {
                if (currentLine.Length == 0)
                {
                    currentLine.Append(token);
                }
                else if (currentLine.Length + 1 + token.Length <= availableWidth)
                {
                    currentLine.Append(' ').Append(token);
                }
                else
                {
                    result.Add($"/// {currentLine}");
                    currentLine.Clear();
                    currentLine.Append(token);
                }
            }

            if (currentLine.Length > 0)
            {
                result.Add($"/// {currentLine}");
            }
        }

        return result;
    }

    private static List<string> TokenizeXmlContent(string innerXml)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        bool insideTag = false;

        for (int i = 0; i < innerXml.Length; i++)
        {
            char c = innerXml[i];

            if (c == '<' && !insideTag)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
                insideTag = true;
                current.Append(c);
            }
            else if (c == '>' && insideTag)
            {
                insideTag = false;
                current.Append(c);

                // Attach any immediate punctuation (.,;:!?) or closing quote/parenthesis to the tag token
                while (i + 1 < innerXml.Length && (innerXml[i + 1] == '.' || innerXml[i + 1] == ',' ||
                       innerXml[i + 1] == ';' || innerXml[i + 1] == ':' || innerXml[i + 1] == ')' ||
                       innerXml[i + 1] == '\'' || innerXml[i + 1] == '"'))
                {
                    i++;
                    current.Append(innerXml[i]);
                }

                tokens.Add(current.ToString());
                current.Clear();
            }
            else if (insideTag)
            {
                current.Append(c);
            }
            else if (char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
