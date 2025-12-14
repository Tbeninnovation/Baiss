using System.Text.Json.Serialization;

namespace Baiss.Application.Models.AI.Universal;

public record UniversalAIRequest
{
    [JsonPropertyName("messages")]
    public UniversalMessage[] Messages { get; init; } = Array.Empty<UniversalMessage>();

    [JsonPropertyName("system")]
    public UniversalSystemInstructions? System { get; init; }

    [JsonPropertyName("config")]
    public UniversalConfig Config { get; init; } = new();

    [JsonPropertyName("model")]
    public UniversalModel Model { get; init; } = new();

    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    [JsonPropertyName("functions")]
    public object[]? Functions { get; init; }

    [JsonPropertyName("tools")]
    public object[]? Tools { get; init; }
}