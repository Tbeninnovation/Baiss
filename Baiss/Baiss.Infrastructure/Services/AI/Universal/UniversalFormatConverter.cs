using Baiss.Application.Models.AI;
using Baiss.Application.Models.AI.Universal;

namespace Baiss.Infrastructure.Services.AI.Universal;

public static class UniversalFormatConverter
{
    /// <summary>
    /// Convert Universal format to existing ChatMessage format
    /// </summary>
    public static List<ChatMessage> ToLegacyChatMessages(UniversalMessage[] messages)
    {
        return messages.Select(msg => new ChatMessage
        {
            Role = msg.Role,
            Content = CombineContent(msg.Content),
            Timestamp = DateTime.UtcNow
        }).ToList();
    }

    /// <summary>
    /// Convert existing ChatMessage format to Universal format
    /// </summary>
    public static UniversalMessage[] FromLegacyChatMessages(IEnumerable<ChatMessage> messages)
    {
        return messages.Select(msg => new UniversalMessage
        {
            Role = msg.Role,
            Content = new[]
            {
                new UniversalContent
                {
                    Type = UniversalContentTypes.Text,
                    Text = msg.Content
                }
            }
        }).ToArray();
    }

    /// <summary>
    /// Convert Universal config to existing AIRequestOptions
    /// </summary>
    public static AIRequestOptions ToLegacyOptions(UniversalConfig config)
    {
        return new AIRequestOptions
        {
            Temperature = config.Temperature,
            MaxTokens = config.MaxTokens,
            TopP = config.TopP,
            EnableStreaming = config.Stream,
            ConversationId = config.Id,
            AdditionalParameters = new Dictionary<string, object>
            {
                ["parent_id"] = config.ParentId ?? string.Empty,
                ["frequency_penalty"] = config.FrequencyPenalty ?? 0.0,
                ["presence_penalty"] = config.PresencePenalty ?? 0.0,
                ["stop"] = config.Stop ?? Array.Empty<string>()
            }
        };
    }

    /// <summary>
    /// Convert existing AIRequestOptions to Universal config
    /// </summary>
    public static UniversalConfig FromLegacyOptions(AIRequestOptions options, string? id = null, string? parentId = null)
    {
        return new UniversalConfig
        {
            Temperature = options.Temperature ?? 0.7,
            MaxTokens = options.MaxTokens ?? 1024,
            TopP = options.TopP ?? 0.9,
            Stream = options.EnableStreaming,
            Id = id ?? options.ConversationId,
            ParentId = parentId ?? options.AdditionalParameters?.GetValueOrDefault("parent_id")?.ToString(),
            FrequencyPenalty = options.AdditionalParameters?.GetValueOrDefault("frequency_penalty") as double?,
            PresencePenalty = options.AdditionalParameters?.GetValueOrDefault("presence_penalty") as double?,
            Stop = options.AdditionalParameters?.GetValueOrDefault("stop") as string[]
        };
    }

    /// <summary>
    /// Convert AICompletionResponse to Universal format
    /// </summary>
    public static UniversalAIResponse FromLegacyResponse(AICompletionResponse response)
    {
        return new UniversalAIResponse
        {
            Success = response.Success,
            Error = response.Error,
            Provider = response.Provider.ToString(),
            Response = new UniversalResponseData
            {
                Choices = new[]
                {
                    new UniversalChoice
                    {
                        Messages = new[]
                        {
                            new UniversalMessage
                            {
                                Role = UniversalMessageRoles.Assistant,
                                Content = new[]
                                {
                                    new UniversalContent
                                    {
                                        Type = UniversalContentTypes.Text,
                                        Text = response.Content
                                    }
                                }
                            }
                        },
                        Index = 0,
                        FinishReason = response.FinishReason
                    }
                },
                Id = response.ConversationId,
                FinishReason = response.FinishReason
            },
            Usage = response.Usage != null ? new UniversalUsageData
            {
                InputTokens = response.Usage.InputTokens,
                OutputTokens = response.Usage.OutputTokens,
                TotalTokens = response.Usage.TotalTokens,
                ProcessingTimeMs = (long)response.Usage.ProcessingTime.TotalMilliseconds,
                Cost = response.Usage.Cost
            } : null,
            Metadata = response.Metadata,
            Timestamp = response.Timestamp
        };
    }

