using System.Text.Json.Serialization;

namespace Baiss.Application.Models.AI.Universal;

public record UniversalMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public UniversalContent[] Content { get; init; } = Array.Empty<UniversalContent>();
}

public static class UniversalMessageRoles
{
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string System = "system";
}