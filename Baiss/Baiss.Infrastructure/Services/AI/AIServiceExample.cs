using Baiss.Application.Interfaces;
using Baiss.Application.Models.AI;
using Microsoft.Extensions.Logging;

namespace Baiss.Infrastructure.Services.AI;

public class AIServiceExample
{
    private readonly ISemanticKernelService _semanticKernelService;
    private readonly IAIStreamingService _streamingService;
    private readonly IDatabricksAIService _databricksService;
    private readonly ILogger<AIServiceExample> _logger;

    public AIServiceExample(
        ISemanticKernelService semanticKernelService,
        IAIStreamingService streamingService,
        IDatabricksAIService databricksService,
        ILogger<AIServiceExample> logger)
    {
        _semanticKernelService = semanticKernelService;
        _streamingService = streamingService;
        _databricksService = databricksService;
        _logger = logger;
    }

    public async Task DemoBasicCompletionAsync()
    {
        _logger.LogInformation("=== Basic Completion Demo ===");

        var prompt = "Explain quantum computing in simple terms";

        var response = await _semanticKernelService.GetCompletionAsync(
            prompt,
            provider: AIProvider.OpenAI,
            options: new AIRequestOptions
            {
                Temperature = 0.7,
                MaxTokens = 500
            });

        if (response.Success)
        {
            _logger.LogInformation("✅ Completion successful");
            _logger.LogInformation("Content: {Content}", response.Content[..Math.Min(100, response.Content.Length)]);
            _logger.LogInformation("Usage: {InputTokens} input, {OutputTokens} output tokens",
                response.Usage?.InputTokens, response.Usage?.OutputTokens);
        }
        else
        {
            _logger.LogError("❌ Completion failed: {Error}", response.Error);
        }
    }

    public async Task DemoStreamingCompletionAsync()
    {
        _logger.LogInformation("=== Streaming Completion Demo ===");

        var prompt = "Write a short story about a robot learning to paint";

        _logger.LogInformation("Starting stream...");

        var tokenCount = 0;
        var content = "";

        await foreach (var token in _streamingService.StreamCompletionAsync(
            prompt,
            provider: AIProvider.OpenAI,
            options: new AIRequestOptions { Temperature = 0.8, MaxTokens = 300 }))
        {
            content += token;
            tokenCount++;

            if (tokenCount % 10 == 0)
            {
                _logger.LogInformation("📡 Received {TokenCount} tokens so far...", tokenCount);
            }
        }

        _logger.LogInformation("✅ Stream completed. Total tokens: {TokenCount}", tokenCount);
        _logger.LogInformation("Final content length: {Length} characters", content.Length);
    }

