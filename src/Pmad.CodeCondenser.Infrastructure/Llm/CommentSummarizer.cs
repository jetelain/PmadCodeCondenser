using Microsoft.Extensions.AI;
using Pmad.CodeCondenser.Core.Comments;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Infrastructure.Llm.Prompts;

namespace Pmad.CodeCondenser.Infrastructure.Llm;

/// <summary>
/// Implements comment summarization using an IChatClient with concurrency throttling and snapshot cache support.
/// </summary>
public sealed class CommentSummarizer : ICommentSummarizer
{
    private readonly IChatClient? _chatClient;
    private readonly ILlmClientFactory? _clientFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="CommentSummarizer"/> using the default <see cref="OllamaClientFactory"/>.
    /// </summary>
    public CommentSummarizer() : this(new OllamaClientFactory())
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CommentSummarizer"/> with a specific <see cref="IChatClient"/>.
    /// </summary>
    /// <param name="chatClient">The chat client instance to use for summarization requests.</param>
    public CommentSummarizer(IChatClient chatClient)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CommentSummarizer"/> with a client factory.
    /// </summary>
    /// <param name="clientFactory">The factory to create chat clients based on settings.</param>
    public CommentSummarizer(ILlmClientFactory clientFactory)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> SummarizeAsync(
        IReadOnlyList<VerboseComment> comments,
        IReadOnlyDictionary<string, string> existingCache,
        CodeCondenserSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comments);
        ArgumentNullException.ThrowIfNull(existingCache);
        ArgumentNullException.ThrowIfNull(settings);

        var client = _chatClient ?? _clientFactory?.CreateClient(settings)
            ?? throw new InvalidOperationException("No IChatClient or ILlmClientFactory configured.");

        var results = new Dictionary<string, string>(existingCache);

        // Deduplicate comments by ContentHash and filter out any comments already present in cache
        var uncachedComments = comments
            .GroupBy(c => c.ContentHash)
            .Where(g => !results.ContainsKey(g.Key))
            .Select(g => g.First())
            .ToList();

        if (uncachedComments.Count == 0)
        {
            return results;
        }

        int maxParallelism = Math.Max(1, settings.MaxParallelLlmRequests);
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxParallelism,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(uncachedComments, parallelOptions, async (comment, ct) =>
        {
            try
            {
                var summary = await SummarizeCommentAsync(client, comment, ct).ConfigureAwait(false);
                lock (results)
                {
                    results[comment.ContentHash] = summary;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Warning] LLM summarization request failed: {ex.Message}. Skipping summarization for this comment.");
            }
        }).ConfigureAwait(false);

        return results;
    }

    private static async Task<string> SummarizeCommentAsync(
        IChatClient client,
        VerboseComment comment,
        CancellationToken cancellationToken)
    {
        string systemPrompt;
        string userPrompt;

        if (comment.Kind == CommentKind.XmlDocumentation)
        {
            systemPrompt = CommentPrompts.XmlDocumentationSystemPrompt;
            userPrompt = CommentPrompts.BuildXmlDocumentationPrompt(comment.OriginalText);
        }
        else
        {
            systemPrompt = CommentPrompts.RegularCommentSystemPrompt;
            userPrompt = CommentPrompts.BuildRegularCommentPrompt(comment.OriginalText);
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };

        var response = await client.GetResponseAsync(messages, cancellationToken: cancellationToken).ConfigureAwait(false);
        var rawText = response.Text ?? string.Empty;
        var cleaned = CommentPrompts.CleanModelResponse(rawText, comment.Kind);

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = comment.OriginalText.Trim();
        }

        return cleaned;
    }
}
