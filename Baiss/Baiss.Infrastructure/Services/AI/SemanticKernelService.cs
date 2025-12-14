using Baiss.Application.Interfaces;
using Baiss.Application.Models.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Runtime.CompilerServices;

namespace Baiss.Infrastructure.Services.AI;

public class SemanticKernelService : ISemanticKernelService
{
    private readonly Dictionary<AIProvider, Kernel> _kernels;
    private readonly SemanticKernelConfig _config;
    private readonly IDatabricksAIService _databricksService;
    private readonly ILogger<SemanticKernelService> _logger;

    public SemanticKernelService(
        SemanticKernelConfig config,
        IDatabricksAIService databricksService,
        ILogger<SemanticKernelService> logger)
    {
        _config = config;
        _databricksService = databricksService;
        _logger = logger;
        _kernels = new Dictionary<AIProvider, Kernel>();

        InitializeKernels();
    }

    private void InitializeKernels()
    {
        try
        {
            if (!string.IsNullOrEmpty(_config.OpenAI.ApiKey))
            {
                var openAIBuilder = Kernel.CreateBuilder();
                openAIBuilder.AddOpenAIChatCompletion(
                    _config.OpenAI.Model,
                    _config.OpenAI.ApiKey,
                    _config.OpenAI.OrganizationId);

                _kernels[AIProvider.OpenAI] = openAIBuilder.Build();
                _logger.LogInformation("Initialized OpenAI kernel with model {Model}", _config.OpenAI.Model);
            }

            if (!string.IsNullOrEmpty(_config.Anthropic.ApiKey))
            {
                _logger.LogWarning("Anthropic support requires additional packages - using OpenAI connector as fallback");
            }

            if (!string.IsNullOrEmpty(_config.AzureOpenAI.ApiKey))
            {
                var azureBuilder = Kernel.CreateBuilder();
                azureBuilder.AddAzureOpenAIChatCompletion(
                    _config.AzureOpenAI.DeploymentName,
                    _config.AzureOpenAI.Endpoint,
                    _config.AzureOpenAI.ApiKey);

                _kernels[AIProvider.AzureOpenAI] = azureBuilder.Build();
                _logger.LogInformation("Initialized Azure OpenAI kernel with deployment {Deployment}", _config.AzureOpenAI.DeploymentName);
            }

            _logger.LogInformation("Initialized {KernelCount} Semantic Kernel instances", _kernels.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Semantic Kernel instances");
        }
    }

    public async Task<AICompletionResponse> GetCompletionAsync(
        string prompt,
        AIProvider? provider = null,
        AIRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var selectedProvider = provider ?? _config.DefaultProvider;

        _logger.LogDebug("Getting completion with provider {Provider} for prompt: {Prompt}", selectedProvider, prompt);

        try
        {
            if (selectedProvider == AIProvider.Databricks)
            {
                return await _databricksService.GetCompletionAsync(prompt, options, cancellationToken);
            }

            if (!_kernels.TryGetValue(selectedProvider, out var kernel))
            {
                return new AICompletionResponse
                {
                    Success = false,
                    Error = $"Provider {selectedProvider} is not available or configured",
                    Provider = selectedProvider
                };
            }

            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var settings = CreateExecutionSettings(options);

            var result = await chatService.GetChatMessageContentAsync(prompt, settings, kernel, cancellationToken);

            return new AICompletionResponse
            {
                Success = true,
                Content = result.Content ?? string.Empty,
                Provider = selectedProvider,
                Usage = ExtractUsage(result),
                FinishReason = result.Metadata?.ContainsKey("FinishReason") == true ?
                    result.Metadata["FinishReason"]?.ToString() : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completion from {Provider}", selectedProvider);
            return new AICompletionResponse
            {
                Success = false,
                Error = ex.Message,
                Provider = selectedProvider
            };
        }
    }

    public async Task<AICompletionResponse> GetChatResponseAsync(
        IEnumerable<ChatMessage> messages,
        AIProvider? provider = null,
        string? systemPrompt = null,
        AIRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var selectedProvider = provider ?? _config.DefaultProvider;

        _logger.LogDebug("Getting chat response with provider {Provider} for {MessageCount} messages", selectedProvider, messages.Count());

        try
        {
            if (selectedProvider == AIProvider.Databricks)
            {
                return await _databricksService.GetChatResponseAsync(messages, systemPrompt, options, cancellationToken);
            }

            if (!_kernels.TryGetValue(selectedProvider, out var kernel))
            {
                return new AICompletionResponse
                {
                    Success = false,
                    Error = $"Provider {selectedProvider} is not available or configured",
                    Provider = selectedProvider
                };
            }

            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var settings = CreateExecutionSettings(options);

            var chatHistory = new ChatHistory();

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                chatHistory.AddSystemMessage(systemPrompt);
            }

            foreach (var message in messages)
            {
                if (message.Role.ToLower() == "user")
                {
                    chatHistory.AddUserMessage(message.Content);
                }
                else if (message.Role.ToLower() == "assistant")
                {
                    chatHistory.AddAssistantMessage(message.Content);
                }
            }

            var result = await chatService.GetChatMessageContentAsync(chatHistory, settings, kernel, cancellationToken);

            return new AICompletionResponse
            {
                Success = true,
                Content = result.Content ?? string.Empty,
                Provider = selectedProvider,
                Usage = ExtractUsage(result),
                FinishReason = result.Metadata?.ContainsKey("FinishReason") == true ?
                    result.Metadata["FinishReason"]?.ToString() : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat response from {Provider}", selectedProvider);
            return new AICompletionResponse
            {
                Success = false,
                Error = ex.Message,
                Provider = selectedProvider
            };
        }
    }

    public async IAsyncEnumerable<string> StreamCompletionAsync(
        string prompt,
        AIProvider? provider = null,
        AIRequestOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var selectedProvider = provider ?? _config.DefaultProvider;

        _logger.LogDebug("Starting streaming completion with provider {Provider}", selectedProvider);

        if (selectedProvider == AIProvider.Databricks)
        {
            await foreach (var token in _databricksService.StreamCompletionAsync(prompt, options, cancellationToken))
            {
                yield return token;
            }
            yield break;
        }

        if (!_kernels.TryGetValue(selectedProvider, out var kernel))
        {
            _logger.LogError("Provider {Provider} is not available or configured", selectedProvider);
            yield break;
        }

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var settings = CreateExecutionSettings(options);

        await foreach (var update in chatService.GetStreamingChatMessageContentsAsync(prompt, settings, kernel, cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Content))
            {
                yield return update.Content;
            }
        }
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> messages,
        AIProvider? provider = null,
        string? systemPrompt = null,
        AIRequestOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var selectedProvider = provider ?? _config.DefaultProvider;

        _logger.LogDebug("Starting streaming chat with provider {Provider} for {MessageCount} messages", selectedProvider, messages.Count());

        if (selectedProvider == AIProvider.Databricks)
        {
            await foreach (var token in _databricksService.StreamChatAsync(messages, systemPrompt, options, cancellationToken))
            {
                yield return token;
            }
            yield break;
        }

        if (!_kernels.TryGetValue(selectedProvider, out var kernel))
        {
            _logger.LogError("Provider {Provider} is not available or configured", selectedProvider);
            yield break;
        }

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var settings = CreateExecutionSettings(options);

        var chatHistory = new ChatHistory();

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            chatHistory.AddSystemMessage(systemPrompt);
        }

        foreach (var message in messages)
        {
            if (message.Role.ToLower() == "user")
            {
                chatHistory.AddUserMessage(message.Content);
            }
            else if (message.Role.ToLower() == "assistant")
            {
                chatHistory.AddAssistantMessage(message.Content);
            }
        }

        await foreach (var update in chatService.GetStreamingChatMessageContentsAsync(chatHistory, settings, kernel, cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Content))
            {
                yield return update.Content;
            }
        }
    }

    public async Task<IEnumerable<AIProvider>> GetAvailableProvidersAsync()
    {
        var availableProviders = new List<AIProvider>();

        foreach (var provider in _kernels.Keys)
        {
            availableProviders.Add(provider);
        }

        if (await _databricksService.IsHealthyAsync())
        {
            availableProviders.Add(AIProvider.Databricks);
        }

        _logger.LogDebug("Available providers: {Providers}", string.Join(", ", availableProviders));

        return availableProviders;
    }

    public async Task<bool> IsProviderAvailableAsync(AIProvider provider)
    {
        if (provider == AIProvider.Databricks)
        {
            return await _databricksService.IsHealthyAsync();
        }

        return _kernels.ContainsKey(provider);
    }

    public async Task<AICompletionResponse> ExecuteFunctionAsync(
        string prompt,
        IEnumerable<object> functions,
        AIProvider? provider = null,
        AIRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var selectedProvider = provider ?? _config.DefaultProvider;

        _logger.LogDebug("Executing function with provider {Provider}", selectedProvider);

        if (selectedProvider == AIProvider.Databricks)
        {
            _logger.LogWarning("Function calling is not yet implemented for Databricks provider");
            return new AICompletionResponse
            {
                Success = false,
                Error = "Function calling is not supported for Databricks provider",
                Provider = selectedProvider
            };
        }

        if (!_kernels.TryGetValue(selectedProvider, out var kernel))
        {
            return new AICompletionResponse
            {
                Success = false,
                Error = $"Provider {selectedProvider} is not available or configured",
                Provider = selectedProvider
            };
        }

        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var settings = CreateExecutionSettings(options);

            var result = await chatService.GetChatMessageContentAsync(prompt, settings, kernel, cancellationToken);

            return new AICompletionResponse
            {
                Success = true,
                Content = result.Content ?? string.Empty,
                Provider = selectedProvider,
                Usage = ExtractUsage(result)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function with {Provider}", selectedProvider);
            return new AICompletionResponse
            {
                Success = false,
                Error = ex.Message,
                Provider = selectedProvider
            };
        }
    }

    private PromptExecutionSettings CreateExecutionSettings(AIRequestOptions? options)
    {
        return new OpenAIPromptExecutionSettings
        {
            Temperature = options?.Temperature ?? _config.Temperature,
            MaxTokens = options?.MaxTokens ?? _config.MaxTokens,
            TopP = options?.TopP ?? _config.TopP
        };
    }

    private static AIUsageMetrics? ExtractUsage(ChatMessageContent result)
    {
        if (result.Metadata?.TryGetValue("Usage", out var usageObj) == true)
        {
            try
            {
                var usageDict = usageObj as Dictionary<string, object>;
                if (usageDict != null)
                {
                    return new AIUsageMetrics
                    {
                        InputTokens = TryGetIntValue(usageDict, "InputTokens") ?? 0,
                        OutputTokens = TryGetIntValue(usageDict, "OutputTokens") ?? 0,
                        TotalTokens = TryGetIntValue(usageDict, "TotalTokens") ?? 0
                    };
                }

                // Try reflection as fallback for different Usage types
                var usageType = usageObj.GetType();
                var inputTokensProperty = usageType.GetProperty("InputTokens");
                var outputTokensProperty = usageType.GetProperty("OutputTokens");
                var totalTokensProperty = usageType.GetProperty("TotalTokens");

                if (inputTokensProperty != null && outputTokensProperty != null)
                {
                    return new AIUsageMetrics
                    {
                        InputTokens = (int?)(inputTokensProperty.GetValue(usageObj)) ?? 0,
                        OutputTokens = (int?)(outputTokensProperty.GetValue(usageObj)) ?? 0,
                        TotalTokens = (int?)(totalTokensProperty?.GetValue(usageObj)) ?? 0
                    };
                }
            }
            catch
            {
                // If we can't extract usage, return null
            }
        }

        return null;
    }

    private static int? TryGetIntValue(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out var value))
        {
            return value switch
            {
                int intValue => intValue,
                long longValue => (int)longValue,
                string strValue when int.TryParse(strValue, out var parsed) => parsed,
                _ => null
            };
        }
        return null;
    }
}