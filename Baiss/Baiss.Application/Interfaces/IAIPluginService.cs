using Baiss.Application.Models.AI;

namespace Baiss.Application.Interfaces;

public interface IAIPluginService
{
    Task<AICompletionResponse> ExecutePluginAsync(
        string pluginName,
        string functionName,
        Dictionary<string, object> arguments,
        AIProvider? provider = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamPluginAsync(
        string pluginName,
        string functionName,
        Dictionary<string, object> arguments,
        AIProvider? provider = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<string>> GetAvailablePluginsAsync();

    Task<IEnumerable<string>> GetPluginFunctionsAsync(string pluginName);

    Task<Dictionary<string, object>> GetFunctionSchemaAsync(string pluginName, string functionName);

    Task<bool> RegisterPluginAsync(string pluginName, object plugin);

    Task<bool> UnregisterPluginAsync(string pluginName);

    Task SaveMemoryAsync(
        string key,
        string content,
        string? collection = null,
        Dictionary<string, object>? metadata = null);

    Task<string?> RecallMemoryAsync(
        string query,
        string? collection = null,
        int limit = 1);

    Task<IEnumerable<string>> SearchMemoryAsync(
        string query,
        string? collection = null,
        int limit = 10);
}