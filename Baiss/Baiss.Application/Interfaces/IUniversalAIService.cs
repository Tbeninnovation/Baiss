using Baiss.Application.Models.AI.Universal;
using System.Runtime.CompilerServices;

namespace Baiss.Application.Interfaces;

public interface IUniversalAIService
{
    /// <summary>
    /// Process a universal AI request and return a complete response
    /// </summary>
    Task<UniversalAIResponse> ProcessAsync(
        UniversalAIRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Process a universal AI request with streaming response
    /// </summary>
    IAsyncEnumerable<UniversalStreamResponse> ProcessStreamAsync(
        UniversalAIRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);

    /// <summary>
    /// Process a universal AI request with a specific provider
    /// </summary>
    Task<UniversalAIResponse> ProcessWithProviderAsync(
        UniversalAIRequest request,
        string provider,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Process a universal AI request with streaming and specific provider
    /// </summary>
    IAsyncEnumerable<UniversalStreamResponse> ProcessStreamWithProviderAsync(
        UniversalAIRequest request,
        string provider,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);

    /// <summary>
    /// Get available providers that support universal format
    /// </summary>
    Task<IEnumerable<string>> GetAvailableProvidersAsync();

    /// <summary>
    /// Check if a provider supports universal format
    /// </summary>
    Task<bool> IsProviderSupportedAsync(string provider);

    /// <summary>
    /// Get supported content types for a provider
    /// </summary>
    Task<IEnumerable<string>> GetSupportedContentTypesAsync(string provider);

    /// <summary>
    /// Validate a universal request before processing
    /// </summary>
    Task<(bool IsValid, string? Error)> ValidateRequestAsync(UniversalAIRequest request);

    /// <summary>
    /// Get provider capabilities and limits
    /// </summary>
    Task<Dictionary<string, object>> GetProviderCapabilitiesAsync(string provider);
}