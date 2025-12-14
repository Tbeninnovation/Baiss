using System.Text.Json.Serialization;

namespace Baiss.Application.Models.AI.Universal;

public record UniversalAIResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("response")]
    public UniversalResponseData? Response { get; init; }

    [JsonPropertyName("usage")]
    public UniversalUsageData? Usage { get; init; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }
}

public record UniversalResponseData
{
    [JsonPropertyName("choices")]
    public UniversalChoice[] Choices { get; init; } = Array.Empty<UniversalChoice>();

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

public record UniversalChoice
{
    [JsonPropertyName("messages")]
    public UniversalMessage[] Messages { get; init; } = Array.Empty<UniversalMessage>();

    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }

    [JsonPropertyName("delta")]
    public UniversalMessage? Delta { get; init; }
}

public record UniversalUsageData
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }

    [JsonPropertyName("processing_time_ms")]
    public long ProcessingTimeMs { get; init; }

    [JsonPropertyName("cost")]
    public decimal? Cost { get; init; }

    [JsonPropertyName("cost_currency")]
    public string? CostCurrency { get; init; }
}

public record UniversalStreamResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("delta")]
    public UniversalMessage? Delta { get; init; }

    [JsonPropertyName("choices")]
    public UniversalChoice[]? Choices { get; init; }

    [JsonPropertyName("usage")]
    public UniversalUsageData? Usage { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; init; }
}