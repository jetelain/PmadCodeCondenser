using FluentAssertions;
using Microsoft.Extensions.AI;
using Pmad.CodeCondenser.Core.Comments;
using Pmad.CodeCondenser.Core.Configuration;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Infrastructure.Llm;
using Pmad.CodeCondenser.Infrastructure.Llm.Prompts;

namespace Pmad.CodeCondenser.Infrastructure.Tests.Llm;

public class CommentSummarizerTests
{
    private static VerboseComment CreateComment(string text, CommentKind kind = CommentKind.SingleLine, string filePath = "test.cs")
    {
        return new VerboseComment(
            FilePath: filePath,
            StartLine: 1,
            EndLine: 1,
            SpanStart: 0,
            SpanLength: text.Length,
            Kind: kind,
            OriginalText: text,
            LeadingWhitespace: "    ");
    }

    [Fact]
    public async Task SummarizeAsync_UncachedComment_InvokesChatClientAndReturnsSummary()
    {
        var originalComment = "// " + new string('x', 120);
        var expectedSummary = "Concise explanation of algorithm.";

        var client = new TestChatClient(messages =>
        {
            var userMsg = messages.First(m => m.Role == ChatRole.User).Text;
            userMsg.Should().Contain(originalComment);
            return expectedSummary;
        });

        var summarizer = new CommentSummarizer(client);
        var comments = new[] { CreateComment(originalComment) };
        var cache = new Dictionary<string, string>();
        var settings = new CodeCondenserSettings();

        var result = await summarizer.SummarizeAsync(comments, cache, settings);

        result.Should().ContainKey(comments[0].ContentHash);
        result[comments[0].ContentHash].Should().Be(expectedSummary);
        client.TotalCalls.Should().Be(1);
    }

    [Fact]
    public async Task SummarizeAsync_CachedComment_DoesNotCallChatClient()
    {
        var originalComment = "// " + new string('y', 120);
        var comment = CreateComment(originalComment);
        var cachedSummary = "Pre-existing summary in snapshot.";

        var client = new TestChatClient(_ => throw new InvalidOperationException("Should not be called for cached comments."));
        var summarizer = new CommentSummarizer(client);

        var cache = new Dictionary<string, string>
        {
            [comment.ContentHash] = cachedSummary
        };

        var result = await summarizer.SummarizeAsync(new[] { comment }, cache, new CodeCondenserSettings());

        result.Should().ContainKey(comment.ContentHash);
        result[comment.ContentHash].Should().Be(cachedSummary);
        client.TotalCalls.Should().Be(0);
    }

    [Fact]
    public async Task SummarizeAsync_IdenticalCommentsInBatch_DeduplicatesAndCallsLlmOnce()
    {
        var text = "// " + new string('z', 120);
        var comment1 = CreateComment(text, filePath: "File1.cs");
        var comment2 = CreateComment(text, filePath: "File2.cs");

        var client = new TestChatClient(_ => "Unified summary");
        var summarizer = new CommentSummarizer(client);

        var comments = new[] { comment1, comment2 };
        var result = await summarizer.SummarizeAsync(comments, new Dictionary<string, string>(), new CodeCondenserSettings());

        result.Should().HaveCount(1);
        result[comment1.ContentHash].Should().Be("Unified summary");
        client.TotalCalls.Should().Be(1);
    }

    [Fact]
    public async Task SummarizeAsync_ConcurrentlyThrottled_RespectsMaxParallelRequests()
    {
        const int maxParallel = 3;
        const int totalComments = 10;

        var client = new TestChatClient(async (_, ct) =>
        {
            await Task.Delay(50, ct);
            return "Summary";
        });

        var summarizer = new CommentSummarizer(client);
        var settings = new CodeCondenserSettings { MaxParallelLlmRequests = maxParallel };

        var comments = Enumerable.Range(0, totalComments)
            .Select(i => CreateComment($"// Comment number {i} " + new string('a', 100)))
            .ToList();

        var result = await summarizer.SummarizeAsync(comments, new Dictionary<string, string>(), settings);

        result.Should().HaveCount(totalComments);
        client.TotalCalls.Should().Be(totalComments);
        client.MaxObservedConcurrency.Should().BeLessThanOrEqualTo(maxParallel);
    }

