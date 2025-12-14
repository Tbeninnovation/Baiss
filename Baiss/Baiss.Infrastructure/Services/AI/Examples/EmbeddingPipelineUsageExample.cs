using Baiss.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Baiss.Infrastructure.Services.AI.Examples;

/// <summary>
/// Example showing how to use the production EmbeddingPipeline with real Databricks embeddings
/// </summary>
public static class EmbeddingPipelineUsageExample
{
    /// <summary>
    /// Run the embedding pipeline with dependency injection
    /// </summary>
    public static async Task RunWithDIAsync(IServiceProvider serviceProvider, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            // Get the embeddings service from DI container
            var embeddingsService = serviceProvider.GetRequiredService<IEmbeddingsService>();
            var pythonBridgeService = serviceProvider.GetRequiredService<IPythonBridgeService>();

            logger.LogInformation("Starting embedding pipeline with real Databricks embeddings...");

            // Run the pipeline with real embeddings
            await Baiss.Infrastructure.Services.AI.EmbeddingPipeline.RunAsync(logger, embeddingsService, pythonBridgeService, ct);

            logger.LogInformation("Embedding pipeline completed successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running embedding pipeline");
            throw;
        }
    }

    /// <summary>
    /// Alternative method for manual service construction (if DI not available)
    /// </summary>
    public static async Task RunManualAsync(ILogger logger, CancellationToken ct = default)
    {
        try
        {
            // Note: You would need to manually construct the embeddings service and python bridge service
            // This requires DatabricksConfig, HttpClient, etc.
            // It's recommended to use the DI approach above instead

            logger.LogWarning("Manual construction not implemented - use RunWithDIAsync instead");

            // For now, run without embeddings service (will log warning and skip)
            // Note: This will fail now because pythonBridgeService is required
            logger.LogError("Cannot run without IPythonBridgeService - manual construction not supported");
            throw new NotSupportedException("Manual construction without DI is not supported for this version");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running embedding pipeline manually");
            throw;
        }
    }
}
