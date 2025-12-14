using System.Dynamic;
using System.Text.Json;
using Baiss.Application.Interfaces;
using Baiss.Application.Models;
using Baiss.Domain.Entities;
// using Baiss.Infrastructure.Interop;
using Microsoft.Extensions.Logging;

namespace Baiss.Infrastructure.Services.AI;

/// <summary>
/// Production embedding pipeline: scans for paths without embeddings via Python bridge
/// and fills embeddings using the configured Databricks embedding model.
/// Uses the singleton IPythonBridgeService for Python interop.
/// </summary>
public static class EmbeddingPipeline
{
    private static ILaunchServerService? _launchServerService;

    private static ISettingsRepository? _settingsRepository;
    private static IModelRepository? _modelRepository;
    private static IEmbeddingsService? _embeddingsService;

    public static void Initialize(ILaunchServerService launchServerService, ISettingsRepository settingsRepository, IModelRepository modelRepository, IEmbeddingsService embeddingsService)
    {
        _launchServerService = launchServerService;
        _settingsRepository = settingsRepository;
        _modelRepository = modelRepository;
        _embeddingsService = embeddingsService;
    }

    public static async Task RunAsync(ILogger? logger = null, IEmbeddingsService? embeddingsService = null, IPythonBridgeService? pythonBridgeService = null, CancellationToken ct = default)
    {
        Settings? settings = null;
        try
        {
            if (pythonBridgeService == null)
            {
                logger?.LogError("IPythonBridgeService is required but was not provided");
                throw new ArgumentNullException(nameof(pythonBridgeService), "PythonBridge service is required");
            }

            // Quick health check first to avoid blocking
            if (!pythonBridgeService.IsHealthyQuick())
            {
                logger?.LogWarning("Python bridge service appears unhealthy, attempting full health check...");

                // Only do the potentially blocking health check if quick check fails
                var isHealthy = await pythonBridgeService.ValidateHealthAsync();
                if (!isHealthy)
                {
                    logger?.LogError("PythonBridge service is not healthy");
                    throw new InvalidOperationException("PythonBridge service is not healthy");
                }
            }

            if (embeddingsService == null)
            {
                logger?.LogWarning("No embeddings service provided, skipping embedding generation");
                return;
            }

            // 1) Get all paths without embeddings
            var resPaths = await pythonBridgeService.CallFunctionAsync("files", "get_all_paths_wo_embeddings");
            if (!resPaths.IsSuccess)
            {
                logger?.LogError("Failed to get paths without embeddings: {Error}", resPaths.ErrorMessage);
                throw new InvalidOperationException($"Failed to get paths without embeddings: {resPaths.ErrorMessage}");
            }

            var paths = ExtractArrayOfStrings(resPaths.Result);
            logger?.LogInformation("Paths without embeddings: {Count}", paths.Count);
            if (paths.Count == 0)
            {
                logger?.LogInformation("Nothing to process");
                return;
            }

            // 2) Fetch chunks for a small batch
            var batch = paths.ToList();
            var reqChunks = new { paths = batch };
            var resChunks = await pythonBridgeService.CallFunctionAsync("files", "get_chunks_by_paths", reqChunks);
            if (!resChunks.IsSuccess)
            {
                logger?.LogError("Failed to get chunks by paths: {Error}", resChunks.ErrorMessage);
                throw new InvalidOperationException($"Failed to get chunks by paths: {resChunks.ErrorMessage}");
            }

            var chunks = ExtractChunks(resChunks.Result);
            logger?.LogInformation("Retrieved chunks: {Count}", chunks.Count);
            if (chunks.Count == 0)
            {
                logger?.LogInformation("No chunks for selected paths");
                return;
            }

            settings = await _settingsRepository.GetAsync();
            if (settings == null || string.IsNullOrWhiteSpace(settings.AIEmbeddingModelId))
            {
                logger?.LogError("No embedding model configured in settings");
                throw new InvalidOperationException("No embedding model configured in settings");
            }
            var model = await _modelRepository.GetModelByIdAsync(settings.AIEmbeddingModelId);
            if (model == null)
            {
                logger?.LogError("Embedding model with ID {ModelId} not found", settings.AIEmbeddingModelId);
                throw new InvalidOperationException($"Embedding model with ID {settings.AIEmbeddingModelId} not found");
            }
            // if (settings.AIModelType == ModelTypes.Local)
            // {
            //     // Ensure local embedding server is running
            //     if (_launchServerService == null)
            //     {
            //         logger?.LogError("LaunchServerService is not initialized for local embeddings");
            //         throw new InvalidOperationException("LaunchServerService is not initialized for local embeddings");
            //     }

            //     // await _launchServerService.LaunchLlamaCppServerAsync("embedding", model.LocalPath, " --embeddings");

            //     // Wait for server to be ready
            //     // await Task.Delay(5000, ct);
            // }


            // 3) Generate embeddings and persist
            foreach (var c in chunks)
            {
                ct.ThrowIfCancellationRequested();

                var chunkText = c.chunk_content ?? $"Content from {c.path}";
                logger?.LogInformation("Generating embedding for id={Id}, length={Length}", c.id, chunkText.Length);

                try
                {
                    // ! herr
                    // var vector = await embeddingsService.EmbedAsync(chunkText, ct);
                    var vector = await embeddingsService.EmbeddingModelsAi(chunkText, model, settings.AIModelType, ct);

                    // Convert to List<float> to interop with Python JSON expectations
                    var embeddingObject = new Dictionary<string, object>
                    {
                        ["id"] = c.id,
                        ["embedding"] = vector.ToList()
                    };
                    var payload = new Dictionary<string, object>
                    {
                        ["values"] = new List<Dictionary<string, object>> { embeddingObject }
                    };

                    var resFill = await pythonBridgeService.CallFunctionAsync("files", "fill_in_missing_embeddings", payload);
                    if (!resFill.IsSuccess)
                    {
                        logger?.LogError("Failed to fill missing embeddings for id={Id}: {Error}", c.id, resFill.ErrorMessage);
                        continue;
                    }

                    var success = ExtractSuccess(resFill.Result);
                    logger?.LogInformation("Fill id={Id} success={Success}", c.id, success);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to generate or store embedding for id={Id}", c.id);
                }
            }



        }

        catch (Exception ex)
        {
            logger?.LogError(ex, "Error in embedding pipeline");
            throw;
        }
        // finally
        // {
        //     if (settings?.AIModelType == ModelTypes.Local)
        //     {
        //         await _launchServerService.StopServerByTypeAsync("embedding");
        //     }
        // }
    }


