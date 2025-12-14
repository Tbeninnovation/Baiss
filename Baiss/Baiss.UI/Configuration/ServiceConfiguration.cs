using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Baiss.Application.Interfaces;
using Baiss.Application.UseCases;
using Baiss.Infrastructure.Interfaces;
using Baiss.Infrastructure.Repositories;
using Baiss.Infrastructure.Db;
using Baiss.UI.ViewModels;
using System;

namespace Baiss.UI.Configuration;

/// <summary>
/// Configuration des services pour l'injection de dépendances
/// </summary>
public static class ServiceConfiguration
{
    /// <summary>
    /// Configure tous les services nécessaires pour l'application
    /// </summary>
    /// <param name="services">Collection de services</param>
    /// <returns>Collection de services configurée</returns>
    public static IServiceCollection ConfigureServices(this IServiceCollection services)
    {
        // Logging
        var loggerFactory = LoggingConfiguration.CreateLoggerFactory();
        services.AddSingleton(loggerFactory);
        services.AddLogging();

        // Infrastructure - Base de données
        services.AddSingleton<IDbConnectionFactory, DapperConnectionFactory>();

        // Infrastructure - Migration system
        services.AddTransient<MigrationRunner>();

        // Infrastructure - Repositories
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IProviderCredentialRepository, ProviderCredentialRepository>();
        services.AddScoped<IResponseChoiceRepository, ResponseChoiceRepository>();
        services.AddScoped<ISearchPathScoreRepository, SearchPathScoreRepository>();

    // Security / Encryption
    services.AddSingleton<ICredentialEncryptionService, Baiss.Infrastructure.Services.Security.CredentialEncryptionService>();

    // Provider credential service
    services.AddScoped<IProviderCredentialService, Baiss.Infrastructure.Services.ProviderCredentialService>();

    // Semantic Kernel & AI provider registrations
    Baiss.Infrastructure.Services.AI.SemanticKernelConfiguration.AddSemanticKernelServices(services);

    // Infrastructure - Services
    services.AddHttpClient(); // Required for GetLlamaServerService
    services.AddScoped<IGetLlamaServerService, Baiss.Infrastructure.Services.GetLlamaServerService>();

        // Application - Use Cases
        services.AddScoped<SendMessageUseCase>();
        services.AddScoped<GetConversationsUseCase>();
        services.AddScoped<GetConversationByIdUseCase>();
        services.AddScoped<SettingsUseCase>();
        services.AddScoped<GetMessagePathsUseCase>();

        // UI - ViewModels
        // services.AddTransient<ChatViewModel>();

        return services;
    }
}

/// <summary>
/// Exemple d'utilisation dans Program.cs ou App.xaml.cs
/// </summary>
public class ServiceUsageExample
{
    public static void ConfigureInMainApplication()
    {
        // Dans votre Program.cs ou App.xaml.cs :

        // 1. Initialize database first
        try
        {
            var migrationLogger = LoggingConfiguration.CreateLogger<MigrationRunner>();
            DatabaseStartup.InitializeDatabase(migrationLogger);
        }
        catch (DatabaseMigrationException ex)
        {
            var logger = LoggingConfiguration.CreateLogger("DatabaseStartup");
            logger.LogCritical(ex, "Database migration failed: {Message}", ex.Message);
            Environment.Exit(1);
        }

        // 2. Configure services
        var services = new ServiceCollection();
        services.ConfigureServices();

        var serviceProvider = services.BuildServiceProvider();

        // 3. Récupérer vos ViewModels via DI
        // var chatViewModel = serviceProvider.GetRequiredService<ChatViewModel>();
    }
}
