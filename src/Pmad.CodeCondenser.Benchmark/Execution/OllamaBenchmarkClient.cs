using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pmad.CodeCondenser.Benchmark.Models;
using Pmad.CodeCondenser.Core.Comments;
using Pmad.CodeCondenser.Core.Models;
using Pmad.CodeCondenser.Infrastructure.Llm.Prompts;

namespace Pmad.CodeCondenser.Benchmark.Execution;

/// <summary>
/// High-performance HTTP client for invoking local Ollama models with precise latency, token counting, and judge scoring.
/// </summary>
public sealed class OllamaBenchmarkClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly int _contextSize;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OllamaBenchmarkClient(string? endpoint = null, int contextSize = 24576, HttpClient? httpClient = null)
    {
        _endpoint = ResolveEndpoint(endpoint);
        _contextSize = contextSize > 0 ? contextSize : 24576;
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    public string Endpoint => _endpoint;
    public int ContextSize => _contextSize;

    /// <summary>
    /// Resolves the effective Ollama endpoint taking OLLAMA_HOST into account.
    /// </summary>
    public static string ResolveEndpoint(string? configuredEndpoint)
    {
        if (!string.IsNullOrWhiteSpace(configuredEndpoint))
        {
            return NormalizeUrl(configuredEndpoint);
        }

        var envHost = Environment.GetEnvironmentVariable("OLLAMA_HOST");
        if (!string.IsNullOrWhiteSpace(envHost))
        {
            return NormalizeUrl(envHost);
        }

        return "http://localhost:11434";
    }

    private static string NormalizeUrl(string input)
    {
        input = input.Trim();
        if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            input = "http://" + input;
        }
        return input.TrimEnd('/');
    }

    /// <summary>
    /// Generates a summary for a comment using the specified candidate model.
    /// </summary>
    public async Task<ModelExecutionResult> GenerateSummaryAsync(
        string modelName,
        BenchmarkComment comment,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        ArgumentNullException.ThrowIfNull(comment);

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

        var requestPayload = new
        {
            model = modelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = false,
            think = false,
            options = new
            {
                temperature = 0.0,
                num_ctx = _contextSize,
                num_predict = 1024
            }
        };

        var result = new ModelExecutionResult
        {
            CommentId = comment.Id,
            Project = comment.Project,
            Kind = comment.Kind,
            ModelName = modelName,
            OriginalLength = comment.CharacterCount,
            OriginalText = comment.OriginalText
        };

        var sw = Stopwatch.StartNew();

        try
        {
            var json = JsonSerializer.Serialize(requestPayload, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{_endpoint}/api/chat";
            using var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);

            sw.Stop();
            result.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                result.Success = false;
                result.ErrorMessage = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}";
                return result;
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            string rawText = string.Empty;
            if (root.TryGetProperty("message", out var messageProp) &&
                messageProp.TryGetProperty("content", out var contentProp))
            {
                rawText = contentProp.GetString() ?? string.Empty;
            }

            result.RawResponse = rawText;

            if (string.IsNullOrWhiteSpace(rawText))
            {
                result.Success = false;
                result.ErrorMessage = "Model generated empty output.";
                result.CleanedSummary = comment.OriginalText.Trim();
                result.SummaryLength = result.CleanedSummary.Length;
                result.IsValidFormat = false;
                result.FormatError = "Empty response.";
                return result;
            }

            var cleaned = CommentPrompts.CleanModelResponse(rawText, comment.Kind);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                cleaned = comment.OriginalText.Trim();
            }

            result.CleanedSummary = cleaned;
            result.SummaryLength = cleaned.Length;
            result.CompressionRatio = comment.CharacterCount > 0
                ? Math.Max(0.0, (double)(comment.CharacterCount - cleaned.Length) / comment.CharacterCount * 100.0)
                : 0.0;

            if (root.TryGetProperty("prompt_eval_count", out var promptEvalProp))
            {
                result.PromptTokens = promptEvalProp.GetInt32();
            }
            if (root.TryGetProperty("eval_count", out var evalCountProp))
            {
                result.CompletionTokens = evalCountProp.GetInt32();
            }
            if (root.TryGetProperty("eval_duration", out var evalDurationProp) && result.CompletionTokens > 0)
            {
                long evalDurationNs = evalDurationProp.GetInt64();
                if (evalDurationNs > 0)
                {
                    double evalSec = evalDurationNs / 1_000_000_000.0;
                    result.TokensPerSecond = Math.Round(result.CompletionTokens / evalSec, 2);
                }
            }
            else if (result.ElapsedMilliseconds > 0 && result.CompletionTokens > 0)
            {
                result.TokensPerSecond = Math.Round(result.CompletionTokens / (result.ElapsedMilliseconds / 1000.0), 2);
            }

            // Validate format
            ValidateFormat(result, comment.Kind);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Evaluates a candidate model's summary using the LLM judge.
    /// </summary>
    public async Task<JudgeEvaluation> EvaluateWithJudgeAsync(
        string judgeModel,
        CommentKind kind,
        string originalComment,
        string generatedSummary,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(judgeModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalComment);

        var evaluation = new JudgeEvaluation
        {
            JudgeModel = judgeModel
        };

        var judgePrompt = $$"""
            You are an expert C# compiler engineer and technical documentation reviewer acting as an impartial LLM judge.
            Your task is to evaluate the quality of a summarized C# code comment generated by an AI model.

            The comment being evaluated is of type: {{kind}}

            Original Comment:
            \"\"\"
            {{originalComment.Trim()}}
            \"\"\"

            Model Generated Summary:
            \"\"\"
            {{generatedSummary.Trim()}}
            \"\"\"

            Evaluation Criteria (rate each from 1.0 to 10.0, where 10 is perfect):

            1. Technical Accuracy & Intent Preservation (1-10):
            - Does the summary preserve the core technical intent, assumptions, contracts, and constraints of the original comment?
            - Are any critical nuances, warnings, or parameter descriptions lost or distorted?

            2. Conciseness & Fluff Removal (1-10):
            - Did the model effectively eliminate conversational filler, trivial restatements of code, and unnecessary verbosity?
            - Is the summary appropriately compact (1-2 clear sentences for regular comments, or succinct tag contents for XML docs)?
            - Does it achieve high information density?

            3. Structural & Formatting Compliance (1-10):
            - If XML documentation: Are the XML tags (<summary>, <param>, <remarks>, <returns>, etc.) properly preserved? Is every non-empty line formatted starting with '///'?
            - If regular comment: Is it formatted as clean comment text without markdown code fences (```csharp), without conversational preambles ("Here is the summary:"), and without commentary?

            4. Overall Quality Score (1-10):
            - Holistic score reflecting whether this summary is production-ready for a high-quality C# codebase.

            Output ONLY a JSON object with this exact structure:
            {
              "accuracy": <number 1.0-10.0>,
              "conciseness": <number 1.0-10.0>,
              "formatting": <number 1.0-10.0>,
              "overall": <number 1.0-10.0>,
              "critique": "<one or two concise sentences explaining the rating, highlighting strengths or issues>"
            }
            """;

        var requestPayload = new
        {
            model = judgeModel,
            messages = new[]
            {
                new { role = "user", content = judgePrompt }
            },
            stream = false,
            think = false,
            options = new
            {
                temperature = 0.0,
                num_ctx = _contextSize
            }
        };

        var sw = Stopwatch.StartNew();

        try
        {
            var json = JsonSerializer.Serialize(requestPayload, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{_endpoint}/api/chat";
            using var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);

            sw.Stop();
            evaluation.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                evaluation.Success = false;
                evaluation.ErrorMessage = $"HTTP {(int)response.StatusCode}: {responseBody}";
                return evaluation;
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            string rawText = string.Empty;
            if (root.TryGetProperty("message", out var messageProp) &&
                messageProp.TryGetProperty("content", out var contentProp))
            {
                rawText = contentProp.GetString() ?? string.Empty;
            }

            ParseJudgeResponse(rawText, evaluation);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            evaluation.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            evaluation.Success = false;
            evaluation.ErrorMessage = ex.Message;
        }

        return evaluation;
    }

    /// <summary>
    /// Parses the JSON or embedded JSON output from the judge model.
    /// </summary>
    public static void ParseJudgeResponse(string rawResponse, JudgeEvaluation evaluation)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            evaluation.Success = false;
            evaluation.ErrorMessage = "Empty judge response.";
            return;
        }

        var text = rawResponse.Trim();

        // Strip thinking monologue if present (e.g. <think>...</think> or ...</think>)
        int thinkEnd = text.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
        if (thinkEnd >= 0)
        {
            text = text[(thinkEnd + "</think>".Length)..].Trim();
        }
        else if (text.StartsWith("<think>", StringComparison.OrdinalIgnoreCase))
        {
            text = text["<think>".Length..].Trim();
        }

        // Extract JSON substring if wrapped in markdown code fence
        int firstBrace = text.IndexOf('{');
        int lastBrace = text.LastIndexOf('}');

        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            var jsonCandidate = text.Substring(firstBrace, lastBrace - firstBrace + 1);
            try
            {
                using var jsonDoc = JsonDocument.Parse(jsonCandidate);
                var root = jsonDoc.RootElement;

                evaluation.AccuracyScore = GetDoubleProperty(root, "accuracy");
                evaluation.ConcisenessScore = GetDoubleProperty(root, "conciseness");
                evaluation.FormatComplianceScore = GetDoubleProperty(root, "formatting");
                evaluation.OverallScore = GetDoubleProperty(root, "overall");

                if (root.TryGetProperty("critique", out var critiqueProp))
                {
                    evaluation.Critique = critiqueProp.GetString() ?? string.Empty;
                }

                evaluation.Success = true;
                return;
            }
            catch (JsonException)
            {
                // Fallback to regex extraction below
            }
        }

        // Fallback regex parsing
        evaluation.AccuracyScore = ExtractNumericValue(text, "accuracy", 5.0);
        evaluation.ConcisenessScore = ExtractNumericValue(text, "conciseness", 5.0);
        evaluation.FormatComplianceScore = ExtractNumericValue(text, "formatting", 5.0);
        evaluation.OverallScore = ExtractNumericValue(text, "overall", 5.0);
        evaluation.Critique = text;
        evaluation.Success = true;
    }

    private static double GetDoubleProperty(JsonElement element, string propName)
    {
        if (element.TryGetProperty(propName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var val))
            {
                return Math.Clamp(val, 0.0, 10.0);
            }
            if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var strVal))
            {
                return Math.Clamp(strVal, 0.0, 10.0);
            }
        }
        return 5.0;
    }

    private static double ExtractNumericValue(string text, string key, double defaultValue)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, $@"""?{key}""?\s*[:=]\s*([0-9]+(?:\.[0-9]+)?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var val))
        {
            return Math.Clamp(val, 0.0, 10.0);
        }
        return defaultValue;
    }

    private static void ValidateFormat(ModelExecutionResult result, CommentKind kind)
    {
        var text = result.CleanedSummary;

        if (string.IsNullOrWhiteSpace(text))
        {
            result.IsValidFormat = false;
            result.FormatError = "Summary is empty.";
            return;
        }

        if (text.Contains("```"))
        {
            result.IsValidFormat = false;
            result.FormatError = "Contains markdown code fence (```).";
            return;
        }

        if (kind == CommentKind.XmlDocumentation)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n');
            bool allHaveSlashes = lines
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .All(l => l.StartsWith("///"));

            if (!allHaveSlashes)
            {
                result.IsValidFormat = false;
                result.FormatError = "Not all lines start with '///'.";
                return;
            }

            var xmlFragment = XmlDocumentationParser.ExtractXmlText(text);
            if (!XmlDocumentationParser.TryParseXmlRootElements(xmlFragment, out _, out _))
            {
                result.IsValidFormat = false;
                result.FormatError = "Malformed XML documentation structure.";
                return;
            }
        }

        result.IsValidFormat = true;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