    public async Task DemoChatConversationAsync()
    {
        _logger.LogInformation("=== Chat Conversation Demo ===");

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "What is machine learning?" },
            new() { Role = "assistant", Content = "Machine learning is a subset of artificial intelligence that enables computers to learn and improve from experience without being explicitly programmed for every task." },
            new() { Role = "user", Content = "Can you give me a simple example?" }
        };

        var response = await _semanticKernelService.GetChatResponseAsync(
            messages,
            provider: AIProvider.OpenAI,
            systemPrompt: "You are a helpful AI assistant that explains complex topics in simple terms.",
            options: new AIRequestOptions { Temperature = 0.6 });

        if (response.Success)
        {
            _logger.LogInformation("✅ Chat response successful");
            _logger.LogInformation("Response: {Content}", response.Content[..Math.Min(200, response.Content.Length)]);
        }
        else
        {
            _logger.LogError("❌ Chat response failed: {Error}", response.Error);
        }
    }

    public async Task DemoStreamingChatAsync()
    {
        _logger.LogInformation("=== Streaming Chat Demo ===");

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Tell me about the history of programming languages, but make it engaging like a story" }
        };

        _logger.LogInformation("Starting chat stream...");

        await foreach (var token in _streamingService.StreamChatAsync(
            messages,
            provider: AIProvider.OpenAI,
            systemPrompt: "You are an engaging storyteller who makes technical topics fun and accessible.",
            options: new AIRequestOptions { Temperature = 0.8, MaxTokens = 400 }))
        {
            Console.Write(token);
        }

        Console.WriteLine();
        _logger.LogInformation("✅ Chat stream completed");
    }

    public async Task DemoProviderSwitchingAsync()
    {
        _logger.LogInformation("=== Provider Switching Demo ===");

        var prompt = "What are the benefits of renewable energy?";

        var providers = await _semanticKernelService.GetAvailableProvidersAsync();
        _logger.LogInformation("Available providers: {Providers}", string.Join(", ", providers));

        foreach (var provider in providers.Take(2))
        {
            _logger.LogInformation("--- Testing {Provider} ---", provider);

            var isAvailable = await _semanticKernelService.IsProviderAvailableAsync(provider);
            if (!isAvailable)
            {
                _logger.LogWarning("⚠️ Provider {Provider} is not available", provider);
                continue;
            }

            var response = await _semanticKernelService.GetCompletionAsync(
                prompt,
                provider: provider,
                options: new AIRequestOptions { MaxTokens = 200 });

            if (response.Success)
            {
                _logger.LogInformation("✅ {Provider}: {Content}", provider,
                    response.Content[..Math.Min(100, response.Content.Length)]);
            }
            else
            {
                _logger.LogError("❌ {Provider} failed: {Error}", provider, response.Error);
            }
        }
    }

    public async Task DemoDatabricksIntegrationAsync()
    {
        _logger.LogInformation("=== Databricks Integration Demo ===");

        var isHealthy = await _databricksService.IsHealthyAsync();
        _logger.LogInformation("Databricks health status: {IsHealthy}", isHealthy ? "✅ Healthy" : "❌ Unhealthy");

        if (!isHealthy)
        {
            _logger.LogWarning("Skipping Databricks demo - service not available");
            return;
        }

        var models = await _databricksService.GetAvailableModelsAsync();
        _logger.LogInformation("Available Databricks models: {Models}", string.Join(", ", models));

        var prompt = "Summarize the key advantages of using machine learning in business";

        _logger.LogInformation("Getting completion from Databricks...");
        var response = await _databricksService.GetCompletionAsync(
            prompt,
            options: new AIRequestOptions { Temperature = 0.5, MaxTokens = 300 });

        if (response.Success)
        {
            _logger.LogInformation("✅ Databricks completion successful");
            _logger.LogInformation("Content: {Content}", response.Content[..Math.Min(150, response.Content.Length)]);
        }
        else
        {
            _logger.LogError("❌ Databricks completion failed: {Error}", response.Error);
        }

        _logger.LogInformation("Testing Databricks streaming...");
        await foreach (var token in _databricksService.StreamCompletionAsync(
            "List 5 emerging technologies in AI",
            options: new AIRequestOptions { MaxTokens = 200 }))
        {
            Console.Write(token);
        }
        Console.WriteLine();
        _logger.LogInformation("✅ Databricks streaming completed");
    }

    public async Task DemoAdvancedStreamingWithMetadataAsync()
    {
        _logger.LogInformation("=== Advanced Streaming with Metadata Demo ===");

        var prompt = "Explain the concept of neural networks";

        await foreach (var response in _streamingService.StreamWithMetadataAsync(
            prompt,
            provider: AIProvider.OpenAI,
            options: new AIRequestOptions { Temperature = 0.7, MaxTokens = 300 }))
        {
            if (response.Success)
            {
                Console.Write(response.Content);

                if (response.Metadata?.ContainsKey("token_count") == true)
                {
                    var tokenCount = response.Metadata["token_count"];
                    var elapsedMs = response.Metadata["elapsed_ms"];

                    if (tokenCount is int tokens && tokens % 20 == 0)
                    {
                        Console.WriteLine($"\n[{tokens} tokens, {elapsedMs}ms elapsed]");
                    }
                }
            }
            else
            {
                _logger.LogError("❌ Stream error: {Error}", response.Error);
                break;
            }
        }

        Console.WriteLine();
        _logger.LogInformation("✅ Advanced streaming completed");
    }

    public async Task RunAllDemosAsync()
    {
        _logger.LogInformation("🚀 Starting AI Service Demonstration");

        try
        {
            await DemoBasicCompletionAsync();
            await Task.Delay(1000);

            await DemoStreamingCompletionAsync();
            await Task.Delay(1000);

            await DemoChatConversationAsync();
            await Task.Delay(1000);

            await DemoStreamingChatAsync();
            await Task.Delay(1000);

            await DemoProviderSwitchingAsync();
            await Task.Delay(1000);

            await DemoDatabricksIntegrationAsync();
            await Task.Delay(1000);

            await DemoAdvancedStreamingWithMetadataAsync();

            _logger.LogInformation("🎉 All demos completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Demo execution failed");
        }
    }
}