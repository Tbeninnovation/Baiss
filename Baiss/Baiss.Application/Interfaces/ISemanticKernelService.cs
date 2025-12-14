using Baiss.Application.Models.AI;

namespace Baiss.Application.Interfaces;

public interface ISemanticKernelService
{
    Task<AICompletionResponse> GetCompletionAsync(
        string prompt,
        AIProvider? provider = null,
        AIRequestOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<AICompletionResponse> GetChatResponseAsync(
        IEnumerable<ChatMessage> messages,
        AIProvider? provider = null,
        string? systemPrompt = null,
        AIRequestOptions? options = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamCompletionAsync(
        string prompt,
        AIProvider? provider = null,
        AIRequestOptions? options = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> messages,
        AIProvider? provider = null,
        string? systemPrompt = null,
        AIRequestOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<AIProvider>> GetAvailableProvidersAsync();

    Task<bool> IsProviderAvailableAsync(AIProvider provider);

    Task<AICompletionResponse> ExecuteFunctionAsync(
        string prompt,
        IEnumerable<object> functions,
        AIProvider? provider = null,
        AIRequestOptions? options = null,
        CancellationToken cancellationToken = default);
}