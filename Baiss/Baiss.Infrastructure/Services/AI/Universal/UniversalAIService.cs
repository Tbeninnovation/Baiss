using Baiss.Application.Interfaces;
using Baiss.Application.Models.AI;
using Baiss.Application.Models.AI.Universal;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Baiss.Infrastructure.Services.AI.Universal;

public class UniversalAIService : IUniversalAIService
{
    private readonly ISemanticKernelService _semanticKernelService;
    private readonly IDatabricksAIService _databricksService;
    private readonly ILogger<UniversalAIService> _logger;

    private readonly Dictionary<string, AIProvider> _providerMapping = new()
    {
        ["openai"] = AIProvider.OpenAI,
        ["anthropic"] = AIProvider.Anthropic,
        ["azure"] = AIProvider.AzureOpenAI,
        ["azureopenai"] = AIProvider.AzureOpenAI,
        ["databricks"] = AIProvider.Databricks
        // Note: no implicit mapping for "local" here; local is handled outside universal service
    };

    public UniversalAIService(
        ISemanticKernelService semanticKernelService,
        IDatabricksAIService databricksService,
        ILogger<UniversalAIService> logger)
    {
        _semanticKernelService = semanticKernelService;
        _databricksService = databricksService;
        _logger = logger;
    }

    public async Task<UniversalAIResponse> ProcessAsync(
        UniversalAIRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validation = await ValidateRequestAsync(request);
            if (!validation.IsValid)
            {
                return CreateErrorResponse(validation.Error ?? "Invalid request");
            }

            var provider = DetermineProvider(request);
            if (provider == null)
            {
                return CreateErrorResponse("Unable to determine AI provider");
            }

            _logger.LogDebug("Processing universal request with provider {Provider}", provider);

            // Convert universal format to legacy format
            var messages = UniversalFormatConverter.ToLegacyChatMessages(request.Messages);
            var options = UniversalFormatConverter.ToLegacyOptions(request.Config);
            var systemPrompt = UniversalFormatConverter.ExtractSystemPrompt(request);

            // Process with the determined provider
            var legacyResponse = await _semanticKernelService.GetChatResponseAsync(
                messages, provider, systemPrompt, options, cancellationToken);

            // Convert back to universal format
            var universalResponse = UniversalFormatConverter.FromLegacyResponse(legacyResponse);

            // Add additional metadata
            universalResponse = universalResponse with
            {
                Model = request.Model.Name ?? request.Model.Path,
                Metadata = MergeMetadata(universalResponse.Metadata, new Dictionary<string, object>
                {
                    ["request_id"] = request.Config.Id ?? Guid.NewGuid().ToString(),
                    ["parent_id"] = request.Config.ParentId ?? string.Empty,
                    ["provider_used"] = provider.ToString(),
                    ["model_type"] = request.Model.Type,
                    ["content_types"] = request.Messages.SelectMany(m => m.Content).Select(c => c.Type).Distinct().ToArray()
                })
            };

            return universalResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing universal AI request");
            return CreateErrorResponse($"Processing failed: {ex.Message}");
        }
    }

    public IAsyncEnumerable<UniversalStreamResponse> ProcessStreamAsync(
        UniversalAIRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        return ProcessStreamInternalAsync(request, cancellationToken);
    }

    private async IAsyncEnumerable<UniversalStreamResponse> ProcessStreamInternalAsync(
        UniversalAIRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var validation = await ValidateRequestAsync(request);
        if (!validation.IsValid)
        {
            yield return CreateStreamErrorResponse(validation.Error ?? "Invalid request");
            yield break;
        }

        var provider = DetermineProvider(request);
        if (provider == null)
        {
            yield return CreateStreamErrorResponse("Unable to determine AI provider");
            yield break;
        }

        _logger.LogDebug("Processing universal stream request with provider {Provider}", provider);

        // Convert universal format to legacy format
        var messages = UniversalFormatConverter.ToLegacyChatMessages(request.Messages);
        var options = UniversalFormatConverter.ToLegacyOptions(request.Config);
        var systemPrompt = UniversalFormatConverter.ExtractSystemPrompt(request);

        var metadata = new Dictionary<string, object>
        {
            ["request_id"] = request.Config.Id ?? Guid.NewGuid().ToString(),
            ["parent_id"] = request.Config.ParentId ?? string.Empty,
            ["provider_used"] = provider.ToString(),
            ["model_type"] = request.Model.Type,
            ["stream"] = true
        };

        // Setup stream outside of any try-catch with yield
        IAsyncEnumerable<string>? stream = null;
        Exception? setupError = null;

        try
        {
            stream = _semanticKernelService.StreamChatAsync(
                messages, provider, systemPrompt, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up universal AI stream");
            setupError = ex;
        }

        if (setupError != null)
        {
            yield return CreateStreamErrorResponse($"Stream setup failed: {setupError.Message}");
            yield break;
        }

        if (stream == null)
        {
            yield return CreateStreamErrorResponse("Failed to initialize stream");
            yield break;
        }

        await using var enumerator = stream.GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            bool hasNext;
            string? token = null;
            Exception? streamError = null;

            try
            {
                hasNext = await enumerator.MoveNextAsync();
                if (hasNext)
                {
                    token = enumerator.Current;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in universal AI stream processing");
                streamError = ex;
                hasNext = false;
            }

            if (streamError != null)
            {
                yield return CreateStreamErrorResponse($"Streaming failed: {streamError.Message}");
                yield break;
            }

            if (!hasNext)
                break;

            yield return UniversalFormatConverter.CreateStreamResponse(token!, provider.ToString(), metadata);
        }
    }

    public Task<UniversalAIResponse> ProcessWithProviderAsync(
        UniversalAIRequest request,
        string provider,
        CancellationToken cancellationToken = default)
    {
        // Override the provider in the request
        var modifiedRequest = request with { Provider = provider };
        return ProcessAsync(modifiedRequest, cancellationToken);
    }

    public IAsyncEnumerable<UniversalStreamResponse> ProcessStreamWithProviderAsync(
        UniversalAIRequest request,
        string provider,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Override the provider in the request
        var modifiedRequest = request with { Provider = provider };
        return ProcessStreamAsync(modifiedRequest, cancellationToken);
    }

    public async Task<IEnumerable<string>> GetAvailableProvidersAsync()
    {
        var availableProviders = new List<string>();

        var legacyProviders = await _semanticKernelService.GetAvailableProvidersAsync();
        foreach (var provider in legacyProviders)
        {
            var providerName = provider.ToString().ToLower();
            availableProviders.Add(providerName);
        }

        // Add any additional provider mappings
        availableProviders.AddRange(_providerMapping.Keys.Where(k => !availableProviders.Contains(k)));

        return availableProviders.Distinct();
    }

    public async Task<bool> IsProviderSupportedAsync(string provider)
    {
        if (_providerMapping.TryGetValue(provider.ToLower(), out var aiProvider))
        {
            return await _semanticKernelService.IsProviderAvailableAsync(aiProvider);
        }

        return false;
    }

    public Task<IEnumerable<string>> GetSupportedContentTypesAsync(string provider)
    {
        // Base content types supported by all providers
        var baseTypes = new[] { UniversalContentTypes.Text };

        // Provider-specific content type support
        var supportedTypes = provider.ToLower() switch
        {
            "openai" => baseTypes.Concat(new[] { UniversalContentTypes.Image, UniversalContentTypes.Url }).ToArray(),
            "anthropic" => baseTypes.Concat(new[] { UniversalContentTypes.Image, UniversalContentTypes.Document }).ToArray(),
            "databricks" => baseTypes.Concat(new[] { UniversalContentTypes.Document }).ToArray(),
            "azure" or "azureopenai" => baseTypes.Concat(new[] { UniversalContentTypes.Image, UniversalContentTypes.Url }).ToArray(),
            _ => baseTypes
        };

        return Task.FromResult<IEnumerable<string>>(supportedTypes);
    }

    public Task<(bool IsValid, string? Error)> ValidateRequestAsync(UniversalAIRequest request)
    {
        if (request == null)
            return Task.FromResult<(bool, string?)>((false, "Request cannot be null"));

        if (request.Messages == null || request.Messages.Length == 0)
            return Task.FromResult<(bool, string?)>((false, "Messages cannot be empty"));

        if (request.Config == null)
            return Task.FromResult<(bool, string?)>((false, "Config cannot be null"));

        // Validate temperature range
        if (request.Config.Temperature < 0 || request.Config.Temperature > 2)
            return Task.FromResult<(bool, string?)>((false, "Temperature must be between 0 and 2"));

        // Validate max_tokens
        if (request.Config.MaxTokens < 1 || request.Config.MaxTokens > 32000)
            return Task.FromResult<(bool, string?)>((false, "MaxTokens must be between 1 and 32000"));

        // Validate top_p
        if (request.Config.TopP < 0 || request.Config.TopP > 1)
            return Task.FromResult<(bool, string?)>((false, "TopP must be between 0 and 1"));

        // Validate messages content
        foreach (var message in request.Messages)
        {
            if (string.IsNullOrEmpty(message.Role))
                return Task.FromResult<(bool, string?)>((false, "Message role cannot be empty"));

            if (message.Content == null || message.Content.Length == 0)
                return Task.FromResult<(bool, string?)>((false, "Message content cannot be empty"));

            foreach (var content in message.Content)
            {
                if (string.IsNullOrEmpty(content.Type))
                    return Task.FromResult<(bool, string?)>((false, "Content type cannot be empty"));

                if (content.Type == UniversalContentTypes.Text && string.IsNullOrEmpty(content.Text))
                    return Task.FromResult<(bool, string?)>((false, "Text content cannot be empty"));

                if (content.Type == UniversalContentTypes.Url && string.IsNullOrEmpty(content.Url))
                    return Task.FromResult<(bool, string?)>((false, "URL content must have a valid URL"));
            }
        }

        return Task.FromResult<(bool, string?)>((true, null));
    }

    public async Task<Dictionary<string, object>> GetProviderCapabilitiesAsync(string provider)
    {
        var capabilities = new Dictionary<string, object>
        {
            ["provider"] = provider,
            ["supported"] = await IsProviderSupportedAsync(provider),
            ["content_types"] = await GetSupportedContentTypesAsync(provider),
            ["streaming"] = true,
            ["function_calling"] = false,
            ["max_tokens"] = 32000
        };

        // Provider-specific capabilities
        switch (provider.ToLower())
        {
            case "openai":
                capabilities["function_calling"] = true;
                capabilities["max_tokens"] = 128000;
                capabilities["supports_vision"] = true;
                break;

            case "anthropic":
                capabilities["max_tokens"] = 200000;
                capabilities["supports_vision"] = true;
                break;

            case "databricks":
                capabilities["max_tokens"] = 32000;
                capabilities["supports_local_models"] = true;
                break;

            case "azure":
            case "azureopenai":
                capabilities["function_calling"] = true;
                capabilities["max_tokens"] = 128000;
                capabilities["supports_vision"] = true;
                break;
        }

        return capabilities;
    }

    private AIProvider? DetermineProvider(UniversalAIRequest request)
    {
        // Try explicit provider first
        if (!string.IsNullOrEmpty(request.Provider))
        {
            if (_providerMapping.TryGetValue(request.Provider.ToLower(), out var explicitProvider))
                return explicitProvider;
        }

        // Try to determine from model configuration
        if (request.Model != null)
        {
            var modelType = request.Model.Type?.ToLower();
            return modelType switch
            {
                // "local" is not supported by universal service; return null to force explicit provider
                "openai" => AIProvider.OpenAI,
                "anthropic" => AIProvider.Anthropic,
                "azure" => AIProvider.AzureOpenAI,
                _ => null
            };
        }

        return null;
    }

    private static UniversalAIResponse CreateErrorResponse(string error)
    {
        return new UniversalAIResponse
        {
            Success = false,
            Error = error,
            Response = null,
            Timestamp = DateTime.UtcNow
        };
    }

    private static UniversalStreamResponse CreateStreamErrorResponse(string error)
    {
        return new UniversalStreamResponse
        {
            Success = false,
            Error = error
        };
    }

    private static Dictionary<string, object>? MergeMetadata(Dictionary<string, object>? existing, Dictionary<string, object> additional)
    {
        if (existing == null)
            return additional;

        var merged = new Dictionary<string, object>(existing);
        foreach (var kvp in additional)
        {
            merged[kvp.Key] = kvp.Value;
        }

        return merged;
    }
}
