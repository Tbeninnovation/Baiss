using System.Text.Json.Serialization;

namespace Baiss.Application.Models.AI.Universal;

public record UniversalContent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }
}

public static class UniversalContentTypes
{
    public const string Text = "text";
    public const string Url = "url";
    public const string Image = "image";
    public const string Document = "document";
}