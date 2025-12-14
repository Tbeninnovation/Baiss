using System.Text.Json;
using Baiss.Application.Interfaces;
using Baiss.Application.Models;
using Baiss.Domain.Entities;
// using Baiss.Infrastructure.Interop;
using Baiss.Infrastructure.Services.AI.Providers;
using Microsoft.Extensions.Logging;

namespace Baiss.Infrastructure.Services.AI;

public class EmbeddingsService : IEmbeddingsService
{
    private readonly DatabricksEmbeddingsClient _databricksClient;
    private readonly IPythonBridgeService _pythonBridgeService;
    private readonly ILogger<EmbeddingsService> _logger;

    public EmbeddingsService(
        DatabricksEmbeddingsClient databricksClient,
        IPythonBridgeService pythonBridgeService,
        ILogger<EmbeddingsService> logger)
    {
        _databricksClient = databricksClient;
        _pythonBridgeService = pythonBridgeService;
        _logger = logger;
    }

    // public Task<float[]> EmbedAsync(string text , string modelName ,  CancellationToken cancellationToken = default)
    //     => _databricksClient.EmbedAsync(text, modelName, cancellationToken);

    public async Task<List<float>> EmbeddingModelsAi(string text, Model model, string aiModelType, CancellationToken ct)
    {
        try
        {
            {
                if (aiModelType == ModelTypes.Local)
                {

                    // Generate embedding by calling the local server
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(30);

                    var requestContent = new
                    {
                        content = text
                    };

                    var jsonContent = JsonSerializer.Serialize(requestContent);
                    var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                    HttpResponseMessage? response = null;
                    // int maxRetries = 1;
                    // int currentRetry = 0;

                    // while (currentRetry < maxRetries)
                    // {
                        // try
                        // {
                            response = await httpClient.PostAsync("http://localhost:8081/embedding", httpContent, ct);
                            response.EnsureSuccessStatusCode();
                            // break; // Success, exit retry loop
                        // }
                        // catch (HttpRequestException ex) when (currentRetry < maxRetries - 1)
                        // {
                        //     currentRetry++;
                        //     _logger.LogWarning($"Connection failed (attempt {currentRetry}/{maxRetries}): {ex.Message}. Retrying in 5 seconds...");
                        //     await Task.Delay(5000, ct); // Wait 5 seconds before retry

                        //     // Recreate the content for retry since it may have been consumed
                        //     httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                        // }
                        // catch (TaskCanceledException ex) when (currentRetry < maxRetries - 1 && !ct.IsCancellationRequested)
                        // {
                        //     currentRetry++;
                        //     _logger.LogWarning($"Request timeout (attempt {currentRetry}/{maxRetries}): {ex.Message}. Retrying in 5 seconds...");
                        //     await Task.Delay(5000, ct); // Wait 5 seconds before retry

                        //     // Recreate the content for retry since it may have been consumed
                        //     httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                        // }
                    // }

                    // If all retries failed, throw an exception
                    if (response == null)
                    {
                        throw new InvalidOperationException($"Failed to connect to embedding server ");
                    }
                    var responseJson = await response.Content.ReadAsStringAsync(ct);
                    var responseArray = JsonSerializer.Deserialize<JsonElement>(responseJson);

                    var vector = new List<float>();
                    // Response is an array with one object containing index and embedding
                    if (responseArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in responseArray.EnumerateArray())
                        {
                            if (item.TryGetProperty("embedding", out var embeddingData))
                            {
                                // embedding is an array of arrays [[...]], take the first array
                                if (embeddingData.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var innerArray in embeddingData.EnumerateArray())
                                    {
                                        if (innerArray.ValueKind == JsonValueKind.Array)
                                        {
                                            foreach (var value in innerArray.EnumerateArray())
                                            {
                                                vector.Add((float)value.GetDouble());
                                            }
                                            break; // Only take the first inner array
                                        }
                                    }
                                }
                            }
                        }
                        return vector;
                    }
                }
                else
                {
                    // if databricks in model provider use databricks
                    if (model != null && model.Provider == "databricks" && !string.IsNullOrWhiteSpace(model.Name))
                    {
                        var vector = await _databricksClient.EmbedAsync(text, model.Name, ct);
                        return vector.ToList();
                    }
                }
            }
            return new List<float>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error generating embedding: {ex.Message}");
            throw new InvalidOperationException("Error generating embedding using EmbeddingModelsAi", ex);
        }
    }


}
