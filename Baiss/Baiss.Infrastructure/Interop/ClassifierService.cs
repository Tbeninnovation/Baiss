// using Baiss.Application.Interfaces;
// using Baiss.Application.Models;
// using Baiss.Infrastructure.Interop;
// using Microsoft.Extensions.Logging;

// namespace Baiss.Infrastructure.Interop;

// /// <summary>
// /// Implementation of classification service using Python ML models
// /// </summary>
// public class ClassifierService : IClassifierService, IDisposable
// {
//     private readonly PythonBridge _pythonBridge;
//     private readonly string _modelsPath;
//     private readonly ILogger<ClassifierService> _logger;
//     private bool _isInitialized;
//     private bool _disposed;

//     public bool IsReady => _isInitialized && !_disposed;

//     public ClassifierService(PythonBridge pythonBridge, string modelsPath, ILogger<ClassifierService>? logger = null)
//     {
//         _pythonBridge = pythonBridge ?? throw new ArgumentNullException(nameof(pythonBridge));
//         _modelsPath = modelsPath ?? throw new ArgumentNullException(nameof(modelsPath));
//         _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ClassifierService>.Instance;
//     }

//     public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
//     {
//         try
//         {
//             if (_isInitialized)
//                 return true;

//             // Initialize Python bridge
//             var pythonInitialized = await _pythonBridge.InitializeAsync();
//             if (!pythonInitialized)
//             {
//                 _logger.LogError("Failed to initialize Python bridge");
//                 return false;
//             }

//             // Load classification models
//             var loadResult = await LoadModelsAsync();
//             if (!loadResult.IsSuccess)
//             {
//                 _logger.LogError("Failed to load classification models: {ErrorMessage}", loadResult.ErrorMessage);
//                 return false;
//             }

//             _isInitialized = true;
//             _logger.LogInformation("Classifier service initialized successfully");
//             return true;
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex, "Error initializing classifier service: {Message}", ex.Message);
//             return false;
//         }
//     }

//     public async Task<ClassificationResult> ClassifyTextAsync(string text, CancellationToken cancellationToken = default)
//     {
//         if (!IsReady)
//         {
//             return new ClassificationResult
//             {
//                 IsSuccessful = false,
//                 ErrorMessage = "Classifier service is not ready"
//             };
//         }

//         if (string.IsNullOrWhiteSpace(text))
//         {
//             return new ClassificationResult
//             {
//                 IsSuccessful = false,
//                 ErrorMessage = "Text cannot be empty"
//             };
//         }

//         try
//         {
//             var parameters = new Dictionary<string, object>
//             {
//                 ["text"] = text,
//                 ["model_type"] = "text"
//             };

//             var result = await _pythonBridge.CallFunctionAsync("classifier", "classify_text", text);

//             if (!result.IsSuccess)
//             {
//                 return new ClassificationResult
//                 {
//                     IsSuccessful = false,
//                     ErrorMessage = result.ErrorMessage
//                 };
//             }

//             return ParseClassificationResult(result.Result);
//         }
//         catch (Exception ex)
//         {
//             return new ClassificationResult
//             {
//                 IsSuccessful = false,
//                 ErrorMessage = $"Classification error: {ex.Message}"
//             };
//         }
//     }

//     public async Task<ClassificationResult> ClassifyImageAsync(string imagePath, CancellationToken cancellationToken = default)
//     {
//         if (!IsReady)
//         {
//             return new ClassificationResult
//             {
//                 IsSuccessful = false,
//                 ErrorMessage = "Classifier service is not ready"
//             };
//         }

//         if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
//         {
//             return new ClassificationResult
//             {
//                 IsSuccessful = false,
//                 ErrorMessage = "Image file not found"
//             };
//         }

//         try
//         {
//             var result = await _pythonBridge.CallFunctionAsync("classifier", "classify_image", imagePath);

//             if (!result.IsSuccess)
//             {
//                 return new ClassificationResult
//                 {
//                     IsSuccessful = false,
//                     ErrorMessage = result.ErrorMessage
//                 };
//             }

//             return ParseClassificationResult(result.Result);
//         }
//         catch (Exception ex)
//         {
//             return new ClassificationResult
//             {
//                 IsSuccessful = false,
//                 ErrorMessage = $"Image classification error: {ex.Message}"
//             };
//         }
//     }

//     public async Task<ClassificationResult> ClassifyImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
//     {
//         if (!IsReady)
//         {
//             return new ClassificationResult
//             {
//                 IsSuccessful = false,
//                 ErrorMessage = "Classifier service is not ready"
//             };
//         }

//         if (imageBytes == null || imageBytes.Length == 0)
//         {
//             return new ClassificationResult
//             {
//                 IsSuccessful = false,
//                 ErrorMessage = "Image data cannot be empty"
//             };
//         }

//         try
//         {
//             // Create temporary file for the image bytes
//             var tempPath = Path.GetTempFileName();
//             await File.WriteAllBytesAsync(tempPath, imageBytes);

