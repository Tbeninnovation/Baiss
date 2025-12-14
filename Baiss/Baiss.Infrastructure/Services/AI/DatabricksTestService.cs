using Baiss.Application.Models.AI;
using Baiss.Application.Models.AI.Universal;
using Baiss.Application.Interfaces;
using Baiss.Infrastructure.Services.AI.Providers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Baiss.Infrastructure.Services.AI;

public class AIProvidersTestService : IHostedService
{
    private readonly ILogger<AIProvidersTestService> _logger;
    private readonly IServiceProvider _serviceProvider;

    // Test configurations for different providers
    private readonly List<ProviderTestConfig> _testConfigs = new()
    {
        new ProviderTestConfig
        {
            Name = "OpenAI",
            Provider = "openai",
            ModelType = "openai",
            ModelName = "gpt-4",
            TestPrompt = "Hello, how are you today? Please respond in one sentence.",
            ExpectedResponse = "I'm doing well"
        },
        new ProviderTestConfig
        {
            Name = "Anthropic",
            Provider = "anthropic",
            ModelType = "anthropic",
            ModelName = "claude-3-sonnet-20240229",
            TestPrompt = "Hello, how are you today? Please respond in one sentence.",
            ExpectedResponse = "I'm doing well"
        },
        new ProviderTestConfig
        {
            Name = "Databricks",
            Provider = "databricks",
            ModelType = "databricks",
            ModelName = "databricks-gemma-3-12b",
            TestPrompt = "Hello, how are you today? Please respond in one sentence.",
            ExpectedResponse = "I'm doing well"
        },
        new ProviderTestConfig
        {
            Name = "Azure OpenAI",
            Provider = "azure",
            ModelType = "azure",
            ModelName = "gpt-4",
            TestPrompt = "Hello, how are you today? Please respond in one sentence.",
            ExpectedResponse = "I'm doing well"
        }
    };

    public AIProvidersTestService(
        ILogger<AIProvidersTestService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🚀 Starting AI Providers Test Service...");

        // Run the test in background to not block app startup
        _ = Task.Run(async () => await RunAllProvidersTestAsync(cancellationToken), cancellationToken);

        return;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🛑 Stopping AI Providers Test Service...");
        return Task.CompletedTask;
    }

    private async Task RunAllProvidersTestAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Add a small delay to let the app finish startup
            await Task.Delay(5000, cancellationToken);

            _logger.LogInformation("🔧 AI Providers Test Configuration");
            _logger.LogInformation("📋 Testing {Count} providers: {Providers}",
                _testConfigs.Count,
                string.Join(", ", _testConfigs.Select(c => c.Name)));

            // Get Universal AI Service
            var universalAIService = _serviceProvider.GetRequiredService<IUniversalAIService>();

            // Test 1: Check Available Providers
            _logger.LogInformation("🏥 Checking Available Providers...");
            var availableProviders = await universalAIService.GetAvailableProvidersAsync();
            _logger.LogInformation("✅ Available providers: {Providers}", string.Join(", ", availableProviders));

            var totalPassed = 0;
            var totalTests = 0;

            // Test each provider
            foreach (var config in _testConfigs)
            {
                _logger.LogInformation("");
                _logger.LogInformation("🧪 Testing {Provider} Provider", config.Name);
                _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                totalTests++;

                // Check if provider is supported
                var isSupported = await universalAIService.IsProviderSupportedAsync(config.Provider);
                if (!isSupported)
                {
                    _logger.LogWarning("⚠️  {Provider} provider is not supported/configured - skipping tests", config.Name);
                    continue;
                }

                // Get provider capabilities
                var capabilities = await universalAIService.GetProviderCapabilitiesAsync(config.Provider);
                _logger.LogInformation("📊 {Provider} Capabilities:", config.Name);
                _logger.LogInformation("   Streaming: {Streaming}", capabilities.GetValueOrDefault("streaming", false));
                _logger.LogInformation("   Max Tokens: {MaxTokens}", capabilities.GetValueOrDefault("max_tokens", "Unknown"));

                var passed = await TestProvider(universalAIService, config, cancellationToken);
                if (passed)
                {
                    totalPassed++;
                    _logger.LogInformation("✅ {Provider} tests: PASSED", config.Name);
                }
                else
                {
                    _logger.LogError("❌ {Provider} tests: FAILED", config.Name);
                }
            }

