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
    /// Default is "gemma4:e4b".
    /// </summary>
    public string Model { get; set; } = "gemma4:e4b";

    /// <summary>
    /// Context window size (num_ctx) for Ollama model requests.
    /// Default is 24576 (24k tokens).
    /// </summary>
    public int ContextSize { get; set; } = 24576;
}
