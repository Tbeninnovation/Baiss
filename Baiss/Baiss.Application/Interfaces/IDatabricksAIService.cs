using Baiss.Application.Models.AI;

namespace Baiss.Application.Interfaces;

public interface IDatabricksAIService
{
    Task<AICompletionResponse> GetCompletionAsync(
        string prompt,
        AIRequestOptions? options = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamCompletionAsync(
        string prompt,
        AIRequestOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<AICompletionResponse> GetChatResponseAsync(
        IEnumerable<ChatMessage> messages,
        string? systemPrompt = null,
        AIRequestOptions? options = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> messages,
        string? systemPrompt = null,
        AIRequestOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<bool> IsHealthyAsync();

    Task<IEnumerable<string>> GetAvailableModelsAsync();

    Task<Dictionary<string, object>> GetModelInfoAsync(string modelName);
}