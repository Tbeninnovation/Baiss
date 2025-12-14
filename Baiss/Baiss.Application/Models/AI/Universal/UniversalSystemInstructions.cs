using System.Text.Json.Serialization;

namespace Baiss.Application.Models.AI.Universal;

public record UniversalSystemInstructions
{
    [JsonPropertyName("instructions")]
    public UniversalContent[] Instructions { get; init; } = Array.Empty<UniversalContent>();
}