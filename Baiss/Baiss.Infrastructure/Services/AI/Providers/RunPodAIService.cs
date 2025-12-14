using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Baiss.Application.Interfaces;
using Baiss.Application.Models.AI;
using Microsoft.Extensions.Logging;

namespace Baiss.Infrastructure.Services.AI.Providers;

public interface IRunPodAIService
{
    Task<AICompletionResponse> GetChatAsync(
        IEnumerable<ChatMessage> messages,
        string? systemPrompt,
        AIRequestOptions? options,
        CancellationToken cancellationToken = default);
}

public class RunPodAIService : IRunPodAIService
{
    private readonly HttpClient _httpClient;
    private readonly SemanticKernelConfig _config;
    private readonly ILogger<RunPodAIService> _logger;

    public RunPodAIService(HttpClient httpClient, SemanticKernelConfig config, ILogger<RunPodAIService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<AICompletionResponse> GetChatAsync(
        IEnumerable<ChatMessage> messages,
        string? systemPrompt,
        AIRequestOptions? options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_config.RunPod.ApiKey) || string.IsNullOrWhiteSpace(_config.RunPod.EndpointId))
        {
            return new AICompletionResponse
            {
                Success = false,
                Error = "RunPod provider is not configured. Ensure API key and endpoint ID are set.",
                Provider = AIProvider.RunPod
            };
        }

        try
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(5, _config.RunPod.TimeoutSeconds));

            var payload = BuildRequestPayload(messages, systemPrompt, options);
            var requestJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var url = $"{_config.RunPod.BaseUrl.TrimEnd('/')}/{_config.RunPod.EndpointId}/runsync";
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.RunPod.ApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("RunPod request failed with status {Status}: {Body}", response.StatusCode, responseBody);
                return new AICompletionResponse
                {
                    Success = false,
                    Error = $"RunPod returned HTTP {(int)response.StatusCode}: {responseBody}",
                    Provider = AIProvider.RunPod
                };
            }

            return ParseResponse(responseBody);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "RunPod request timed out");
            return new AICompletionResponse
            {
                Success = false,
                Error = "RunPod request timed out",
                Provider = AIProvider.RunPod
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RunPod request failed");
            return new AICompletionResponse
            {
                Success = false,
                Error = ex.Message,
                Provider = AIProvider.RunPod
            };
        }
    }

    private static object BuildRequestPayload(
        IEnumerable<ChatMessage> messages,
        string? systemPrompt,
        AIRequestOptions? options)
    {
        var messageList = messages.ToList();
        var flattenedPrompt = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            flattenedPrompt.AppendLine($"[system]\n{systemPrompt}\n");
        }

        foreach (var message in messageList)
        {
            flattenedPrompt.AppendLine($"[{message.Role}] {message.Content}");
        }

        return new
        {
            input = new
            {
                prompt = flattenedPrompt.ToString().Trim(),
                messages = messageList.Select(m => new
                {
                    role = m.Role,
                    content = m.Content
                }),
                systemPrompt,
                temperature = options?.Temperature ?? 0.7,
                maxTokens = options?.MaxTokens ?? 1024,
                topP = options?.TopP ?? 0.9
            }
        };
    }

    private AICompletionResponse ParseResponse(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            var output = root.TryGetProperty("output", out var outputElement)
                ? outputElement
                : root.TryGetProperty("result", out var resultElement) ? resultElement : root;

            var content = ExtractText(output);

            return new AICompletionResponse
            {
                Success = true,
                Content = content ?? string.Empty,
                Provider = AIProvider.RunPod
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse RunPod response: {Body}", responseBody);
            return new AICompletionResponse
            {
                Success = true,
                Content = responseBody,
                Provider = AIProvider.RunPod,
                Error = "RunPod response returned unrecognized JSON format"
            };
        }
    }

    private static string? ExtractText(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Object => ExtractTextFromObject(element),
            JsonValueKind.Array => string.Join("\n", element.EnumerateArray().Select(ExtractText).Where(s => !string.IsNullOrWhiteSpace(s))),
            _ => null
        };
    }

    private static string? ExtractTextFromObject(JsonElement element)
    {
        if (element.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
        {
            return textProp.GetString();
        }

        if (element.TryGetProperty("choices", out var choicesProp) && choicesProp.ValueKind == JsonValueKind.Array)
        {
            var fromChoices = string.Join("\n", choicesProp.EnumerateArray().Select(ExtractText).Where(s => !string.IsNullOrWhiteSpace(s)));
            if (!string.IsNullOrWhiteSpace(fromChoices))
            {
                return fromChoices;
            }
        }

        if (element.TryGetProperty("message", out var messageProp) && messageProp.ValueKind == JsonValueKind.String)
        {
            return messageProp.GetString();
        }

        foreach (var property in element.EnumerateObject())
        {
            var value = ExtractText(property.Value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
