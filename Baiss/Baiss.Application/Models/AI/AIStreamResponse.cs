namespace Baiss.Application.Models.AI;

public record AIStreamResponse
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string Content { get; init; } = string.Empty;
    public AIProvider Provider { get; init; }
    public AIUsageMetrics? Usage { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

public record AICompletionResponse
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string Content { get; init; } = string.Empty;
    public AIProvider Provider { get; init; }
    public AIUsageMetrics? Usage { get; init; }
    public string? ConversationId { get; init; }
    public string? FinishReason { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object>? Metadata { get; init; }
}

public record AIUsageMetrics
{
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int TotalTokens { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public decimal? Cost { get; init; }
}