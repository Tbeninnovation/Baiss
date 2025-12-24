using System.Threading.Tasks;
using Baiss.Application.DTOs;

namespace Baiss.Application.Interfaces
{
    public interface IExternalApiService
    {
        // Task<string> CallExternalApiAsync(string endpoint, object payload);
        // Task<ChatResponse> SendChatMessageAsync(string message);
        // Task<ContentResponse> SendChatMessageAsync(string message, List<MessageItem>? conversationContext);

        Task<ReleaseInfoResponse?> CheckForUpdatesAsync(string currentVersion = "");


        IAsyncEnumerable<string> SendChatMessageStreamAsync(string message, List<MessageItem>? conversationContext = null, List<string>? filePaths = null);

        IAsyncEnumerable<string> SendChatMessageStreamLlamaCppAsync(string message, List<MessageItem>? conversationContext = null, List<string>? filePaths = null);

        List<PathScoreDto> LastReceivedPaths { get; }
        // Download Model related methods
        Task<List<ModelInfo>> DownloadAvailableModelsAsync();
        Task<StartModelsDownloadResponse> StartModelDownloadAsync(string modelId, string? downloadUrl = null);
        Task<ModelDownloadListResponse> GetModelDownloadListAsync();
        Task<StopModelDownloadResponse> StopModelDownloadAsync(string processId);
        Task<ModelDownloadProgressResponse> GetModelDownloadProgressAsync(string processId);

        Task<ModelsListResponse> GetModelsListExistsAsync();

        Task<bool> DeleteModelAsync(string modelId);

        Task<ModelInfoResponse> GetModelInfoAsync(string modelId);

        Task<bool> StartTreeStructureAsync(List<string> paths, List<string> extensions, string url, CancellationToken cancellationToken = default);

        Task<bool> RemoveTreeStructureAsync(List<string> paths, List<string> extensions);

        Task<bool> CancelTree();

        Task<bool> baiss_update();

        Task<bool> CheckServerStatus();

        /// <summary>
        /// Gets details for an external model
        /// </summary>
        /// <param name="modelId">The model ID to search for</param>
        /// <param name="token">Optional API token</param>
        /// <returns>The model details as a ModelDetailsResponseDto</returns>
        Task<ModelDetailsResponseDto> GetExternalModelDetailsAsync(string modelId, string? token = null);
    }
}
