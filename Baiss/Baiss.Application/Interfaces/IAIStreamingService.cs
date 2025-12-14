using Baiss.Application.Models.AI;
using System.Runtime.CompilerServices;

namespace Baiss.Application.Interfaces;

public interface IAIStreamingService
{
    IAsyncEnumerable<string> StreamCompletionAsync(
        string prompt,
        AIProvider? provider = null,
        AIRequestOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> messages,
        AIProvider? provider = null,
        string? systemPrompt = null,
        AIRequestOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);

    IAsyncEnumerable<AIStreamResponse> StreamWithMetadataAsync(
        string prompt,
        AIProvider? provider = null,
        AIRequestOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);

    Task<string> GetStreamingStatusAsync(string streamId);

    Task CancelStreamAsync(string streamId);
}