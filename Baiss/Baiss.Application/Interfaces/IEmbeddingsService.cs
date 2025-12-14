using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baiss.Domain.Entities;

namespace Baiss.Application.Interfaces;

public interface IEmbeddingsService
{
    // Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
    // Task<float[]> EmbedAsync(string text , string modelName ,  CancellationToken cancellationToken = default);

    // Task<IReadOnlyList<float[]>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Orchestrates fetching missing chunks via Python (files.py) and fills embeddings using the configured provider.
    /// Returns total number of embeddings attempted (filled or updated) as a simple progress metric.
    /// </summary>
    // Task<int> BackfillMissingEmbeddingsAsync(CancellationToken cancellationToken = default);


    Task<List<float>> EmbeddingModelsAi(string text, Model model, string aiModelType, CancellationToken ct);




}

