using Microsoft.Extensions.AI;
using OllamaSharp;
using Pmad.CodeCondenser.Core.Configuration;

namespace Pmad.CodeCondenser.Infrastructure.Llm;

/// <summary>
/// Creates Ollama-backed IChatClient instances.
/// </summary>
public sealed class OllamaClientFactory : ILlmClientFactory
{
    public IChatClient CreateClient(CodeCondenserSettings settings)
    {
        var uri = new Uri(settings.Ollama.Endpoint);
        return new OllamaApiClient(uri, settings.Ollama.Model);
    }
}
