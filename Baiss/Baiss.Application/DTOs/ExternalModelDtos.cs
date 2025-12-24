using System.Text.Json.Serialization;

namespace Baiss.Application.DTOs;

public class ModelDetailsRequestDto
{
    [JsonPropertyName("model_id")]
    public required string ModelId { get; set; }

    [JsonPropertyName("token")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Token { get; set; }
}

public class ModelDetailsResponseDto
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}


