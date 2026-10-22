using System.Text;

namespace Pmad.CodeCondenser.Core.Strings;

/// <summary>
/// Generates idiomatic C# identifier names for constants derived from string literal values.
/// </summary>
public static class ConstantNamingHelper
{
    /// <summary>
    /// Suggests a PascalCase constant name from a string literal value.
    /// </summary>
    /// <param name="value">The string literal value.</param>
    /// <returns>A valid C# identifier name.</returns>
    public static string SuggestConstantName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "StringConstant";
        }

        var words = new List<string>();
        var current = new StringBuilder();

        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(c);
            }
            else
            {
                if (current.Length > 0)
                {
                    words.Add(current.ToString());
                    current.Clear();
                    if (words.Count >= 6)
                    {
                        break;
                    }
                }
            }
        }

        if (current.Length > 0 && words.Count < 6)
        {
            words.Add(current.ToString());
        }

        if (words.Count == 0)
        {
            return "StringConstant";
        }

        var sb = new StringBuilder();
        foreach (var word in words)
        {
            if (word.Length > 0)
            {
                sb.Append(char.ToUpperInvariant(word[0]));
                if (word.Length > 1)
                {
                    sb.Append(word[1..]);
                }
            }
        }

        var candidate = sb.ToString();
        if (candidate.Length == 0)
        {
            return "StringConstant";
        }

        if (char.IsDigit(candidate[0]))
        {
            candidate = "String_" + candidate;
        }

        if (candidate.Length > 40)
        {
            candidate = candidate[..40];
        }

        return candidate;
    }
}
