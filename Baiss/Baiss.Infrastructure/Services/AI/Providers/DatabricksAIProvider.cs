using Baiss.Application.Interfaces;
using Baiss.Application.Models.AI;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Baiss.Infrastructure.Services.AI.Providers;

public class DatabricksAIProvider : IDatabricksAIService
{
    private readonly DatabricksConnector _connector;
    private readonly ILogger<DatabricksAIProvider> _logger;

    public DatabricksAIProvider(
        DatabricksConnector connector,
        ILogger<DatabricksAIProvider> logger)
    {
        _connector = connector;
        _logger = logger;
    }

    public async Task<AICompletionResponse> GetCompletionAsync(
        string prompt,
        AIRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting completion from Databricks for prompt: {Prompt}", prompt);

        try
        {
            var result = await _connector.GetCompletionAsync(prompt, options, cancellationToken);

            _logger.LogDebug("Databricks completion successful. Content length: {Length}", result.Content.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completion from Databricks");
            return new AICompletionResponse
            {
                Success = false,
                Error = ex.Message,
                Provider = AIProvider.Databricks
            };
        }
    }

    public async IAsyncEnumerable<string> StreamCompletionAsync(
        string prompt,
        AIRequestOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting streaming completion from Databricks for prompt: {Prompt}", prompt);

        await foreach (var token in _connector.StreamCompletionAsync(prompt, options, cancellationToken))
        {
            yield return token;
        }
    }

    public async Task<AICompletionResponse> GetChatResponseAsync(
        IEnumerable<ChatMessage> messages,
        string? systemPrompt = null,
        AIRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting chat response from Databricks for {MessageCount} messages", messages.Count());

        try
        {
            var result = await _connector.GetChatResponseAsync(messages, systemPrompt, options, cancellationToken);

            _logger.LogDebug("Databricks chat response successful. Content length: {Length}", result.Content.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat response from Databricks");
            return new AICompletionResponse
            {
                Success = false,
                Error = ex.Message,
                Provider = AIProvider.Databricks
            };
        }
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> messages,
        string? systemPrompt = null,
        AIRequestOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting streaming chat from Databricks for {MessageCount} messages", messages.Count());

        await foreach (var token in _connector.StreamChatAsync(messages, systemPrompt, options, cancellationToken))
        {
            yield return token;
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            _logger.LogDebug("Checking Databricks health");
            var isHealthy = await _connector.IsHealthyAsync();

            _logger.LogDebug("Databricks health check result: {IsHealthy}", isHealthy);

            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Databricks health");
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetAvailableModelsAsync()
    {
        try
        {
            _logger.LogDebug("Getting available models from Databricks");
            var models = await _connector.GetAvailableModelsAsync();

            _logger.LogDebug("Found {ModelCount} available models in Databricks", models.Count());

            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available models from Databricks");
            return Enumerable.Empty<string>();
        }
    }

    public Task<Dictionary<string, object>> GetModelInfoAsync(string modelName)
    {
        try
        {
            _logger.LogDebug("Getting model info for {ModelName} from Databricks", modelName);

            var modelInfo = new Dictionary<string, object>
            {
                ["name"] = modelName,
                ["provider"] = "databricks",
                ["capabilities"] = new[] { "completion", "chat", "streaming" },
                ["last_checked"] = DateTime.UtcNow
            };

            return Task.FromResult(modelInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model info for {ModelName}", modelName);
            var errorInfo = new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["name"] = modelName,
                ["provider"] = "databricks"
            };
            return Task.FromResult(errorInfo);
        }
    }
}