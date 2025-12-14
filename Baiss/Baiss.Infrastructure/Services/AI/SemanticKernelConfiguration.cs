using Baiss.Application.Interfaces;
using Baiss.Application.Models.AI;
using Baiss.Domain.Entities;
using Baiss.Infrastructure.Services.AI.Providers;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;

namespace Baiss.Infrastructure.Services.AI;

public static class SemanticKernelConfiguration
{
    public static IServiceCollection AddSemanticKernelServices(this IServiceCollection services)
    {
        // Register SemanticKernelConfig built from database credentials (with env fallback)
        services.AddSingleton<SemanticKernelConfig>(sp => BuildFromDatabase(sp));

        services.AddHttpClient<DatabricksConnector>();
        services.AddHttpClient<RunPodAIService>();
        services.AddHttpClient<Providers.DatabricksEmbeddingsClient>();
        services.AddScoped<Providers.DatabricksEmbeddingsClient>(provider =>
        {
            var httpClient = provider.GetRequiredService<HttpClient>();
            var logger = provider.GetRequiredService<ILogger<Providers.DatabricksEmbeddingsClient>>();
            var skConfig = provider.GetRequiredService<SemanticKernelConfig>();
            var settingsRepository = provider.GetService<ISettingsRepository>();
            var modelRepository = provider.GetService<IModelRepository>();
            return new Providers.DatabricksEmbeddingsClient(httpClient, skConfig.Databricks, logger, settingsRepository, modelRepository);
        });

        services.AddScoped<DatabricksConnector>(provider =>
        {
            var httpClient = provider.GetRequiredService<HttpClient>();
            var logger = provider.GetRequiredService<ILogger<DatabricksConnector>>();
            var skConfig = provider.GetRequiredService<SemanticKernelConfig>();
            var settingsRepo = provider.GetService<ISettingsRepository>();
            var modelRepo = provider.GetService<IModelRepository>();
            return new DatabricksConnector(httpClient, skConfig.Databricks, logger, settingsRepo, modelRepo);
        });

        services.AddScoped<IDatabricksAIService, DatabricksAIProvider>();
        services.AddScoped<IRunPodAIService, RunPodAIService>();
        services.AddScoped<IEmbeddingsService, EmbeddingsService>();

        services.AddScoped<ISemanticKernelService, SemanticKernelService>();

        services.AddScoped<IAIStreamingService, AIStreamingService>();

        // Add Universal AI services
        services.AddScoped<IUniversalAIService, Universal.UniversalAIService>();
        services.AddSingleton<Universal.Adapters.ProviderAdapterFactory>();

        return services;
    }

    private static SemanticKernelConfig BuildFromDatabase(IServiceProvider sp)
    {
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("SemanticKernelConfigBuilder");
        // Create an explicit scope so we can resolve scoped dependencies safely while building singleton
        using var scope = sp.CreateScope();
        var repo = scope.ServiceProvider.GetService<IProviderCredentialRepository>();
        var enc = scope.ServiceProvider.GetService<ICredentialEncryptionService>();
        if (repo == null || enc == null)
        {
            logger.LogWarning("Provider credential repository or encryption service not available. Building empty provider configuration (no credentials).");
            return CreateEmptyProviderConfig();
        }
        try
        {
            var creds = repo.GetAllAsync().GetAwaiter().GetResult();
            var cfg = CreateEmptyProviderConfig();

            PopulateConfigFromCredentials(cfg, creds, enc, logger);

            if (creds.Count == 0)
            {
                logger.LogInformation("No provider credentials found in database. Providers will remain inactive until credentials are added via Settings > AI Models.");
            }
            else
            {
                logger.LogInformation("SemanticKernelConfig built from database. Providers loaded: {Providers}", string.Join(',', creds.Select(c => c.Provider)));
            }

            LogProviderWarnings(cfg, logger);

            return cfg;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build SemanticKernelConfig from database. Building empty provider configuration.");
            return CreateEmptyProviderConfig();
        }
    }

