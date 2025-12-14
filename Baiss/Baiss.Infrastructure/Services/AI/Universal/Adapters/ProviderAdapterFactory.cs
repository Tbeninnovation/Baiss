using Baiss.Application.Models.AI;

namespace Baiss.Infrastructure.Services.AI.Universal.Adapters;

public class ProviderAdapterFactory
{
    private readonly Dictionary<AIProvider, Func<IProviderAdapter>> _adapterFactories;

    public ProviderAdapterFactory()
    {
        _adapterFactories = new Dictionary<AIProvider, Func<IProviderAdapter>>
        {
            [AIProvider.OpenAI] = () => new OpenAIUniversalAdapter(),
            [AIProvider.Databricks] = () => new DatabricksUniversalAdapter(),
            [AIProvider.Anthropic] = () => new AnthropicUniversalAdapter(),
            [AIProvider.AzureOpenAI] = () => new AzureOpenAIUniversalAdapter()
        };
    }

    public IProviderAdapter? GetAdapter(AIProvider provider)
    {
        if (_adapterFactories.TryGetValue(provider, out var factory))
        {
            return factory();
        }

        return null;
    }

    public IProviderAdapter? GetAdapter(string providerName)
    {
        if (Enum.TryParse<AIProvider>(providerName, true, out var provider))
        {
            return GetAdapter(provider);
        }

        // Handle string-based provider names
        var normalizedName = providerName.ToLower();
        return normalizedName switch
        {
            "openai" => new OpenAIUniversalAdapter(),
            "databricks" => new DatabricksUniversalAdapter(),
            "anthropic" => new AnthropicUniversalAdapter(),
            "azure" or "azureopenai" => new AzureOpenAIUniversalAdapter(),
            _ => null
        };
    }

    public IEnumerable<string> GetSupportedProviders()
    {
        return _adapterFactories.Keys.Select(p => p.ToString().ToLower());
    }

    public bool IsProviderSupported(string providerName)
    {
        return GetAdapter(providerName) != null;
    }
}
