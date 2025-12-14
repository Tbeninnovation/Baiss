using Baiss.Application.Models.AI.Universal;

namespace Baiss.Infrastructure.Services.AI.Universal.Adapters;

public interface IProviderAdapter
{
    /// <summary>
    /// Convert universal request to provider-specific format
    /// </summary>
    object ConvertRequest(UniversalAIRequest request);

    /// <summary>
    /// Convert provider-specific response to universal format
    /// </summary>
    UniversalAIResponse ConvertResponse(object providerResponse);

    /// <summary>
    /// Convert provider-specific streaming response to universal format
    /// </summary>
    UniversalStreamResponse ConvertStreamResponse(object providerStreamItem);

    /// <summary>
    /// Get provider-specific configuration
    /// </summary>
    Dictionary<string, object> GetProviderConfig(UniversalAIRequest request);

    /// <summary>
    /// Validate if the request is compatible with this provider
    /// </summary>
    (bool IsValid, string? Error) ValidateRequest(UniversalAIRequest request);

    /// <summary>
    /// Get supported content types for this provider
    /// </summary>
    IEnumerable<string> GetSupportedContentTypes();

    /// <summary>
    /// Get provider capabilities
    /// </summary>
    Dictionary<string, object> GetCapabilities();
}