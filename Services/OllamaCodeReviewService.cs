using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OfflineDotNetCodeReviewer.Models;
using OfflineDotNetCodeReviewer.Prompts;

namespace OfflineDotNetCodeReviewer.Services;

public sealed class OllamaException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class OllamaCodeReviewService : ICodeReviewService, IDisposable
{
    public const string Model = "qwen2.5-coder:7b";
    public const string BaseUrl = "http://localhost:11434";

    private static readonly string[] Categories =
        ["Bug", "Security", "Code Smell", "Performance", "Maintainability", "Testing"];

    private static readonly string[] Severities = ["Low", "Medium", "High", "Critical"];

    private static readonly object ResponseSchema = new
    {
        type = "object",
        properties = new
        {
            summary = new { type = "string" },
            issues = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", @enum = Categories },
                        severity = new { type = "string", @enum = Severities },
                        explanation = new { type = "string" },
                        suggestedFix = new { type = "string" }
                    },
                    required = new[] { "category", "severity", "explanation", "suggestedFix" }
                }
            }
        },
        required = new[] { "summary", "issues" }
    };

    private static readonly JsonSerializerOptions ParseOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(BaseUrl),
        Timeout = TimeSpan.FromMinutes(5)
    };

    public async Task<CodeReviewResult> ReviewAsync(string fileName, string sourceCode, CancellationToken ct = default)
    {
        var request = new
        {
            model = Model,
            stream = false,
            format = ResponseSchema,
            options = new { temperature = 0 },
            messages = new[]
            {
                new { role = "system", content = CodeReviewPrompt.SystemPrompt },
                new { role = "user", content = CodeReviewPrompt.BuildUserMessage(fileName, sourceCode) }
            }
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("/api/chat", request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new OllamaException($"Could not reach Ollama at {BaseUrl}.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new OllamaException(
                $"Ollama did not respond within {_http.Timeout.TotalMinutes:0} minutes.", ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new OllamaException($"Ollama is running but the model '{Model}' was not found.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new OllamaException($"Ollama returned HTTP {(int)response.StatusCode}: {body}");
            }

            return ParseReview(ExtractContent(body));
        }
    }

    public void Dispose() => _http.Dispose();

    private static string ExtractContent(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new OllamaException("Ollama returned a response in an unexpected format.", ex);
        }
    }

    private static CodeReviewResult ParseReview(string content)
    {
        var result = TryDeserialize(content);

        if (result is null)
        {
            var start = content.IndexOf('{');
            var end = content.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                result = TryDeserialize(content[start..(end + 1)]);
            }
        }

        if (result is null)
        {
            return new CodeReviewResult(
                $"The model response could not be parsed as a structured review. Raw response:{Environment.NewLine}{content}",
                []);
        }

        var issues = (result.Issues ?? [])
            .Where(issue => issue is not null)
            .Select(issue => new CodeIssue(
                issue.Category ?? "Unknown",
                issue.Severity ?? "Unknown",
                issue.Explanation ?? string.Empty,
                issue.SuggestedFix ?? string.Empty))
            .ToList();

        return new CodeReviewResult(result.Summary ?? string.Empty, issues);
    }

    private static CodeReviewResult? TryDeserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<CodeReviewResult>(json, ParseOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
