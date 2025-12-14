using Baiss.Application.Models.AI.Universal;

namespace Baiss.Infrastructure.Services.AI.Universal.Adapters;

public class DatabricksUniversalAdapter : IProviderAdapter
{
    public object ConvertRequest(UniversalAIRequest request)
    {
        var databricksMessages = new List<object>();

        // Add system message if present
        if (request.System?.Instructions != null && request.System.Instructions.Length > 0)
        {
            databricksMessages.Add(new
            {
                role = "system",
                content = CombineInstructions(request.System.Instructions)
            });
        }

        // Convert messages
        foreach (var message in request.Messages)
        {
            databricksMessages.Add(new
            {
                role = message.Role,
                content = ConvertContentToText(message.Content)
            });
        }

        return new
        {
            inputs = new
            {
                messages = databricksMessages
            },
            parameters = new
            {
                temperature = request.Config.Temperature,
                max_tokens = request.Config.MaxTokens,
                top_p = request.Config.TopP,
                stream = request.Config.Stream
            }
        };
    }

    public UniversalAIResponse ConvertResponse(object providerResponse)
    {
        // This would be implemented based on Databricks' actual response format
        return new UniversalAIResponse
        {
            Success = true,
            Provider = "databricks",
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
                                        Text = "Response from Databricks" // Would extract from actual response
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
        // This would be implemented based on Databricks' streaming response format
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
            ["workspace_url"] = request.Model.Endpoint ?? string.Empty,
            ["token"] = request.Model.ApiKey ?? string.Empty,
            ["serving_endpoint"] = request.Model.Name ?? string.Empty,
            ["model_path"] = request.Model.Path ?? string.Empty
        };
    }

    public (bool IsValid, string? Error) ValidateRequest(UniversalAIRequest request)
    {
        if (string.IsNullOrEmpty(request.Model.Endpoint))
        {
            return (false, "Databricks requires a workspace URL (endpoint)");
        }

        if (string.IsNullOrEmpty(request.Model.ApiKey))
        {
            return (false, "Databricks requires an access token (api_key)");
        }

        if (string.IsNullOrEmpty(request.Model.Name) && string.IsNullOrEmpty(request.Model.Path))
        {
            return (false, "Databricks requires either a serving endpoint name or model path");
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
            return (false, $"Databricks does not support content types: {string.Join(", ", unsupportedTypes)}");
        }

        return (true, null);
    }

    public IEnumerable<string> GetSupportedContentTypes()
    {
        return new[]
        {
            UniversalContentTypes.Text,
            UniversalContentTypes.Document
        };
    }

    public Dictionary<string, object> GetCapabilities()
    {
        return new Dictionary<string, object>
        {
            ["provider"] = "databricks",
            ["max_tokens"] = 32000,
            ["supports_streaming"] = true,
            ["supports_function_calling"] = false,
            ["supports_vision"] = false,
            ["supports_local_models"] = true,
            ["supports_custom_models"] = true,
            ["models"] = new[] { "llama-2-70b-chat", "code-llama-34b", "custom-models" }
        };
    }

    private string ConvertContentToText(UniversalContent[] content)
    {
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
                        textParts.Add($"[Document URL: {item.Url}]");
                    break;

                case UniversalContentTypes.Document:
                    if (!string.IsNullOrEmpty(item.Url))
                        textParts.Add($"[Document: {item.Url}]");
                    break;

                default:
                    if (!string.IsNullOrEmpty(item.Text))
                        textParts.Add(item.Text);
                    break;
            }
        }

        return string.Join(" ", textParts);
    }

    private string CombineInstructions(UniversalContent[] instructions)
    {
        return string.Join(" ", instructions
            .Where(i => i.Type == UniversalContentTypes.Text && !string.IsNullOrEmpty(i.Text))
            .Select(i => i.Text));
    }
}