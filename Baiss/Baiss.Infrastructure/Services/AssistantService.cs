using Baiss.Application.Interfaces;
using Baiss.Application.DTOs;
using Baiss.Application.Models.AI.Universal;
using Baiss.Domain.Entities;
// using Baiss.Infrastructure.Interop;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Baiss.Infrastructure.Services;

/// <summary>
/// Implementation of assistant service - AI integration via HTTP API (with Python bridge fallback)
/// </summary>
public class AssistantService : IAssistantService
{
    private readonly IExternalApiService _externalApiService;
    // private readonly IUniversalAIService _universalAIService;
    private readonly ILogger<AssistantService> _logger;

    public AssistantService(
        IExternalApiService externalApiService,
        ILogger<AssistantService> logger)
    {
        _externalApiService = externalApiService;
        // _universalAIService = universalAIService;
        _logger = logger;
    }

    public bool IsReady => true; // For now, always ready


    /// <summary>
    /// Generate streaming response with smart routing between local and hosted models
    /// </summary>
    public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(string userMessage, List<MessageItem>? conversationContext, List<string>? filePaths = null)
    {
        _logger.LogInformation("Starting GenerateStreamingResponseAsync for message: {Message}", userMessage);

        await foreach (var chunk in UseLocalStreamingService(userMessage, conversationContext, filePaths))
        {
            yield return chunk;
        }
        yield break;
    }


    /// <summary>
    /// Generate streaming response using local Python service via WebSocket
    /// </summary>
    private async IAsyncEnumerable<string> UseLocalStreamingService(string userMessage, List<MessageItem>? conversationContext, List<string>? filePaths = null)
    {
        _logger.LogDebug("Using local Python streaming service");

        IAsyncEnumerable<string>? streamEnumerable = null;
        string? errorMessage = null;

        try
        {
            streamEnumerable = _externalApiService.SendChatMessageStreamAsync(userMessage, conversationContext, filePaths);
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("Unable to connect to local chat streaming service at localhost:8000. Service may not be running.");
            errorMessage = "Error: Chat service is currently unavailable";
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error connecting to local streaming API: {Message}", ex.Message);
            errorMessage = $"Error: {ex.Message}";
        }

        if (errorMessage != null)
        {
            yield return errorMessage;
            yield break;
        }

        if (streamEnumerable != null)
        {
            await foreach (var chunk in streamEnumerable)
            {
                // _logger.LogDebug("Received local streaming chunk: {Chunk}", chunk);
                yield return chunk;
            }
        }
    }

    /// <summary>
    /// Creates a default ContentResponse for error cases
    /// </summary>
    /// <param name="status">HTTP status code</param>
    /// <param name="error">Error message</param>
    /// <param name="stopReason">Stop reason for the response</param>
    /// <returns>A ContentResponse with error information</returns>
    private static ContentResponse CreateErrorContentResponse(int status, string error, string stopReason)
    {
        return new ContentResponse
        {
            Status = status,
            Success = false,
            Error = error,
            Response = new ResponseData(),
            Usage = new UsageData(),
            Sources = new List<SourceItem>(), // Empty sources list for error cases
            // Dashboard = new List<object>(),
            StopReason = stopReason
        };
    }

    /// <summary>
    /// Gets the paths with scores from the last streaming response
    /// </summary>
    /// <returns>List of paths with their relevance scores</returns>
    public List<PathScoreDto> GetLastReceivedPaths()
    {
        return _externalApiService.LastReceivedPaths;
    }
}
