using Baiss.Application.Interfaces;
using Baiss.Application.Models.AI.Universal;
using Microsoft.Extensions.Logging;

namespace Baiss.Infrastructure.Services.AI.Universal;

public class UniversalAIServiceExample
{
    private readonly IUniversalAIService _universalAIService;
    private readonly ILogger<UniversalAIServiceExample> _logger;

    public UniversalAIServiceExample(
        IUniversalAIService universalAIService,
        ILogger<UniversalAIServiceExample> logger)
    {
        _universalAIService = universalAIService;
        _logger = logger;
    }

    public async Task DemoBasicTextRequestAsync()
    {
        _logger.LogInformation("=== Basic Text Request Demo ===");

        var request = new UniversalAIRequest
        {
            Messages = new[]
            {
                new UniversalMessage
                {
                    Role = UniversalMessageRoles.User,
                    Content = new[]
                    {
                        new UniversalContent
                        {
                            Type = UniversalContentTypes.Text,
                            Text = "Explain quantum computing in simple terms",
                            Language = "en"
                        }
                    }
                }
            },
            System = new UniversalSystemInstructions
            {
                Instructions = new[]
                {
                    new UniversalContent
                    {
                        Type = UniversalContentTypes.Text,
                        Text = "You are a helpful assistant that explains complex topics in simple terms."
                    }
                }
            },
            Config = new UniversalConfig
            {
                Temperature = 0.7,
                MaxTokens = 500,
                TopP = 0.9,
                Stream = false,
                Id = Guid.NewGuid().ToString()
            },
            Model = new UniversalModel
            {
                Type = UniversalModelTypes.Cloud,
                Name = "gpt-4",
                Provider = "openai"
            }
        };

        var response = await _universalAIService.ProcessAsync(request);

        if (response.Success)
        {
            _logger.LogInformation("✅ Request successful");
            var content = response.Response?.Choices?.FirstOrDefault()?.Messages?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text;
            _logger.LogInformation("Response: {Content}", content?[..Math.Min(200, content?.Length ?? 0)]);
            _logger.LogInformation("Usage: {InputTokens} input, {OutputTokens} output",
                response.Usage?.InputTokens, response.Usage?.OutputTokens);
        }
        else
        {
            _logger.LogError("❌ Request failed: {Error}", response.Error);
        }
    }

    public async Task DemoMultiContentRequestAsync()
    {
        _logger.LogInformation("=== Multi-Content Request Demo ===");

        var request = new UniversalAIRequest
        {
            Messages = new[]
            {
                new UniversalMessage
                {
                    Role = UniversalMessageRoles.User,
                    Content = new[]
                    {
                        new UniversalContent
                        {
                            Type = UniversalContentTypes.Text,
                            Text = "Please analyze this document",
                            Language = "en"
                        },
                        new UniversalContent
                        {
                            Type = UniversalContentTypes.Url,
                            Url = "https://example.com/pdfs/example.pdf"
                        }
                    }
                }
            },
            Config = new UniversalConfig
            {
                Temperature = 0.5,
                MaxTokens = 1024,
                Stream = false
            },
            Model = new UniversalModel
            {
                Type = UniversalModelTypes.Cloud,
                Provider = "anthropic"
            }
        };

        var response = await _universalAIService.ProcessAsync(request);

        if (response.Success)
        {
            _logger.LogInformation("✅ Multi-content request successful");
            var content = response.Response?.Choices?.FirstOrDefault()?.Messages?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text;
            _logger.LogInformation("Analysis: {Content}", content?[..Math.Min(300, content?.Length ?? 0)]);
        }
        else
        {
            _logger.LogError("❌ Multi-content request failed: {Error}", response.Error);
        }
    }

