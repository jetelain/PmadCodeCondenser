using Microsoft.Extensions.AI;
using Pmad.CodeCondenser.Core.Configuration;

namespace Pmad.CodeCondenser.Infrastructure.Llm;

/// <summary>
/// Factory interface to create IChatClient instances for LLM communication.
/// </summary>
public interface ILlmClientFactory
{
    IChatClient CreateClient(CodeCondenserSettings settings);
}