    /// <summary>
    /// Convert Universal response to AICompletionResponse
    /// </summary>
    public static AICompletionResponse ToLegacyResponse(UniversalAIResponse response)
    {
        var content = response.Response?.Choices?.FirstOrDefault()?.Messages?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text ?? string.Empty;

        return new AICompletionResponse
        {
            Success = response.Success,
            Error = response.Error,
            Content = content,
            Provider = Enum.TryParse<AIProvider>(response.Provider, true, out var provider) ? provider : AIProvider.OpenAI,
            Usage = response.Usage != null ? new AIUsageMetrics
            {
                InputTokens = response.Usage.InputTokens,
                OutputTokens = response.Usage.OutputTokens,
                TotalTokens = response.Usage.TotalTokens,
                ProcessingTime = TimeSpan.FromMilliseconds(response.Usage.ProcessingTimeMs),
                Cost = response.Usage.Cost
            } : null,
            ConversationId = response.Response?.Id,
            FinishReason = response.Response?.FinishReason,
            Timestamp = response.Timestamp,
            Metadata = response.Metadata
        };
    }

    /// <summary>
    /// Extract system prompt from Universal request
    /// </summary>
    public static string? ExtractSystemPrompt(UniversalAIRequest request)
    {
        if (request.System?.Instructions == null || request.System.Instructions.Length == 0)
            return null;

        return CombineContent(request.System.Instructions);
    }

    /// <summary>
    /// Combine content array into a single string
    /// </summary>
    private static string CombineContent(UniversalContent[] content)
    {
        if (content == null || content.Length == 0)
            return string.Empty;

        var textParts = new List<string>();

        foreach (var item in content)
        {
            switch (item.Type)
            {
                case UniversalContentTypes.Text:
                    if (!string.IsNullOrEmpty(item.Text))
                        textParts.Add(item.Text);
                    break;

                case UniversalContentTypes.Url:
                    if (!string.IsNullOrEmpty(item.Url))
                        textParts.Add($"[URL: {item.Url}]");
                    break;

                case UniversalContentTypes.Image:
                    if (!string.IsNullOrEmpty(item.Url))
                        textParts.Add($"[IMAGE: {item.Url}]");
                    break;

                case UniversalContentTypes.Document:
                    if (!string.IsNullOrEmpty(item.Url))
                        textParts.Add($"[DOCUMENT: {item.Url}]");
                    break;

                default:
                    if (!string.IsNullOrEmpty(item.Text))
                        textParts.Add(item.Text);
                    break;
            }
        }

        return string.Join(" ", textParts);
    }

    /// <summary>
    /// Determine provider from Universal request
    /// </summary>
    public static AIProvider? DetermineProvider(UniversalAIRequest request)
    {
        // Priority: explicit provider in request
        if (!string.IsNullOrEmpty(request.Provider))
        {
            if (Enum.TryParse<AIProvider>(request.Provider, true, out var explicitProvider))
                return explicitProvider;
        }

        // Fallback: determine from model configuration
        if (request.Model != null)
        {
            return request.Model.Type switch
            {
                // Local models are not handled by universal service
                "openai" => AIProvider.OpenAI,
                "anthropic" => AIProvider.Anthropic,
                "azure" => AIProvider.AzureOpenAI,
                _ => null
            };
        }

        return null;
    }

    /// <summary>
    /// Create streaming response from text token
    /// </summary>
    public static UniversalStreamResponse CreateStreamResponse(string token, string? provider = null, Dictionary<string, object>? metadata = null)
    {
        return new UniversalStreamResponse
        {
            Success = true,
            Delta = new UniversalMessage
            {
                Role = UniversalMessageRoles.Assistant,
                Content = new[]
                {
                    new UniversalContent
                    {
                        Type = UniversalContentTypes.Text,
                        Text = token
                    }
                }
            },
            Choices = new[]
            {
                new UniversalChoice
                {
                    Delta = new UniversalMessage
                    {
                        Role = UniversalMessageRoles.Assistant,
                        Content = new[]
                        {
                            new UniversalContent
                            {
                                Type = UniversalContentTypes.Text,
                                Text = token
                            }
                        }
                    },
                    Index = 0
                }
            },
            Metadata = metadata
        };
    }
}