    public async Task DemoStreamingRequestAsync()
    {
        _logger.LogInformation("=== Streaming Request Demo ===");

        var request = new UniversalAIRequest
        {
            Messages = new[]
            {
                new UniversalMessage
                {
                    Role = UniversalMessageRoles.User,
                    Content = new[]
                    {
                        new UniversalContent
                        {
                            Type = UniversalContentTypes.Text,
                            Text = "Write a short story about a robot learning to paint"
                        }
                    }
                }
            },
            Config = new UniversalConfig
            {
                Temperature = 0.8,
                MaxTokens = 400,
                Stream = true,
                Id = "story-generation-" + Guid.NewGuid().ToString("N")[..8]
            },
            Model = new UniversalModel
            {
                Type = UniversalModelTypes.Cloud,
                Provider = "openai"
            }
        };

        _logger.LogInformation("Starting stream...");

        var tokenCount = 0;
        var startTime = DateTime.UtcNow;

        await foreach (var streamResponse in _universalAIService.ProcessStreamAsync(request))
        {
            if (streamResponse.Success && streamResponse.Delta?.Content != null)
            {
                var token = streamResponse.Delta.Content.FirstOrDefault()?.Text;
                if (!string.IsNullOrEmpty(token))
                {
                    Console.Write(token);
                    tokenCount++;

                    if (tokenCount % 20 == 0)
                    {
                        _logger.LogInformation("📡 Received {TokenCount} tokens so far...", tokenCount);
                    }
                }
            }
            else if (!streamResponse.Success)
            {
                _logger.LogError("❌ Stream error: {Error}", streamResponse.Error);
                break;
            }
        }

        Console.WriteLine();
        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation("✅ Stream completed. Total tokens: {TokenCount}, Duration: {Duration}ms",
            tokenCount, duration.TotalMilliseconds);
    }

    public async Task DemoConversationRequestAsync()
    {
        _logger.LogInformation("=== Conversation Request Demo ===");

        var conversationId = Guid.NewGuid().ToString();

        // First message
        var request1 = new UniversalAIRequest
        {
            Messages = new[]
            {
                new UniversalMessage
                {
                    Role = UniversalMessageRoles.User,
                    Content = new[]
                    {
                        new UniversalContent
                        {
                            Type = UniversalContentTypes.Text,
                            Text = "What is machine learning?"
                        }
                    }
                }
            },
            Config = new UniversalConfig
            {
                Temperature = 0.6,
                MaxTokens = 200,
                Id = conversationId + "-1"
            },
            Model = new UniversalModel
            {
                Type = UniversalModelTypes.Cloud,
                Provider = "openai"
            }
        };

        var response1 = await _universalAIService.ProcessAsync(request1);
        var assistantResponse = response1.Response?.Choices?.FirstOrDefault()?.Messages?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text ?? "No response";

        _logger.LogInformation("First response: {Response}", assistantResponse[..Math.Min(150, assistantResponse.Length)]);

        // Follow-up message
        var request2 = new UniversalAIRequest
        {
            Messages = new[]
            {
                new UniversalMessage
                {
                    Role = UniversalMessageRoles.User,
                    Content = new[]
                    {
                        new UniversalContent { Type = UniversalContentTypes.Text, Text = "What is machine learning?" }
                    }
                },
                new UniversalMessage
                {
                    Role = UniversalMessageRoles.Assistant,
                    Content = new[]
                    {
                        new UniversalContent { Type = UniversalContentTypes.Text, Text = assistantResponse }
                    }
                },
                new UniversalMessage
                {
                    Role = UniversalMessageRoles.User,
                    Content = new[]
                    {
                        new UniversalContent
                        {
                            Type = UniversalContentTypes.Text,
                            Text = "Can you give me a simple example?"
                        }
                    }
                }
            },
            Config = new UniversalConfig
            {
                Temperature = 0.6,
                MaxTokens = 200,
                Id = conversationId + "-2",
                ParentId = conversationId + "-1"
            },
            Model = new UniversalModel
            {
                Type = UniversalModelTypes.Cloud,
                Provider = "openai"
            }
        };

        var response2 = await _universalAIService.ProcessAsync(request2);

        if (response2.Success)
        {
            var followUpResponse = response2.Response?.Choices?.FirstOrDefault()?.Messages?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text;
            _logger.LogInformation("✅ Follow-up response: {Response}", followUpResponse?[..Math.Min(200, followUpResponse?.Length ?? 0)]);
        }
    }

