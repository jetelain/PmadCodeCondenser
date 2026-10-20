namespace Pmad.CodeCondenser.Core.Configuration;

/// <summary>
/// Settings for the Ollama LLM provider.
/// </summary>
public sealed class OllamaSettings
{
    /// <summary>
    /// Endpoint URL for Ollama.
    /// Default is "http://localhost:11434".
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Model tag to use for comment summarization.
    /// Default is "llama3.2:3b".
    /// </summary>
    public string Model { get; set; } = "llama3.2:3b";
}
