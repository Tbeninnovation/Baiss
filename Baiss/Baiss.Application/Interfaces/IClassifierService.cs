namespace Baiss.Application.Interfaces;

/// <summary>
/// Service interface for classification operations using Python ML models
/// </summary>
public interface IClassifierService
{
    /// <summary>
    /// Initializes the Python environment and loads classification models
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if initialization was successful, false otherwise</returns>
    Task<bool> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Classifies text content using the loaded ML model
    /// </summary>
    /// <param name="text">The text content to classify</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Classification result with confidence score</returns>
    Task<ClassificationResult> ClassifyTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Classifies image content using the loaded ML model
    /// </summary>
    /// <param name="imagePath">Path to the image file to classify</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Classification result with confidence score</returns>
    Task<ClassificationResult> ClassifyImageAsync(string imagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Classifies image content from byte array
    /// </summary>
    /// <param name="imageBytes">The image data as byte array</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Classification result with confidence score</returns>
    Task<ClassificationResult> ClassifyImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trains or retrains the classification model with new data
    /// </summary>
    /// <param name="trainingDataPath">Path to the training data file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if training was successful, false otherwise</returns>
    Task<bool> TrainModelAsync(string trainingDataPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the available classification categories
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of available categories</returns>
    Task<IEnumerable<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the classifier service is ready for operations
    /// </summary>
    /// <returns>True if ready, false otherwise</returns>
    bool IsReady { get; }

    /// <summary>
    /// Disposes of Python resources
    /// </summary>
    Task DisposeAsync();
}

/// <summary>
/// Classification result containing prediction and confidence
/// </summary>
public record ClassificationResult
{
    public string Category { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public Dictionary<string, double> AllScores { get; init; } = new();
    public bool IsSuccessful { get; init; }
    public string? ErrorMessage { get; init; }
}
