using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Pmad.CodeCondenser.Benchmark.Execution;
using Pmad.CodeCondenser.Benchmark.Models;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Benchmark.Tests.Execution;

public class BenchmarkRunnerTests : IDisposable
{
    private readonly string _tempDir;

    public BenchmarkRunnerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "BenchmarkRunnerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public async Task JudgeExistingRunAsync_OverwritesRunFileWithoutFileLockException()
    {
        var runFilePath = Path.Combine(_tempDir, "benchmark-run.json");
        var initialRun = new BenchmarkRun
        {
            Endpoint = "http://localhost:11434",
            JudgeModel = "initial-judge",
            CandidateModels = ["modelA"],
            Results =
            [
                new ModelExecutionResult
                {
                    CommentId = "c1",
                    Project = "Proj",
                    Kind = CommentKind.SingleLine,
                    ModelName = "modelA",
                    Success = true,
                    OriginalText = "// Original comment text",
                    CleanedSummary = "// Condensed"
                }
            ]
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await File.WriteAllTextAsync(runFilePath, JsonSerializer.Serialize(initialRun, jsonOptions));

        var judgeResponseBody = """
            {
                "message": {
                    "role": "assistant",
                    "content": "{\"accuracy\":9.0,\"conciseness\":9.0,\"formatting\":9.0,\"overall\":9.0,\"critique\":\"Good\"}"
                }
            }
            """;

        var fakeHandler = new FakeHttpMessageHandler((req, ct) =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(judgeResponseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(res);
        });

        using var httpClient = new HttpClient(fakeHandler);
        using var client = new OllamaBenchmarkClient(httpClient: httpClient);
        var runner = new BenchmarkRunner(client);

        // Act - On Windows, if the stream was held open, SaveRunAsync would throw IOException
        var updatedRun = await runner.JudgeExistingRunAsync(runFilePath, "new-judge");

        // Assert
        updatedRun.Should().NotBeNull();
        updatedRun.JudgeModel.Should().Be("new-judge");
        updatedRun.Results[0].Evaluation.Should().NotBeNull();
        updatedRun.Results[0].Evaluation!.OverallScore.Should().Be(9.0);

        // Verify the file was overwritten on disk
        var fileContent = await File.ReadAllTextAsync(runFilePath);
        fileContent.Should().Contain("new-judge");
        fileContent.Should().Contain("\"overallScore\": 9");
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }
}
