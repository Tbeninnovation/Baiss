using System.Text.Json;
using Baiss.Application.DTOs;
using Baiss.Application.UseCases;
using Baiss.Domain.Entities;

namespace Baiss.Tests.Examples;

/// <summary>
/// Example demonstrating how to work with sources in messages
/// </summary>
public class SourcesExample
{
    /// <summary>
    /// Demonstrates how sources are serialized and stored with assistant messages
    /// </summary>
    public void ExampleSourcesSerialization()
    {
        // Example JSON response that contains sources (like your example)
        var jsonResponse = """
        {
            "status": 200,
            "success": true,
            "error": null,
            "response": {
                "messages": [{
                    "role": "assistant",
                    "content": [{"text": "Hello! How can I assist you today? 😊"}]
                }]
            },
            "usage": {"inputTokens": 33, "outputTokens": 12, "totalTokens": 45},
            "sources": [{
                "file_name": "",
                "file_chunk": {
                    "full_text": "# Quartz.NET Job Scheduling Integration\n\nThis document describes the Quartz.NET integration in the Baiss desktop application.",
                    "token_count": 22
                }
            }],
            "stop_reason": "end_turn"
        }
        """;

        // Parse the response
        var contentResponse = JsonSerializer.Deserialize<ContentResponse>(jsonResponse);

        // Extract sources if available
        string? sourcesJson = null;
        if (contentResponse?.Success == true && contentResponse.Sources?.Count > 0)
        {
            sourcesJson = JsonSerializer.Serialize(contentResponse.Sources);
        }

        // Create assistant message with sources
        var conversationId = Guid.NewGuid();
        var assistantResponse = contentResponse?.Response?.Messages?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text ?? "Default response";

        var assistantMessage = Message.CreateAssistantMessage(conversationId, assistantResponse, sourcesJson);

        // The message now contains:
        // - assistantMessage.Content: "Hello! How can I assist you today? 😊"
        // - assistantMessage.Sources: JSON string containing the source information
        // - assistantMessage.SenderType: SenderType.ASSISTANT

        // When you need to retrieve and use the sources later:
        if (!string.IsNullOrEmpty(assistantMessage.Sources))
        {
            var sources = JsonSerializer.Deserialize<List<SourceItem>>(assistantMessage.Sources);
            foreach (var source in sources ?? new List<SourceItem>())
            {
                Console.WriteLine($"Source file: {source.FileName}");
                Console.WriteLine($"Token count: {source.FileChunk.TokenCount}");
                Console.WriteLine($"Text snippet: {source.FileChunk.FullText}");
            }
        }
    }
}