    private static SemanticKernelConfig CreateEmptyProviderConfig()
    {
        var cfg = new SemanticKernelConfig();

        ApplyGeneralDefaults(cfg);

        cfg.OpenAI.ApiKey = string.Empty;
        cfg.OpenAI.Model = "gpt-4";
        cfg.OpenAI.OrganizationId = null;

        cfg.Anthropic.ApiKey = string.Empty;
        cfg.Anthropic.Model = "claude-3-sonnet-20240229";

        cfg.AzureOpenAI.ApiKey = string.Empty;
        cfg.AzureOpenAI.Endpoint = string.Empty;
        cfg.AzureOpenAI.DeploymentName = string.Empty;
        cfg.AzureOpenAI.ApiVersion = "2024-02-01";

        cfg.Databricks.WorkspaceUrl = string.Empty;
        cfg.Databricks.Token = string.Empty;
        cfg.Databricks.ServingEndpoint = string.Empty;
        cfg.Databricks.ModelName = string.Empty;
        cfg.Databricks.MaxRetries = ParseInt("DATABRICKS_MAX_RETRIES", 3);
        cfg.Databricks.Timeout = TimeSpan.FromSeconds(ParseInt("DATABRICKS_TIMEOUT_SECONDS", 120));

        return cfg;
    }

    private static void ApplyGeneralDefaults(SemanticKernelConfig cfg)
    {
        cfg.DefaultProvider = ParseDefaultProvider();
        cfg.Temperature = ParseDouble("SK_TEMPERATURE", 0.7);
        cfg.MaxTokens = ParseInt("SK_MAX_TOKENS", 1024);
        cfg.TopP = ParseDouble("SK_TOP_P", 0.9);
        cfg.EnableStreaming = ParseBool("SK_ENABLE_STREAMING", true);
        cfg.EnableFunctionCalling = ParseBool("SK_ENABLE_FUNCTION_CALLING", true);
        cfg.EnablePlugins = ParseBool("SK_ENABLE_PLUGINS", true);
    }

    private static void PopulateConfigFromCredentials(
        SemanticKernelConfig cfg,
    IReadOnlyCollection<ProviderCredential> creds,
        ICredentialEncryptionService enc,
        ILogger logger)
    {
        ApplyGeneralDefaults(cfg);

        string? openaiKey = null; string? openaiOrg = null; string? openaiModel = null;
        string? anthropicKey = null; string? anthropicModel = null;
        string? azureKey = null; string? azureEndpoint = null; string? azureDeployment = null; string? azureApiVersion = null;
        string? dbToken = null; string? dbWorkspace = null; string? dbEndpoint = null; string? dbModelName = null;

        foreach (var credential in creds)
        {
            var secret = enc.Decrypt(credential.EncryptedSecret);
            var extra = string.IsNullOrWhiteSpace(credential.ExtraJson)
                ? new Dictionary<string, string?>()
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(credential.ExtraJson!) ?? new();

            switch (credential.Provider)
            {
                case "openai":
                    openaiKey = secret;
                    extra.TryGetValue("organizationId", out openaiOrg);
                    extra.TryGetValue("model", out openaiModel);
                    break;
                case "anthropic":
                    anthropicKey = secret;
                    extra.TryGetValue("model", out anthropicModel);
                    break;
                case "azure":
                    azureKey = secret;
                    extra.TryGetValue("endpoint", out azureEndpoint);
                    extra.TryGetValue("deploymentName", out azureDeployment);
                    extra.TryGetValue("apiVersion", out azureApiVersion);
                    break;
                case "databricks":
                    dbToken = secret;
                    extra.TryGetValue("workspaceUrl", out dbWorkspace);
                    extra.TryGetValue("servingEndpoint", out dbEndpoint);
                    extra.TryGetValue("modelName", out dbModelName);
                    break;
            }
        }

        cfg.OpenAI.ApiKey = openaiKey ?? string.Empty;
        cfg.OpenAI.Model = openaiModel ?? "gpt-4";
        cfg.OpenAI.OrganizationId = openaiOrg;

        cfg.Anthropic.ApiKey = anthropicKey ?? string.Empty;
        cfg.Anthropic.Model = anthropicModel ?? "claude-3-sonnet-20240229";

        cfg.AzureOpenAI.ApiKey = azureKey ?? string.Empty;
        cfg.AzureOpenAI.Endpoint = azureEndpoint ?? string.Empty;
        cfg.AzureOpenAI.DeploymentName = azureDeployment ?? string.Empty;
        cfg.AzureOpenAI.ApiVersion = azureApiVersion ?? "2024-02-01";

        cfg.Databricks.WorkspaceUrl = dbWorkspace ?? string.Empty;
        cfg.Databricks.Token = dbToken ?? string.Empty;
        cfg.Databricks.ServingEndpoint = dbEndpoint ?? string.Empty;
        cfg.Databricks.ModelName = dbModelName ?? string.Empty;
        cfg.Databricks.MaxRetries = ParseInt("DATABRICKS_MAX_RETRIES", 3);
    cfg.Databricks.Timeout = TimeSpan.FromSeconds(ParseInt("DATABRICKS_TIMEOUT_SECONDS", 120));
    }

