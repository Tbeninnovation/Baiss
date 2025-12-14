using Baiss.Application.Models.AI.Universal;

namespace Baiss.Infrastructure.Services.AI.Universal.Adapters;

public class AzureOpenAIUniversalAdapter : IProviderAdapter
{
    public object ConvertRequest(UniversalAIRequest request)
    {
        var azureMessages = new List<object>();

        // Add system message if present
        if (request.System?.Instructions != null && request.System.Instructions.Length > 0)
        {
            azureMessages.Add(new
            {
                role = "system",
                content = CombineInstructions(request.System.Instructions)
            });
        }

        // Convert messages
        foreach (var message in request.Messages)
        {
            azureMessages.Add(new
            {
                role = message.Role,
                content = ConvertContent(message.Content)
            });
        }

        return new
        {
            messages = azureMessages,
            temperature = request.Config.Temperature,
            max_tokens = request.Config.MaxTokens,
            top_p = request.Config.TopP,
            stream = request.Config.Stream,
            frequency_penalty = request.Config.FrequencyPenalty ?? 0.0,
            presence_penalty = request.Config.PresencePenalty ?? 0.0,
            stop = request.Config.Stop
        };
    }

    public UniversalAIResponse ConvertResponse(object providerResponse)
    {
        // This would be implemented based on Azure OpenAI's actual response format
        return new UniversalAIResponse
        {
            Success = true,
            Provider = "azure",
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
                                        Text = "Response from Azure OpenAI" // Would extract from actual response
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
        // This would be implemented based on Azure OpenAI's streaming response format
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
            ["endpoint"] = request.Model.Endpoint ?? string.Empty,
            ["deployment_name"] = request.Model.Name ?? string.Empty,
            ["api_version"] = "2024-02-01"
        };
    }

    public (bool IsValid, string? Error) ValidateRequest(UniversalAIRequest request)
    {
        if (string.IsNullOrEmpty(request.Model.ApiKey))
        {
            return (false, "Azure OpenAI requires an API key");
        }

        if (string.IsNullOrEmpty(request.Model.Endpoint))
        {
            return (false, "Azure OpenAI requires an endpoint URL");
        }

        if (string.IsNullOrEmpty(request.Model.Name))
        {
            return (false, "Azure OpenAI requires a deployment name");
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
            return (false, $"Azure OpenAI does not support content types: {string.Join(", ", unsupportedTypes)}");
        }

        return (true, null);
    }

    public IEnumerable<string> GetSupportedContentTypes()
    {
        return new[]
        {
            UniversalContentTypes.Text,
            UniversalContentTypes.Image,
            UniversalContentTypes.Url
        };
    }

    public Dictionary<string, object> GetCapabilities()
    {
        return new Dictionary<string, object>
        {
            ["provider"] = "azure",
            ["max_tokens"] = 128000,
            ["supports_streaming"] = true,
            ["supports_function_calling"] = true,
            ["supports_vision"] = true,
            ["supports_json_mode"] = true,
            ["enterprise_features"] = true,
            ["models"] = new[] { "gpt-4", "gpt-4-turbo", "gpt-35-turbo", "gpt-4o" }
        };
    }

    private object ConvertContent(UniversalContent[] content)
    {
        if (content.Length == 1 && content[0].Type == UniversalContentTypes.Text)
        {
            // Simple text content
            return content[0].Text ?? string.Empty;
        }

        // Complex content with multiple parts (similar to OpenAI)
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
                            type = "image_url",
                            image_url = new { url = item.Url }
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