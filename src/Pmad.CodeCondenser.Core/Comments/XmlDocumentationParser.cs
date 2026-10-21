using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pmad.CodeCondenser.Core.Configuration;

namespace Pmad.CodeCondenser.Core.Comments;

/// <summary>
/// Parser and analyzer for C# XML documentation comments to check the content length of individual root tags
/// (such as &lt;summary&gt;, &lt;remarks&gt;, &lt;param&gt;) against configured thresholds.
/// </summary>
public static class XmlDocumentationParser
{
    /// <summary>
    /// Extracts the XML content from a raw documentation comment text by stripping comment markers (///, //!, /**, *, */).
    /// </summary>
    /// <param name="commentText">The raw documentation comment text.</param>
    /// <returns>The extracted XML fragment text.</returns>
    public static string ExtractXmlText(string commentText)
    {
        if (string.IsNullOrWhiteSpace(commentText))
        {
            return string.Empty;
        }

        var lines = commentText.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("///"))
            {
                line = line[3..];
                if (line.StartsWith(' ')) line = line[1..];
                sb.AppendLine(line);
            }
            else if (line.StartsWith("//!"))
            {
                line = line[3..];
                if (line.StartsWith(' ')) line = line[1..];
                sb.AppendLine(line);
            }
            else if (line.StartsWith("/**"))
            {
                line = line[3..];
                if (line.EndsWith("*/")) line = line[..^2];
                line = line.Trim();
                if (!string.IsNullOrEmpty(line)) sb.AppendLine(line);
            }
            else if (line.EndsWith("*/"))
            {
                line = line[..^2].Trim();
                if (line.StartsWith('*')) line = line[1..].Trim();
                if (!string.IsNullOrEmpty(line)) sb.AppendLine(line);
            }
            else if (line.StartsWith('*'))
            {
                line = line[1..];
                if (line.StartsWith(' ')) line = line[1..];
                sb.AppendLine(line);
            }
            else
            {
                sb.AppendLine(rawLine);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Normalizes an XML documentation comment or XML text fragment by stripping comment delimiters
    /// and collapsing all interior whitespace and line breaks, producing a canonical whitespace-invariant representation.
    /// </summary>
    /// <param name="commentText">The raw documentation comment text or XML fragment.</param>
    /// <returns>A normalized XML string invariant to line wrapping and indentation.</returns>
    public static string NormalizeXmlText(string commentText)
    {
        if (string.IsNullOrWhiteSpace(commentText))
        {
            return string.Empty;
        }

        var xml = ExtractXmlText(commentText).Trim();
        if (string.IsNullOrWhiteSpace(xml))
        {
            return string.Empty;
        }

        // Collapse any sequence of whitespace characters (including newlines and tabs) to a single space
        var collapsed = Regex.Replace(xml, @"\s+", " ").Trim();

        // Normalize spacing around XML tags
        // Remove spaces between tags: "> <" -> "><"
        collapsed = Regex.Replace(collapsed, @">\s+<", "><");
        // Remove spaces immediately after start tag: "<tag> " -> "<tag>"
        collapsed = Regex.Replace(collapsed, @"<([a-zA-Z0-9_\-]+(?: [^>]*)?)>\s+", "<$1>");
        // Remove spaces immediately before end tag: " </tag>" -> "</tag>"
        collapsed = Regex.Replace(collapsed, @"\s+</([a-zA-Z0-9_\-]+)>", "</$1>");

        return collapsed;
    }

    /// <summary>
    /// Parses root XML elements from an XML text fragment.
    /// </summary>
    /// <param name="xmlContent">The XML text fragment.</param>
    /// <param name="rootTags">The parsed root tag names and their text content.</param>
    /// <param name="textOutsideTags">Any non-whitespace text located outside the root elements.</param>
    /// <returns>True if the XML was parsed successfully; otherwise, false.</returns>
    public static bool TryParseXmlRootElements(
        string xmlContent,
        out List<(string TagName, string Content)> rootTags,
        out string textOutsideTags)
    {
        rootTags = new List<(string TagName, string Content)>();
        textOutsideTags = string.Empty;

        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            return false;
        }

        try
        {
            var wrapped = $"<root>{xmlContent}</root>";
            var doc = XDocument.Parse(wrapped, LoadOptions.PreserveWhitespace);
            if (doc.Root == null)
            {
                return false;
            }

            var textOutsideBuilder = new StringBuilder();

            foreach (var node in doc.Root.Nodes())
            {
                if (node is XElement elem)
                {
                    var content = GetElementText(elem);
                    rootTags.Add((elem.Name.LocalName, content));
                }
                else if (node is XText text)
                {
                    textOutsideBuilder.Append(text.Value);
                }
            }

            textOutsideTags = textOutsideBuilder.ToString();
            return true;
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    /// <summary>
    /// Parses root XML documentation elements using Roslyn syntax trivia structure as fallback.
    /// </summary>
    /// <param name="trivia">The syntax trivia representing documentation comments.</param>
    /// <param name="rootTags">The parsed root tag names and their text content.</param>
    /// <param name="textOutsideTags">Any non-whitespace text located outside the root elements.</param>
    /// <returns>True if any XML tags or text were extracted; otherwise, false.</returns>
    public static bool TryParseFromTrivia(
        SyntaxTrivia trivia,
        out List<(string TagName, string Content)> rootTags,
        out string textOutsideTags)
    {
        rootTags = new List<(string TagName, string Content)>();
        textOutsideTags = string.Empty;

        if (!trivia.HasStructure || trivia.GetStructure() is not DocumentationCommentTriviaSyntax docTrivia)
        {
            return false;
        }

        var textOutsideBuilder = new StringBuilder();

        foreach (var node in docTrivia.Content)
        {
            if (node is XmlElementSyntax elem)
            {
                var tagName = elem.StartTag.Name.LocalName.ValueText;
                var content = GetSyntaxNodeInnerText(elem);
                rootTags.Add((tagName, content));
            }
            else if (node is XmlTextSyntax textSyntax)
            {
                foreach (var token in textSyntax.TextTokens)
                {
                    if (token.IsKind(SyntaxKind.XmlTextLiteralToken))
                    {
                        textOutsideBuilder.Append(token.ValueText);
                    }
                }
            }
        }

        textOutsideTags = textOutsideBuilder.ToString();
        return rootTags.Count > 0;
    }

    /// <summary>
    /// Gets all root XML tags from a documentation comment trivia and/or raw comment text.
    /// </summary>
    /// <param name="trivia">The syntax trivia.</param>
    /// <param name="commentText">The raw comment text.</param>
    /// <param name="textOutsideTags">Any text outside root elements.</param>
    /// <returns>A list of root tag names and their inner text contents.</returns>
    public static IReadOnlyList<(string TagName, string Content)> GetRootTags(
        SyntaxTrivia trivia,
        string commentText,
        out string textOutsideTags)
    {
        var xmlContent = ExtractXmlText(commentText);
        if (TryParseXmlRootElements(xmlContent, out var rootTags, out textOutsideTags) && rootTags.Count > 0)
        {
            return rootTags;
        }

        if (TryParseFromTrivia(trivia, out rootTags, out textOutsideTags) && rootTags.Count > 0)
        {
            return rootTags;
        }

        return Array.Empty<(string TagName, string Content)>();
    }

    /// <summary>
    /// Gets the length threshold corresponding to a specific XML tag name according to condenser settings.
    /// </summary>
    /// <param name="tagName">The XML tag name (e.g. "summary", "remarks", "param").</param>
    /// <param name="settings">The code condenser settings.</param>
    /// <returns>The character threshold for this tag.</returns>
    public static int GetTagThreshold(string tagName, CodeCondenserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.Equals(tagName, "summary", StringComparison.OrdinalIgnoreCase))
        {
            return settings.GetEffectiveXmlSummaryThreshold();
        }

        if (string.Equals(tagName, "remarks", StringComparison.OrdinalIgnoreCase))
        {
            return settings.GetEffectiveXmlRemarksThreshold();
        }

        if (string.Equals(tagName, "param", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tagName, "typeparam", StringComparison.OrdinalIgnoreCase))
        {
            return settings.GetEffectiveXmlParamThreshold();
        }

        return settings.CommentLengthThreshold;
    }

    /// <summary>
    /// Determines whether an XML documentation comment is verbose by checking whether the content of any
    /// root tag exceeds its applicable threshold.
    /// </summary>
    /// <param name="trivia">The syntax trivia.</param>
    /// <param name="commentText">The raw comment text.</param>
    /// <param name="settings">The code condenser settings.</param>
    /// <returns>True if the comment exceeds thresholds and is considered verbose; otherwise, false.</returns>
    public static bool IsVerbose(SyntaxTrivia trivia, string commentText, CodeCondenserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(commentText))
        {
            return false;
        }

        if (CommentIgnoreMarker.IsIgnored(commentText))
        {
            return false;
        }

        var rootTags = GetRootTags(trivia, commentText, out var textOutsideTags);

        if (rootTags.Count > 0)
        {
            foreach (var (tagName, content) in rootTags)
            {
                var trimmed = content.Trim();
                var threshold = GetTagThreshold(tagName, settings);
                if (trimmed.Length >= threshold)
                {
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(textOutsideTags) &&
                textOutsideTags.Trim().Length >= settings.CommentLengthThreshold)
            {
                return true;
            }

            return false;
        }

        // When no XML root tags are detected, fall back to checking the plain text without comment markers
        var plainText = ExtractXmlText(commentText).Trim();
        if (!string.IsNullOrEmpty(plainText))
        {
            return plainText.Length >= settings.CommentLengthThreshold;
        }

        return commentText.Trim().Length >= settings.CommentLengthThreshold;
    }

    private static string GetElementText(XElement elem)
    {
        var sb = new StringBuilder();
        foreach (var node in elem.DescendantNodesAndSelf())
        {
            if (node is XText text)
            {
                sb.Append(text.Value);
            }
            else if (node is XElement child && !child.Nodes().Any())
            {
                var attr = child.Attribute("cref") ?? child.Attribute("name");
                if (attr != null && !string.IsNullOrWhiteSpace(attr.Value))
                {
                    sb.Append(attr.Value);
                }
            }
        }
        return sb.ToString();
    }

    private static string GetSyntaxNodeInnerText(XmlElementSyntax elem)
    {
        var sb = new StringBuilder();
        foreach (var descendant in elem.Content)
        {
            if (descendant is XmlTextSyntax textSyntax)
            {
                foreach (var token in textSyntax.TextTokens)
                {
                    if (token.IsKind(SyntaxKind.XmlTextLiteralToken))
                    {
                        sb.Append(token.ValueText);
                    }
                    else if (token.IsKind(SyntaxKind.XmlTextLiteralNewLineToken))
                    {
                        sb.Append(' ');
                    }
                }
            }
            else if (descendant is XmlElementSyntax childElem)
            {
                sb.Append(GetSyntaxNodeInnerText(childElem));
            }
            else if (descendant is XmlEmptyElementSyntax emptyElem)
            {
                var attr = emptyElem.Attributes.FirstOrDefault();
                if (attr is XmlCrefAttributeSyntax crefAttr)
                {
                    sb.Append(crefAttr.Cref.ToString());
                }
                else if (attr is XmlNameAttributeSyntax nameAttr)
                {
                    sb.Append(nameAttr.Identifier.Identifier.ValueText);
                }
            }
            else if (descendant is XmlCDataSectionSyntax cdata)
            {
                foreach (var token in cdata.TextTokens)
                {
                    sb.Append(token.ValueText);
                }
            }
        }
        return sb.ToString().Trim();
    }
}