    private static void LogProviderWarnings(SemanticKernelConfig cfg, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(cfg.OpenAI.ApiKey)) logger.LogWarning("OpenAI credentials missing (no API key in DB).");
        if (string.IsNullOrWhiteSpace(cfg.Anthropic.ApiKey)) logger.LogWarning("Anthropic credentials missing (no API key in DB).");
        if (string.IsNullOrWhiteSpace(cfg.AzureOpenAI.ApiKey)) logger.LogWarning("Azure OpenAI credentials missing (no API key in DB).");
        if (string.IsNullOrWhiteSpace(cfg.Databricks.Token)) logger.LogWarning("Databricks credentials missing (no token in DB).");
    }

    public static async Task RefreshSemanticKernelConfigAsync(
        SemanticKernelConfig cfg,
        IProviderCredentialRepository repo,
        ICredentialEncryptionService enc,
        ILogger logger)
    {
        if (repo == null || enc == null)
        {
            logger.LogWarning("Cannot refresh SemanticKernelConfig because repository or encryption service is unavailable.");
            return;
        }

        try
        {
            var creds = await repo.GetAllAsync();
            PopulateConfigFromCredentials(cfg, creds, enc, logger);
            LogProviderWarnings(cfg, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh SemanticKernelConfig from database.");
        }
    }

    private static AIProvider ParseDefaultProvider()
    {
        var providerStr = Environment.GetEnvironmentVariable("SK_DEFAULT_PROVIDER")?.ToLower() ?? "openai";

        return providerStr switch
        {
            "openai" => AIProvider.OpenAI,
            "anthropic" => AIProvider.Anthropic,
            "azure" or "azureopenai" => AIProvider.AzureOpenAI,
            "databricks" => AIProvider.Databricks,
            _ => AIProvider.OpenAI
        };
    }

    private static double ParseDouble(string envVar, double defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        return double.TryParse(value, out var result) ? result : defaultValue;
    }

    private static int ParseInt(string envVar, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    private static bool ParseBool(string envVar, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    public static void ValidateConfiguration(SemanticKernelConfig config, ILogger logger)
    {
        var errors = new List<string>();

        if (config.DefaultProvider == AIProvider.OpenAI && string.IsNullOrEmpty(config.OpenAI.ApiKey))
        {
            errors.Add("OpenAI API key is required when using OpenAI as default provider");
        }

        if (config.DefaultProvider == AIProvider.Anthropic && string.IsNullOrEmpty(config.Anthropic.ApiKey))
        {
            errors.Add("Anthropic API key is required when using Anthropic as default provider");
        }

        if (config.DefaultProvider == AIProvider.AzureOpenAI)
        {
            if (string.IsNullOrEmpty(config.AzureOpenAI.ApiKey))
                errors.Add("Azure OpenAI API key is required");
            if (string.IsNullOrEmpty(config.AzureOpenAI.Endpoint))
                errors.Add("Azure OpenAI endpoint is required");
            if (string.IsNullOrEmpty(config.AzureOpenAI.DeploymentName))
                errors.Add("Azure OpenAI deployment name is required");
        }

        if (config.DefaultProvider == AIProvider.Databricks)
        {
            if (string.IsNullOrEmpty(config.Databricks.WorkspaceUrl))
                errors.Add("Databricks workspace URL is required");
            if (string.IsNullOrEmpty(config.Databricks.Token))
                errors.Add("Databricks token is required");
            if (string.IsNullOrEmpty(config.Databricks.ServingEndpoint))
                errors.Add("Databricks serving endpoint is required");
        }

        if (config.Temperature < 0 || config.Temperature > 2)
        {
            errors.Add("Temperature must be between 0 and 2");
        }

        if (config.MaxTokens < 1 || config.MaxTokens > 32000)
        {
            errors.Add("MaxTokens must be between 1 and 32000");
        }

        if (config.TopP < 0 || config.TopP > 1)
        {
            errors.Add("TopP must be between 0 and 1");
        }

        if (errors.Any())
        {
            logger.LogWarning("Semantic Kernel configuration issues detected:\n{Issues}", string.Join("\n", errors));
        }
        else
        {
            logger.LogInformation("Semantic Kernel configuration validated successfully. Default provider: {Provider}", config.DefaultProvider);
        }
    }
}