    [Fact]
    public async Task SummarizeAsync_XmlDocumentationComment_UsesXmlSystemPrompt()
    {
        var xmlComment = "/// <summary>\r\n/// Very long documentation...\r\n/// </summary>";
        var comment = CreateComment(xmlComment, CommentKind.XmlDocumentation);

        var client = new TestChatClient(messages =>
        {
            var systemMsg = messages.First(m => m.Role == ChatRole.System).Text;
            systemMsg.Should().Be(CommentPrompts.XmlDocumentationSystemPrompt);
            return "/// <summary>Short summary.</summary>";
        });

        var summarizer = new CommentSummarizer(client);
        var result = await summarizer.SummarizeAsync(new[] { comment }, new Dictionary<string, string>(), new CodeCondenserSettings());

        result.Should().ContainKey(comment.ContentHash);
        result[comment.ContentHash].Should().Contain("Short summary.");
    }

    [Fact]
    public async Task SummarizeAsync_WhenCancellationTokenCanceled_ThrowsOperationCanceledException()
    {
        var comment = CreateComment("// " + new string('c', 120));
        var client = new TestChatClient(async (_, ct) =>
        {
            await Task.Delay(500, ct);
            return "Summary";
        });

        var summarizer = new CommentSummarizer(client);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => summarizer.SummarizeAsync(new[] { comment }, new Dictionary<string, string>(), new CodeCondenserSettings(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SummarizeAsync_NullArguments_ThrowsArgumentNullException()
    {
        var client = new TestChatClient(_ => "Summary");
        var summarizer = new CommentSummarizer(client);
        var settings = new CodeCondenserSettings();
        var comments = new List<VerboseComment>();
        var cache = new Dictionary<string, string>();

        var act1 = () => summarizer.SummarizeAsync(null!, cache, settings);
        var act2 = () => summarizer.SummarizeAsync(comments, null!, settings);
        var act3 = () => summarizer.SummarizeAsync(comments, cache, null!);

        await act1.Should().ThrowAsync<ArgumentNullException>();
        await act2.Should().ThrowAsync<ArgumentNullException>();
        await act3.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SummarizeAsync_DefaultSettings_PassesContextSize24576AndDisablesThinking()
    {
        var client = new TestChatClient(_ => "Summary");
        var summarizer = new CommentSummarizer(client);
        var comments = new[] { CreateComment("// " + new string('d', 120)) };
        var settings = new CodeCondenserSettings();

        await summarizer.SummarizeAsync(comments, new Dictionary<string, string>(), settings);

        client.LastOptions.Should().NotBeNull();
        client.LastOptions!.AdditionalProperties.Should().NotBeNull();
        client.LastOptions.AdditionalProperties!["think"].Should().Be(false);
        client.LastOptions.AdditionalProperties["num_ctx"].Should().Be(24576);
    }

    [Fact]
    public async Task SummarizeAsync_CustomContextSize_PassesCustomContextSizeToChatOptions()
    {
        var client = new TestChatClient(_ => "Summary");
        var summarizer = new CommentSummarizer(client);
        var comments = new[] { CreateComment("// " + new string('e', 120)) };
        var settings = new CodeCondenserSettings
        {
            Ollama = { ContextSize = 16384 }
        };

        await summarizer.SummarizeAsync(comments, new Dictionary<string, string>(), settings);

        client.LastOptions.Should().NotBeNull();
        client.LastOptions!.AdditionalProperties.Should().NotBeNull();
        client.LastOptions.AdditionalProperties!["think"].Should().Be(false);
        client.LastOptions.AdditionalProperties["num_ctx"].Should().Be(16384);
    }

    [Fact]
    public async Task OllamaApiClient_ChatOptionsMapping_IncludesNumCtxAndDisablesThinkingInJsonPayload()
    {
        string capturedJson = "";
        var handler = new DelegatingHandlerStub(async (request, ct) =>
        {
            if (request.Content != null)
            {
                capturedJson = await request.Content.ReadAsStringAsync(ct);
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"message\":{\"role\":\"assistant\",\"content\":\"hello\"},\"done\":true}")
            };
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        var ollama = new OllamaSharp.OllamaApiClient(http, "qwen3.5:4b", (System.Text.Json.Serialization.JsonSerializerContext?)null);
        var chatClient = (Microsoft.Extensions.AI.IChatClient)ollama;

        var chatOptions = new ChatOptions
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["think"] = false,
                ["num_ctx"] = 24576
            }
        };

        var response = await chatClient.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") }, chatOptions);

        response.Should().NotBeNull();
        capturedJson.Should().Contain("\"num_ctx\":24576");
        capturedJson.Should().Contain("\"think\":false");
    }

    [Fact]
    public async Task SummarizeAsync_WithProgress_ReportsCompletedAndTotal()
    {
        var comment1 = CreateComment("// " + new string('a', 120), filePath: "a.cs");
        var comment2 = CreateComment("// " + new string('b', 120), filePath: "b.cs");

        var client = new TestChatClient(_ => "Summary");
        var summarizer = new CommentSummarizer(client);
        var progressReports = new List<CommentSummarizationProgress>();
        var progress = new DirectProgress<CommentSummarizationProgress>(p =>
        {
            lock (progressReports)
            {
                progressReports.Add(p);
            }
        });

        var result = await summarizer.SummarizeAsync(
            [comment1, comment2],
            new Dictionary<string, string>(),
            new CodeCondenserSettings(),
            progress: progress);

        result.Should().HaveCount(2);
        progressReports.Should().NotBeEmpty();
        progressReports.First().Total.Should().Be(2);
        progressReports.Last().Completed.Should().Be(2);
        progressReports.Last().Percentage.Should().Be(100);
    }

    [Fact]
    public async Task SummarizeAsync_WhenLlmOutputIsMoreVerboseThanOriginalComment_KeepsOldComment()
    {
        var originalComment = "// Short comment that was flagged.";
        var comment = CreateComment(originalComment);

        // LLM returns a more verbose explanation
        var verboseLlmOutput = "// This is an unnecessarily long explanation that adds lots of redundant filler words to the comment.";

        var client = new TestChatClient(_ => verboseLlmOutput);
        var summarizer = new CommentSummarizer(client);

        var result = await summarizer.SummarizeAsync(new[] { comment }, new Dictionary<string, string>(), new CodeCondenserSettings());

        result.Should().ContainKey(comment.ContentHash);
        // The guard must keep the old comment
        result[comment.ContentHash].Should().Be(originalComment.Trim());
    }

    [Fact]
    public async Task SummarizeAsync_WhenLlmOutputIsMoreVerboseXmlDoc_KeepsOldComment()
    {
        var originalComment = "/// <summary>\r\n/// Brief method summary.\r\n/// </summary>";
        var comment = CreateComment(originalComment, CommentKind.XmlDocumentation);

        // LLM returns a much longer XML doc comment
        var verboseLlmOutput = "/// <summary>\r\n/// This method performs an exceptionally detailed operation with multiple stages of validation, caching, and serialization.\r\n/// </summary>";

        var client = new TestChatClient(_ => verboseLlmOutput);
        var summarizer = new CommentSummarizer(client);

        var result = await summarizer.SummarizeAsync(new[] { comment }, new Dictionary<string, string>(), new CodeCondenserSettings());

        result.Should().ContainKey(comment.ContentHash);
        // The guard must keep the old comment
        result[comment.ContentHash].Should().Be(originalComment.Trim());
    }

    [Fact]
    public async Task SummarizeAsync_WhenFirstAttemptIsMoreVerbose_RetriesAndReturnsShorterSecondAttempt()
    {
        var originalComment = "// Flagged comment that needs summarizing.";
        var comment = CreateComment(originalComment);

        var callCount = 0;
        ChatMessage? capturedRetryUserMessage = null;

        var client = new TestChatClient(messages =>
        {
            callCount++;
            if (callCount == 1)
            {
                return "// This is an excessively long and unnecessarily wordy first response from LLM.";
            }

            capturedRetryUserMessage = messages.LastOrDefault(m => m.Role == ChatRole.User);
            return "// Concise explanation.";
        });

        var summarizer = new CommentSummarizer(client);
        var settings = new CodeCondenserSettings { MaxSummarizationRetries = 1 };

        var result = await summarizer.SummarizeAsync([comment], new Dictionary<string, string>(), settings);

        client.TotalCalls.Should().Be(2);
        capturedRetryUserMessage.Should().NotBeNull();
        capturedRetryUserMessage!.Text.Should().Contain("drop secondary details");
        result[comment.ContentHash].Should().Be("// Concise explanation.");
    }

    [Fact]
    public async Task SummarizeAsync_WhenReductionAboveTargetAndAbove50Percent_RetriesAndReturnsSecondAttempt()
    {
        // Original = 200 chars, Target = 100
        var originalComment = "// " + new string('x', 197);
        var comment = CreateComment(originalComment);

        var callCount = 0;
        var client = new TestChatClient(messages =>
        {
            callCount++;
            if (callCount == 1)
            {
                // Attempt 1: 130 chars (> 100 target and > 100 50% of initial) -> Should trigger retry
                return "// " + new string('y', 127);
            }

            // Attempt 2: 60 chars (<= 100 target) -> Good summary
            return "// " + new string('z', 57);
        });

        var summarizer = new CommentSummarizer(client);
        var settings = new CodeCondenserSettings { CommentLengthThreshold = 100, MaxSummarizationRetries = 1 };

        var result = await summarizer.SummarizeAsync([comment], new Dictionary<string, string>(), settings);

        client.TotalCalls.Should().Be(2);
        result[comment.ContentHash].Should().Be("// " + new string('z', 57));
    }

    [Fact]
    public async Task SummarizeAsync_WhenLlmAnswersCommentIsUseless_ReturnsUselessMarkerWithoutRetrying()
    {
        var comment = CreateComment("// Redundant comment that restates the obvious.");

        var client = new TestChatClient(_ => "Comment is useless");
        var summarizer = new CommentSummarizer(client);
        var settings = new CodeCondenserSettings { MaxSummarizationRetries = 2 };

        var result = await summarizer.SummarizeAsync([comment], new Dictionary<string, string>(), settings);

        client.TotalCalls.Should().Be(1);
        result[comment.ContentHash].Should().Be("Comment is useless");
    }

    [Fact]
    public async Task SummarizeAsync_WhenLlmAnswersCommentIsUslessTypo_ReturnsUselessMarkerWithoutRetrying()
    {
        var comment = CreateComment("// Redundant comment that restates the obvious.");

        var client = new TestChatClient(_ => "Comment is usless");
        var summarizer = new CommentSummarizer(client);
        var settings = new CodeCondenserSettings { MaxSummarizationRetries = 2 };

        var result = await summarizer.SummarizeAsync([comment], new Dictionary<string, string>(), settings);

        client.TotalCalls.Should().Be(1);
        result[comment.ContentHash].Should().Be("Comment is useless");
    }

    [Fact]
    public async Task SummarizeAsync_WhenMaxSummarizationRetriesIsZero_DoesNotRetry()
    {
        var originalComment = "// Short comment.";
        var comment = CreateComment(originalComment);

        var client = new TestChatClient(_ => "// Much longer and more verbose comment produced by LLM.");
        var summarizer = new CommentSummarizer(client);
        var settings = new CodeCondenserSettings { MaxSummarizationRetries = 0 };

        var result = await summarizer.SummarizeAsync([comment], new Dictionary<string, string>(), settings);

        client.TotalCalls.Should().Be(1);
        result[comment.ContentHash].Should().Be(originalComment.Trim());
    }

    [Fact]
    public async Task SummarizeAsync_WhenRetriesExhausted_ReturnsBestCandidateOrOriginal()
    {
        var originalComment = "// Initial comment length 30 chars";
        var comment = CreateComment(originalComment);

        var callCount = 0;
        var client = new TestChatClient(_ =>
        {
            callCount++;
            return $"// Attempt {callCount} is much longer and more verbose than the original comment was.";
        });

        var summarizer = new CommentSummarizer(client);
        var settings = new CodeCondenserSettings { MaxSummarizationRetries = 2 };

        var result = await summarizer.SummarizeAsync([comment], new Dictionary<string, string>(), settings);

        // 1 initial attempt + 2 retries = 3 calls
        client.TotalCalls.Should().Be(3);
        result[comment.ContentHash].Should().Be(originalComment.Trim());
    }

    private sealed class DirectProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    private class DelegatingHandlerStub : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;
        public DelegatingHandlerStub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) => _handler = handler;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => _handler(request, cancellationToken);
    }
}
