namespace Baiss.Application.Models.AI;

public class SemanticKernelConfig
{
    public SemanticKernelConfig()
    {
        OpenAI = new OpenAIConfig();
        Anthropic = new AnthropicConfig();
        AzureOpenAI = new AzureOpenAIConfig();
        Databricks = new DatabricksConfig();
        RunPod = new RunPodConfig();
    }

    public AIProvider DefaultProvider { get; set; } = AIProvider.OpenAI;
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 1024;
    public double TopP { get; set; } = 0.9;
    public bool EnableStreaming { get; set; } = true;
    public bool EnableFunctionCalling { get; set; } = true;
    public bool EnablePlugins { get; set; } = true;

    public OpenAIConfig OpenAI { get; set; }
    public AnthropicConfig Anthropic { get; set; }
    public AzureOpenAIConfig AzureOpenAI { get; set; }
    public DatabricksConfig Databricks { get; set; }
    public RunPodConfig RunPod { get; set; }
}

public class OpenAIConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4";
    public string? OrganizationId { get; set; }
}

public class AnthropicConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-3-sonnet-20240229";
}

public class AzureOpenAIConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2024-02-01";
}

public class DatabricksConfig
{
    public string WorkspaceUrl { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string ServingEndpoint { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
}

public class RunPodConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string EndpointId { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.runpod.ai/v2";
    public int TimeoutSeconds { get; set; } = 120;
    public bool EnableStreaming { get; set; } = false;
}