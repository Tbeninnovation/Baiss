using Baiss.Application.Models.AI.Universal;

namespace Baiss.Infrastructure.Services.AI.Universal.Adapters;

public class AnthropicUniversalAdapter : IProviderAdapter
{
    public object ConvertRequest(UniversalAIRequest request)
    {
        var anthropicMessages = new List<object>();

        // Anthropic handles system messages differently - they go in a separate field
        string? systemMessage = null;
        if (request.System?.Instructions != null && request.System.Instructions.Length > 0)
        {
            systemMessage = CombineInstructions(request.System.Instructions);
        }

        // Convert messages (excluding system messages)
        foreach (var message in request.Messages.Where(m => m.Role != "system"))
        {
            anthropicMessages.Add(new
            {
                role = message.Role,
                content = ConvertContent(message.Content)
            });
        }

        var anthropicRequest = new
        {
            model = request.Model.Name ?? "claude-3-sonnet-20240229",
            messages = anthropicMessages,
            max_tokens = request.Config.MaxTokens,
            temperature = request.Config.Temperature,
            top_p = request.Config.TopP,
            stream = request.Config.Stream,
            stop_sequences = request.Config.Stop
        };

        // Add system message if present
        if (!string.IsNullOrEmpty(systemMessage))
        {
            return new
            {
                model = anthropicRequest.model,
                messages = anthropicRequest.messages,
                max_tokens = anthropicRequest.max_tokens,
                temperature = anthropicRequest.temperature,
                top_p = anthropicRequest.top_p,
                stream = anthropicRequest.stream,
                stop_sequences = anthropicRequest.stop_sequences,
                system = systemMessage
            };
        }

        return anthropicRequest;
    }

    public UniversalAIResponse ConvertResponse(object providerResponse)
    {
        // This would be implemented based on Anthropic's actual response format
        return new UniversalAIResponse
        {
            Success = true,
            Provider = "anthropic",
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
                                        Text = "Response from Anthropic" // Would extract from actual response
                                    }
                                }
                            }
                        },
                        Index = 0
                    }
                }
            }
        };
    }

    public UniversalStreamResponse ConvertStreamResponse(object providerStreamItem)
    {
        // This would be implemented based on Anthropic's streaming response format
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
                        Text = "stream_token" // Would extract from actual stream item
                    }
                }
            }
        };
    }

    public Dictionary<string, object> GetProviderConfig(UniversalAIRequest request)
    {
        return new Dictionary<string, object>
        {
            ["api_key"] = request.Model.ApiKey ?? string.Empty,
            ["base_url"] = request.Model.Endpoint ?? "https://api.anthropic.com",
            ["model"] = request.Model.Name ?? "claude-3-sonnet-20240229",
            ["anthropic_version"] = "2023-06-01"
        };
    }

    public (bool IsValid, string? Error) ValidateRequest(UniversalAIRequest request)
    {
        if (string.IsNullOrEmpty(request.Model.ApiKey))
        {
            return (false, "Anthropic requires an API key");
        }

        // Check for unsupported content types
        var unsupportedTypes = request.Messages
            .SelectMany(m => m.Content)
            .Where(c => !GetSupportedContentTypes().Contains(c.Type))
            .Select(c => c.Type)
            .Distinct()
            .ToArray();

        if (unsupportedTypes.Any())
        {
            return (false, $"Anthropic does not support content types: {string.Join(", ", unsupportedTypes)}");
        }

        return (true, null);
    }

    public IEnumerable<string> GetSupportedContentTypes()
    {
        return new[]
        {
            UniversalContentTypes.Text,
            UniversalContentTypes.Image,
            UniversalContentTypes.Document
        };
    }

    public Dictionary<string, object> GetCapabilities()
    {
        return new Dictionary<string, object>
        {
            ["provider"] = "anthropic",
            ["max_tokens"] = 200000,
            ["supports_streaming"] = true,
            ["supports_function_calling"] = false, // Note: Anthropic has different function calling
            ["supports_vision"] = true,
            ["supports_documents"] = true,
            ["models"] = new[] { "claude-3-opus-20240229", "claude-3-sonnet-20240229", "claude-3-haiku-20240307" }
        };
    }

    private object ConvertContent(UniversalContent[] content)
    {
        if (content.Length == 1 && content[0].Type == UniversalContentTypes.Text)
        {
            // Simple text content
            return content[0].Text ?? string.Empty;
        }

        // Complex content with multiple parts
        var contentParts = new List<object>();

        foreach (var item in content)
        {
            switch (item.Type)
            {
                case UniversalContentTypes.Text:
                    contentParts.Add(new
                    {
                        type = "text",
                        text = item.Text ?? string.Empty
                    });
                    break;

                case UniversalContentTypes.Image:
                case UniversalContentTypes.Url:
                    if (!string.IsNullOrEmpty(item.Url))
                    {
                        contentParts.Add(new
                        {
                            type = "image",
                            source = new
                            {
                                type = "base64", // or "url" depending on format
                                data = item.Url
                            }
                        });
                    }
                    break;

                case UniversalContentTypes.Document:
                    if (!string.IsNullOrEmpty(item.Url))
                    {
                        contentParts.Add(new
                        {
                            type = "text",
                            text = $"[Document: {item.Url}]"
                        });
                    }
                    break;
            }
        }

        return contentParts;
    }

    private string CombineInstructions(UniversalContent[] instructions)
    {
        return string.Join(" ", instructions
            .Where(i => i.Type == UniversalContentTypes.Text && !string.IsNullOrEmpty(i.Text))
            .Select(i => i.Text));
    }
}