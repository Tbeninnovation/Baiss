using System.Text.Json.Serialization;

namespace Baiss.Application.Models.AI.Universal;

public record UniversalConfig
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; init; } = 0.7;

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; init; } = 1024;

    [JsonPropertyName("top_p")]
    public double TopP { get; init; } = 0.9;

    [JsonPropertyName("stream")]
    public bool Stream { get; init; } = true;

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("parent_id")]
    public string? ParentId { get; init; }

    [JsonPropertyName("frequency_penalty")]
    public double? FrequencyPenalty { get; init; }

    [JsonPropertyName("presence_penalty")]
    public double? PresencePenalty { get; init; }

    [JsonPropertyName("stop")]
    public string[]? Stop { get; init; }
}