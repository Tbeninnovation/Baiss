namespace Baiss.Application.Models.AI;

public record AIStreamRequest
{
    public string Prompt { get; init; } = string.Empty;
    public List<ChatMessage> Messages { get; init; } = new();
    public string? SystemPrompt { get; init; }
    public AIProvider? Provider { get; init; }
    public AIRequestOptions Options { get; init; } = new();
}

public record AIRequestOptions
{
    public double? Temperature { get; init; }
    public int? MaxTokens { get; init; }
    public double? TopP { get; init; }
    public bool EnableStreaming { get; init; } = true;
    public bool EnableFunctionCalling { get; init; } = false;
    public string? ConversationId { get; init; }
    public Dictionary<string, object>? AdditionalParameters { get; init; }
}

public record ChatMessage
{
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}