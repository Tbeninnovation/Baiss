using System.Text.Json.Serialization;

namespace Baiss.Application.DTOs;

public class ChatConfigDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("response")]
    public ChatConfigResponseDto? Response { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class ChatConfigResponseDto
{
    [JsonPropertyName("system")]
    public SystemConfigDto? System { get; set; }

    [JsonPropertyName("config")]
    public ConfigDto? Config { get; set; }

    [JsonPropertyName("model")]
    public ModelConfigDto? Model { get; set; }
}

public class SystemConfigDto
{
    [JsonPropertyName("instructions")]
    public List<InstructionDto>? Instructions { get; set; }
}

public class InstructionDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class ConfigDto
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }
}

public class ModelConfigDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
}
