namespace Pmad.CodeCondenser.Cli.Formatting;

/// <summary>
/// Normalizes file paths for machine-readable reports (SARIF, JSON) to ensure relative forward-slash URIs.
/// </summary>
public static class ReportPathHelper
{
    /// <summary>
    /// Converts a file path into a repository-relative path with forward slashes without leading slashes.
    /// </summary>
    public static string NormalizeRelativePath(string path, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normPath = path.Replace('\\', '/');
        var normBase = baseDirectory?.Replace('\\', '/').TrimEnd('/') ?? string.Empty;

        string rel;
        if (!string.IsNullOrEmpty(normBase) && normPath.StartsWith(normBase + "/", StringComparison.OrdinalIgnoreCase))
        {
            rel = normPath[(normBase.Length + 1)..];
        }
        else if (!string.IsNullOrEmpty(normBase) && string.Equals(normPath, normBase, StringComparison.OrdinalIgnoreCase))
        {
            rel = string.Empty;
        }
        else
        {
            try
            {
                if (Path.IsPathRooted(path) && !string.IsNullOrWhiteSpace(baseDirectory))
                {
                    rel = Path.GetRelativePath(baseDirectory, path).Replace('\\', '/');
                }
                else
                {
                    rel = normPath;
                }
            }
            catch
            {
                rel = normPath;
            }
        }

        while (rel.StartsWith("./", StringComparison.Ordinal))
        {
            rel = rel[2..];
        }

        while (rel.StartsWith('/'))
        {
            rel = rel[1..];
        }

        return rel;
    }
}
