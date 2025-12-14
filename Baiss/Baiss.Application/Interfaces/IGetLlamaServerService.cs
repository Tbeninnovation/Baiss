namespace Baiss.Application.Interfaces;

/// <summary>
/// Service for downloading and extracting Llama server binaries
/// </summary>
public interface IGetLlamaServerService
{
    /// <summary>
    /// Gets the download URL for the llama-server appropriate for the current platform
    /// </summary>
    /// <returns>The download URL for the llama-server</returns>
    Task<string> GetLlamaServerInfoAsync();

    /// <summary>
    /// Downloads the llama-server archive for the current platform
    /// </summary>
    /// <param name="destinationPath">The directory where the archive should be downloaded</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The path to the downloaded file</returns>
    Task<string> DownloadLlamaServerAsync(string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the downloaded llama-server archive
    /// </summary>
    /// <param name="archivePath">Path to the downloaded zip archive</param>
    /// <param name="extractionPath">Directory where the archive should be extracted</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The path to the extracted directory</returns>
    Task<string> ExtractLlamaServerAsync(string archivePath, string extractionPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads and extracts the llama-server in one operation
    /// </summary>
    /// <param name="destinationBasePath">Base directory where both download and extraction will occur</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The path to the extracted llama-server directory</returns>
    Task<string> DownloadAndExtractLlamaServerAsync(string destinationBasePath, CancellationToken cancellationToken = default);
}