//             try
//             {
//                 var result = await ClassifyImageAsync(tempPath, cancellationToken);
//                 return result;
//             }
//             finally
//             {
//                 // Clean up temporary file
//                 if (File.Exists(tempPath))
//                 {
//                     File.Delete(tempPath);
//                 }
//             }
//         }
//         catch (Exception ex)
//         {
//             return new ClassificationResult
//             {
//                 IsSuccessful = false,
//                 ErrorMessage = $"Image classification error: {ex.Message}"
//             };
//         }
//     }

//     public async Task<bool> TrainModelAsync(string trainingDataPath, CancellationToken cancellationToken = default)
//     {
//         if (!IsReady)
//         {
//             return false;
//         }

//         if (string.IsNullOrWhiteSpace(trainingDataPath) || !File.Exists(trainingDataPath))
//         {
//             return false;
//         }

//         try
//         {
//             var result = await _pythonBridge.CallFunctionAsync("trainer", "train_model", trainingDataPath, _modelsPath);
//             return result.IsSuccess;
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex, "Model training error: {Message}", ex.Message);
//             return false;
//         }
//     }

//     public async Task<IEnumerable<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
//     {
//         if (!IsReady)
//         {
//             return Enumerable.Empty<string>();
//         }

//         try
//         {
//             var result = await _pythonBridge.CallFunctionAsync("classifier", "get_categories");

//             if (!result.IsSuccess)
//             {
//                 return Enumerable.Empty<string>();
//             }

//             // Parse the result as a list of categories
//             // This is a simplified implementation - you might need to parse JSON
//             var categories = result.Result?.Split(',')
//                 .Select(c => c.Trim())
//                 .Where(c => !string.IsNullOrEmpty(c))
//                 .ToList() ?? new List<string>();

//             return categories;
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex, "Error getting categories: {Message}", ex.Message);
//             return Enumerable.Empty<string>();
//         }
//     }

//     /// <summary>
//     /// Loads the classification models
//     /// </summary>
//     private async Task<PythonExecutionResult> LoadModelsAsync()
//     {
//         try
//         {
//             // Check if models directory exists
//             if (!Directory.Exists(_modelsPath))
//             {
//                 return PythonExecutionResult.Failure($"Models directory not found: {_modelsPath}");
//             }

//             var parameters = new Dictionary<string, object>
//             {
//                 ["models_path"] = _modelsPath
//             };

//             var result = await _pythonBridge.CallFunctionAsync("classifier", "load_models", _modelsPath);
//             return result;
//         }
//         catch (Exception ex)
//         {
//             return PythonExecutionResult.Failure($"Error loading models: {ex.Message}");
//         }
//     }

//     /// <summary>
//     /// Parses the classification result from Python
//     /// </summary>
//     private ClassificationResult ParseClassificationResult(string? pythonResult)
//     {
//         try
//         {
//             if (string.IsNullOrEmpty(pythonResult))
//             {
//                 return new ClassificationResult
//                 {
//                     IsSuccessful = false,
//                     ErrorMessage = "Empty result from classifier"
//                 };
//             }

//             // Simple parsing - in a real implementation, you'd parse JSON
//             // Expected format: "category:confidence" or JSON
//             if (pythonResult.Contains(':'))
//             {
//                 var parts = pythonResult.Split(':');
//                 if (parts.Length >= 2)
//                 {
//                     var category = parts[0].Trim();
//                     if (double.TryParse(parts[1].Trim(), out var confidence))
//                     {
//                         return new ClassificationResult
//                         {
//                             Category = category,
//                             Confidence = confidence,
//                             IsSuccessful = true,
//                             AllScores = new Dictionary<string, double> { [category] = confidence }
//                         };
//                     }
//                 }
//             }

//             // Fallback - treat entire result as category with 100% confidence
//             return new ClassificationResult
//             {
//                 Category = pythonResult.Trim(),
//                 Confidence = 1.0,
//                 IsSuccessful = true,
//                 AllScores = new Dictionary<string, double> { [pythonResult.Trim()] = 1.0 }
//             };
//         }
//         catch (Exception ex)
//         {
//             return new ClassificationResult
//             {
//                 IsSuccessful = false,
//                 ErrorMessage = $"Error parsing classification result: {ex.Message}"
//             };
//         }
//     }

//     public async Task DisposeAsync()
//     {
//         if (!_disposed)
//         {
//             try
//             {
//                 if (_isInitialized)
//                 {
//                     // Cleanup models if needed
//                     await _pythonBridge.CallFunctionAsync("classifier", "cleanup");
//                 }
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError(ex, "Error during classifier cleanup: {Message}", ex.Message);
//             }
//             finally
//             {
//                 _disposed = true;
//             }
//         }
//     }

//     public void Dispose()
//     {
//         DisposeAsync().GetAwaiter().GetResult();
//     }
// }
