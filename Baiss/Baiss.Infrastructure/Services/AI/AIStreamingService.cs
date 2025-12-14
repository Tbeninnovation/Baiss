using Baiss.Application.Interfaces;
using Baiss.Application.Models.AI;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Baiss.Infrastructure.Services.AI;

public class AIStreamingService : IAIStreamingService
{
    private readonly ISemanticKernelService _semanticKernelService;
    private readonly ILogger<AIStreamingService> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeStreams;

    public AIStreamingService(
        ISemanticKernelService semanticKernelService,
        ILogger<AIStreamingService> logger)
    {
        _semanticKernelService = semanticKernelService;
        _logger = logger;
        _activeStreams = new ConcurrentDictionary<string, CancellationTokenSource>();
    }

    public async IAsyncEnumerable<string> StreamCompletionAsync(
        string prompt,
        AIProvider? provider = null,
        AIRequestOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var token in StreamCompletionInternalAsync(prompt, provider, options, cancellationToken))
        {
            yield return token;
        }
    }

    private async IAsyncEnumerable<string> StreamCompletionInternalAsync(
        string prompt,
        AIProvider? provider,
        AIRequestOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var streamId = Guid.NewGuid().ToString();
        var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _activeStreams[streamId] = combinedCts;

        _logger.LogDebug("Starting streaming completion {StreamId} with provider {Provider}", streamId, provider);

        IAsyncEnumerable<string>? stream = null;

        try
        {
            stream = _semanticKernelService.StreamCompletionAsync(prompt, provider, options, combinedCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Stream {StreamId} was cancelled during setup", streamId);
            CleanupStream(streamId, combinedCts);
            yield break;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up streaming completion {StreamId}", streamId);
            CleanupStream(streamId, combinedCts);
            yield break;
        }

        var tokenCount = 0;
        var startTime = DateTime.UtcNow;

        await using var enumerator = stream.GetAsyncEnumerator(combinedCts.Token);

        while (true)
        {
            bool hasNext;
            string? current = null;

            try
            {
                hasNext = await enumerator.MoveNextAsync();
                if (hasNext)
                {
                    current = enumerator.Current;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Stream {StreamId} was cancelled", streamId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in streaming completion {StreamId}", streamId);
                break;
            }

            if (!hasNext || combinedCts.Token.IsCancellationRequested)
            {
                break;
            }

            if (current != null)
            {
                tokenCount++;
                yield return current;
            }
        }

        CleanupStream(streamId, combinedCts);

        var duration = DateTime.UtcNow - startTime;
        _logger.LogDebug("Completed streaming {StreamId}. Tokens: {TokenCount}, Duration: {Duration}ms",
            streamId, tokenCount, duration.TotalMilliseconds);
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> messages,
        AIProvider? provider = null,
        string? systemPrompt = null,
        AIRequestOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var token in StreamChatInternalAsync(messages, provider, systemPrompt, options, cancellationToken))
        {
            yield return token;
        }
    }

    private async IAsyncEnumerable<string> StreamChatInternalAsync(
        IEnumerable<ChatMessage> messages,
        AIProvider? provider,
        string? systemPrompt,
        AIRequestOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var streamId = Guid.NewGuid().ToString();
        var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _activeStreams[streamId] = combinedCts;

        _logger.LogDebug("Starting streaming chat {StreamId} with provider {Provider} for {MessageCount} messages",
            streamId, provider, messages.Count());

        IAsyncEnumerable<string>? stream = null;

        try
        {
            stream = _semanticKernelService.StreamChatAsync(messages, provider, systemPrompt, options, combinedCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Chat stream {StreamId} was cancelled during setup", streamId);
            CleanupStream(streamId, combinedCts);
            yield break;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up streaming chat {StreamId}", streamId);
            CleanupStream(streamId, combinedCts);
            yield break;
        }

        var tokenCount = 0;
        var startTime = DateTime.UtcNow;

        await using var enumerator = stream.GetAsyncEnumerator(combinedCts.Token);

        while (true)
        {
            bool hasNext;
            string? current = null;

            try
            {
                hasNext = await enumerator.MoveNextAsync();
                if (hasNext)
                {
                    current = enumerator.Current;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Chat stream {StreamId} was cancelled", streamId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in streaming chat {StreamId}", streamId);
                break;
            }

            if (!hasNext || combinedCts.Token.IsCancellationRequested)
            {
                break;
            }

            if (current != null)
            {
                tokenCount++;
                yield return current;
            }
        }

        CleanupStream(streamId, combinedCts);

        var duration = DateTime.UtcNow - startTime;
        _logger.LogDebug("Completed chat streaming {StreamId}. Tokens: {TokenCount}, Duration: {Duration}ms",
            streamId, tokenCount, duration.TotalMilliseconds);
    }

    public async IAsyncEnumerable<AIStreamResponse> StreamWithMetadataAsync(
        string prompt,
        AIProvider? provider = null,
        AIRequestOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var response in StreamWithMetadataInternalAsync(prompt, provider, options, cancellationToken))
        {
            yield return response;
        }
    }

    private async IAsyncEnumerable<AIStreamResponse> StreamWithMetadataInternalAsync(
        string prompt,
        AIProvider? provider,
        AIRequestOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var streamId = Guid.NewGuid().ToString();
        var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _activeStreams[streamId] = combinedCts;

        _logger.LogDebug("Starting streaming with metadata {StreamId} with provider {Provider}", streamId, provider);

        var selectedProvider = provider ?? AIProvider.OpenAI;
        IAsyncEnumerable<string>? stream = null;

        try
        {
            stream = _semanticKernelService.StreamCompletionAsync(prompt, provider, options, combinedCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Metadata stream {StreamId} was cancelled during setup", streamId);
            CleanupStream(streamId, combinedCts);
            yield break;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up metadata streaming {StreamId}", streamId);
            CleanupStream(streamId, combinedCts);
            yield break;
        }

        var tokenCount = 0;
        var startTime = DateTime.UtcNow;

        await using var enumerator = stream.GetAsyncEnumerator(combinedCts.Token);

        while (true)
        {
            bool hasNext;
            string? current = null;

            try
            {
                hasNext = await enumerator.MoveNextAsync();
                if (hasNext)
                {
                    current = enumerator.Current;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Metadata stream {StreamId} was cancelled", streamId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in metadata streaming {StreamId}", streamId);
                break;
            }

            if (!hasNext || combinedCts.Token.IsCancellationRequested)
            {
                break;
            }

            if (current != null)
            {
                tokenCount++;

                yield return new AIStreamResponse
                {
                    Success = true,
                    Content = current,
                    Provider = selectedProvider,
                    Metadata = new Dictionary<string, object>
                    {
                        ["stream_id"] = streamId,
                        ["token_count"] = tokenCount,
                        ["elapsed_ms"] = (DateTime.UtcNow - startTime).TotalMilliseconds
                    }
                };
            }
        }

        CleanupStream(streamId, combinedCts);

        var duration = DateTime.UtcNow - startTime;
        _logger.LogDebug("Completed metadata streaming {StreamId}. Tokens: {TokenCount}, Duration: {Duration}ms",
            streamId, tokenCount, duration.TotalMilliseconds);
    }

    public Task<string> GetStreamingStatusAsync(string streamId)
    {
        if (_activeStreams.ContainsKey(streamId))
        {
            return Task.FromResult("active");
        }

        return Task.FromResult("completed_or_not_found");
    }

    public Task CancelStreamAsync(string streamId)
    {
        if (_activeStreams.TryRemove(streamId, out var cts))
        {
            _logger.LogDebug("Cancelling stream {StreamId}", streamId);
            cts.Cancel();
            cts.Dispose();
        }

        return Task.CompletedTask;
    }

    private void CleanupStream(string streamId, CancellationTokenSource cancellationTokenSource)
    {
        _activeStreams.TryRemove(streamId, out _);
        cancellationTokenSource.Dispose();
    }
}