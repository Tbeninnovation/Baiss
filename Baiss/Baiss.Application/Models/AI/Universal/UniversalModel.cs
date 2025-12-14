using System.Text.Json.Serialization;

namespace Baiss.Application.Models.AI.Universal;

public record UniversalModel
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; init; }

    [JsonPropertyName("api_key")]
    public string? ApiKey { get; init; }
}

public static class UniversalModelTypes
{
    public const string Local = "local";
    public const string Remote = "remote";
    public const string Cloud = "cloud";
    public const string Hosted = "hosted";
}