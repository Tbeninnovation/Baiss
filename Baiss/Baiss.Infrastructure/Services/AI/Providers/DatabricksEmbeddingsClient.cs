using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Baiss.Application.Models.AI;
using Baiss.Application.Interfaces;
using Baiss.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Baiss.Infrastructure.Services.AI.Providers;

public class DatabricksEmbeddingsClient
{
    private readonly HttpClient _httpClient;
    private readonly DatabricksConfig _config;
    private readonly ILogger<DatabricksEmbeddingsClient> _logger;
    private readonly ISettingsRepository? _settingsRepository;
    private readonly IModelRepository? _modelRepository;

    public DatabricksEmbeddingsClient(
        HttpClient httpClient,
        DatabricksConfig config,
        ILogger<DatabricksEmbeddingsClient> logger,
        ISettingsRepository? settingsRepository = null,
        IModelRepository? modelRepository = null)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _settingsRepository = settingsRepository;
        _modelRepository = modelRepository;
    }

    public async Task<float[]> EmbedAsync(string text, string endpoint ,  CancellationToken cancellationToken = default)
    {
        var results = await EmbedBatchAsync(new[] { text }, endpoint, cancellationToken);
        return results.Count > 0 ? results[0] : Array.Empty<float>();
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IEnumerable<string> texts, string endpoint, CancellationToken cancellationToken = default)
    {
        var list = texts.ToList();
        if (list.Count == 0) return Array.Empty<float[]>();

        // Get the embedding-specific serving endpoint
        // var servingEndpoint = await GetCurrentEmbeddingServingEndpointAsync();
        var fullUrl = BuildUrl($"/serving-endpoints/{endpoint}/invocations");

        _logger.LogDebug("Using embedding serving endpoint: {Endpoint}", endpoint);

        var request = BuildRequestPayload(list);
        var jsonContent = JsonSerializer.Serialize(request);
        using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, fullUrl) { Content = content };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.Token);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseContent);
        var embeddings = TryExtractEmbeddings(doc.RootElement);
        if (embeddings != null) return embeddings;

        // Fallback: single embedding at root
        var single = TryExtractSingleEmbedding(doc.RootElement);
        if (single != null) return new[] { single };

        throw new InvalidOperationException("Failed to parse embeddings from Databricks response.");
    }

    /// <summary>
    /// Gets the current embedding serving endpoint from the selected embedding model.
    /// Falls back to the config or general model if embedding-specific model is not available.
    /// </summary>
    private async Task<string> GetCurrentEmbeddingServingEndpointAsync()
    {
        // Try to get the selected embedding model's name dynamically
        if (_settingsRepository != null && _modelRepository != null)
        {
            try
            {
                var settings = await _settingsRepository.GetAsync();
                if (settings != null && !string.IsNullOrWhiteSpace(settings.AIEmbeddingModelId))
                {
                    var model = await _modelRepository.GetModelByIdAsync(settings.AIEmbeddingModelId);
                    if (model != null && model.Provider == "databricks")
                    {
                        // The model's name IS the serving endpoint
                        _logger.LogDebug("Using embedding model name as serving endpoint: {Endpoint}", model.Name);
                        return model.Name;
                    }
                }

                // No fallback to chat or legacy model: strict separation enforced
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving embedding model from database, falling back to config");
            }
        }

        // Final fallback to config
        if (string.IsNullOrWhiteSpace(_config.ServingEndpoint))
        {
            throw new InvalidOperationException("No embedding serving endpoint available. Please configure a Databricks embedding model in settings.");
        }

        _logger.LogDebug("Using config serving endpoint for embeddings: {Endpoint}", _config.ServingEndpoint);
        return _config.ServingEndpoint;
    }

    private object BuildRequestPayload(List<string> texts)
    {
        // Common OpenAI-compatible embedding schema uses "input"
        // Some serving endpoints may expect "inputs" or different keys; adjust if needed.
        if (texts.Count == 1)
        {
            return new { input = texts[0] };
        }
        return new { input = texts };
    }

    private string BuildUrl(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(_config.WorkspaceUrl))
        {
            throw new InvalidOperationException("Databricks workspace URL is not configured (empty). Add credentials in settings.");
        }

        var baseUrl = _config.WorkspaceUrl.Trim();
        if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = "https://" + baseUrl;
        }
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var _))
        {
            throw new InvalidOperationException($"Databricks workspace URL '{_config.WorkspaceUrl}' is invalid.");
        }
        if (!relativePath.StartsWith("/")) relativePath = "/" + relativePath;
        return baseUrl.TrimEnd('/') + relativePath;
    }

    private static IReadOnlyList<float[]>? TryExtractEmbeddings(JsonElement root)
    {
        // Expected shapes:
        // { "data": [ { "embedding": [..] }, ... ] }
        // or { "embeddings": [..] }
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            var list = new List<float[]>();
            foreach (var item in data.EnumerateArray())
            {
                var emb = TryExtractSingleEmbedding(item);
                if (emb != null) list.Add(emb);
            }
            return list;
        }
        if (root.TryGetProperty("embeddings", out var embs) && embs.ValueKind == JsonValueKind.Array)
        {
            var list = new List<float[]>();
            foreach (var e in embs.EnumerateArray())
            {
                list.Add(JsonArrayToFloatArray(e));
            }
            return list;
        }
        return null;
    }

    private static float[]? TryExtractSingleEmbedding(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("embedding", out var embedding))
            {
                return JsonArrayToFloatArray(embedding);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            // raw array
            return JsonArrayToFloatArray(element);
        }
        return null;
    }

    private static float[] JsonArrayToFloatArray(JsonElement arr)
    {
        var list = new List<float>();
        foreach (var v in arr.EnumerateArray())
        {
            list.Add((float)v.GetDouble());
        }
        return list.ToArray();
    }
}