            // Final Summary
            _logger.LogInformation("");
            _logger.LogInformation("🎉 AI Providers Test Summary");
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _logger.LogInformation("📈 Results: {Passed}/{Total} providers passed tests", totalPassed, totalTests);

            if (totalPassed == totalTests)
            {
                _logger.LogInformation("🎊 All available providers are working correctly!");
            }
            else
            {
                _logger.LogWarning("⚠️  Some providers failed - check configuration");
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 AI Providers Test Service failed with exception: {Message}", ex.Message);
            _logger.LogError("🔍 Please verify your provider configurations in .env file");
        }
    }

    private async Task<bool> TestProvider(IUniversalAIService universalAIService, ProviderTestConfig config, CancellationToken cancellationToken)
    {
        try
        {
            // Test 1: Completion Test
            _logger.LogInformation("💬 Testing {Provider} completion...", config.Name);

            var universalRequest = new UniversalAIRequest
            {
                Messages = new[]
                {
                    new UniversalMessage
                    {
                        Role = "user",
                        Content = new[]
                        {
                            new UniversalContent
                            {
                                Type = "text",
                                Text = config.TestPrompt
                            }
                        }
                    }
                },
                Config = new UniversalConfig
                {
                    Temperature = 0.7f,
                    MaxTokens = 100,
                    TopP = 0.9f
                },
                Model = new UniversalModel
                {
                    Name = config.ModelName,
                    Type = config.ModelType
                },
                Provider = config.Provider
            };

            var response = await universalAIService.ProcessAsync(universalRequest, cancellationToken);

            if (!response.Success)
            {
                _logger.LogError("❌ {Provider} completion failed: {Error}", config.Name, response.Error);
                return false;
            }

            // Extract content from response
            var content = response.Response?.Choices?.FirstOrDefault()?.Messages?.FirstOrDefault()?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
            _logger.LogInformation("✅ {Provider} completion: SUCCESS", config.Name);
            _logger.LogInformation("📝 Response: {Content}", content?.Substring(0, Math.Min(content.Length, 100)) + "...");

            if (response.Usage != null)
            {
                _logger.LogInformation("📊 Usage - Input: {Input}, Output: {Output}, Total: {Total} tokens",
                    response.Usage.InputTokens, response.Usage.OutputTokens, response.Usage.TotalTokens);
            }

            // Test 2: Streaming Test
            _logger.LogInformation("🌊 Testing {Provider} streaming...", config.Name);

            var streamRequest = universalRequest with
            {
                Messages = new[]
                {
                    new UniversalMessage
                    {
                        Role = "user",
                        Content = new[]
                        {
                            new UniversalContent
                            {
                                Type = "text",
                                Text = "Tell me a very short joke."
                            }
                        }
                    }
                },
                Config = universalRequest.Config with { MaxTokens = 50 }
            };

            var streamTokens = new List<string>();
            await foreach (var streamResponse in universalAIService.ProcessStreamAsync(streamRequest, cancellationToken))
            {
                if (streamResponse.Success && streamResponse.Delta?.Content?.Length > 0)
                {
                    var textContent = streamResponse.Delta.Content.FirstOrDefault(c => c.Type == "text")?.Text;
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        streamTokens.Add(textContent);
                    }
                }

                if (streamTokens.Count > 20) // Prevent infinite streaming
                    break;
            }

            if (streamTokens.Count > 0)
            {
                _logger.LogInformation("✅ {Provider} streaming: SUCCESS - Received {Count} tokens", config.Name, streamTokens.Count);
                _logger.LogInformation("📝 Streamed: {Content}", string.Join("", streamTokens).Substring(0, Math.Min(string.Join("", streamTokens).Length, 50)) + "...");
            }
            else
            {
                _logger.LogWarning("⚠️  {Provider} streaming: No tokens received", config.Name);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ {Provider} test failed: {Error}", config.Name, ex.Message);
            return false;
        }
    }
}

public record ProviderTestConfig
{
    public string Name { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string ModelType { get; init; } = string.Empty;
    public string ModelName { get; init; } = string.Empty;
    public string TestPrompt { get; init; } = string.Empty;
    public string ExpectedResponse { get; init; } = string.Empty;
}