    public async Task DemoProviderSwitchingAsync()
    {
        _logger.LogInformation("=== Provider Switching Demo ===");

        var baseRequest = new UniversalAIRequest
        {
            Messages = new[]
            {
                new UniversalMessage
                {
                    Role = UniversalMessageRoles.User,
                    Content = new[]
                    {
                        new UniversalContent
                        {
                            Type = UniversalContentTypes.Text,
                            Text = "What are the benefits of renewable energy?"
                        }
                    }
                }
            },
            Config = new UniversalConfig
            {
                Temperature = 0.7,
                MaxTokens = 150
            }
        };

        var providers = await _universalAIService.GetAvailableProvidersAsync();
        _logger.LogInformation("Available providers: {Providers}", string.Join(", ", providers));

        foreach (var provider in providers.Take(3))
        {
            _logger.LogInformation("--- Testing {Provider} ---", provider);

            var isSupported = await _universalAIService.IsProviderSupportedAsync(provider);
            if (!isSupported)
            {
                _logger.LogWarning("⚠️ Provider {Provider} is not available", provider);
                continue;
            }

            var requestWithProvider = baseRequest with
            {
                Provider = provider,
                Model = new UniversalModel
                {
                    Type = provider == "databricks" ? UniversalModelTypes.Local : UniversalModelTypes.Cloud,
                    Provider = provider
                }
            };

            var response = await _universalAIService.ProcessWithProviderAsync(requestWithProvider, provider);

            if (response.Success)
            {
                var content = response.Response?.Choices?.FirstOrDefault()?.Messages?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text;
                _logger.LogInformation("✅ {Provider}: {Content}", provider, content?[..Math.Min(100, content?.Length ?? 0)]);
            }
            else
            {
                _logger.LogError("❌ {Provider} failed: {Error}", provider, response.Error);
            }
        }
    }

    public async Task DemoCapabilitiesAsync()
    {
        _logger.LogInformation("=== Provider Capabilities Demo ===");

        var providers = await _universalAIService.GetAvailableProvidersAsync();

        foreach (var provider in providers)
        {
            _logger.LogInformation("--- {Provider} Capabilities ---", provider);

            var capabilities = await _universalAIService.GetProviderCapabilitiesAsync(provider);
            var contentTypes = await _universalAIService.GetSupportedContentTypesAsync(provider);

            _logger.LogInformation("Max tokens: {MaxTokens}", capabilities.GetValueOrDefault("max_tokens"));
            _logger.LogInformation("Streaming: {Streaming}", capabilities.GetValueOrDefault("supports_streaming"));
            _logger.LogInformation("Function calling: {FunctionCalling}", capabilities.GetValueOrDefault("supports_function_calling"));
            _logger.LogInformation("Content types: {ContentTypes}", string.Join(", ", contentTypes));
        }
    }

    public async Task RunAllDemosAsync()
    {
        _logger.LogInformation("🚀 Starting Universal AI Service Demonstration");

        try
        {
            await DemoBasicTextRequestAsync();
            await Task.Delay(1000);

            await DemoMultiContentRequestAsync();
            await Task.Delay(1000);

            await DemoStreamingRequestAsync();
            await Task.Delay(1000);

            await DemoConversationRequestAsync();
            await Task.Delay(1000);

            await DemoProviderSwitchingAsync();
            await Task.Delay(1000);

            await DemoCapabilitiesAsync();

            _logger.LogInformation("🎉 All Universal AI demos completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Demo execution failed");
        }
    }
}