using Microsoft.Extensions.AI;

namespace Pmad.CodeCondenser.Infrastructure.Tests.Llm;

internal sealed class TestChatClient : IChatClient
{
    private readonly Func<IEnumerable<ChatMessage>, CancellationToken, Task<string>> _responder;
    private int _concurrentCalls;
    private int _maxObservedConcurrency;
    private int _totalCalls;

    public TestChatClient(Func<IEnumerable<ChatMessage>, string> responder)
        : this((messages, _) => Task.FromResult(responder(messages)))
    {
    }

    public TestChatClient(Func<IEnumerable<ChatMessage>, CancellationToken, Task<string>> responder)
    {
        _responder = responder ?? throw new ArgumentNullException(nameof(responder));
    }

    public int MaxObservedConcurrency => _maxObservedConcurrency;
    public int TotalCalls => _totalCalls;
    public ChatOptions? LastOptions { get; private set; }

    public ChatClientMetadata Metadata => new("TestChatClient");

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        LastOptions = options;
        Interlocked.Increment(ref _totalCalls);
        int current = Interlocked.Increment(ref _concurrentCalls);

        lock (this)
        {
            if (current > _maxObservedConcurrency)
            {
                _maxObservedConcurrency = current;
            }
        }

        try
        {
            var text = await _responder(messages, cancellationToken);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
        }
        finally
        {
            Interlocked.Decrement(ref _concurrentCalls);
        }
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