    private record Chunk(long id, string path, string? chunk_content);

    private static List<string> ExtractArrayOfStrings(string? json)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(json)) return list;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("response", out var response))
        {
            if (response.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in response.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var s = item.GetString();
                        if (!string.IsNullOrEmpty(s)) list.Add(s);
                    }
                }
            }
        }
        return list;
    }

    private static List<Chunk> ExtractChunks(string? json)
    {
        var list = new List<Chunk>();
        if (string.IsNullOrWhiteSpace(json)) return list;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("response", out var response))
        {
            if (response.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in response.EnumerateArray())
                {
                    long id = 0; string path = string.Empty; string? content = null;
                    if (item.TryGetProperty("id", out var idEl) && idEl.TryGetInt64(out var idVal)) id = idVal;
                    if (item.TryGetProperty("path", out var pEl) && pEl.ValueKind == JsonValueKind.String) path = pEl.GetString() ?? string.Empty;
                    if (item.TryGetProperty("chunk_content", out var cEl) && cEl.ValueKind == JsonValueKind.String) content = cEl.GetString();
                    if (id != 0) list.Add(new Chunk(id, path, content));
                }
            }
        }
        return list;
    }

    private static bool ExtractSuccess(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("success", out var ok))
        {
            return ok.ValueKind == JsonValueKind.True;
        }
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.Number)
        {
            return status.GetInt32() == 200;
        }
        return false;
    }
}

