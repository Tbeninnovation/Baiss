using Baiss.Application.DTOs;

namespace Baiss.Application.Interfaces;

/// <summary>
/// Service interface for AI assistant operations
/// </summary>
public interface IAssistantService
{
    /// <summary>
    /// Generates a response from the AI assistant based on user input
    /// </summary>
    /// <param name="userMessage">The user's message</param>
    /// <param name="conversationContext">Optional conversation context</param>
    /// <returns>Assistant response or null if no response could be generated</returns>
    // Task<ContentResponse?> GenerateResponseAsync(string userMessage, List<MessageItem>? conversationContext);


    /// <summary>
    /// Generates a streaming response from the AI assistant using WebSocket connection
    /// </summary>
    /// <param name="userMessage">The user's message</param>
    /// <param name="conversationContext">Optional conversation context</param>
    /// <param name="filePaths">Optional list of file paths to include in the request</param>
    /// <returns>Async enumerable of text chunks as they arrive</returns>
    IAsyncEnumerable<string> GenerateStreamingResponseAsync(string userMessage, List<MessageItem>? conversationContext, List<string>? filePaths = null);

    /// <summary>
    /// Checks if the assistant service is ready for operations
    /// </summary>
    /// <returns>True if ready, false otherwise</returns>
    bool IsReady { get; }

    /// <summary>
    /// Gets the paths with scores from the last streaming response
    /// </summary>
    /// <returns>List of paths with their relevance scores</returns>
    List<PathScoreDto> GetLastReceivedPaths();
}
