using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.IO;
using Baiss.Infrastructure.Services;
using Baiss.Infrastructure.Jobs;
using Baiss.Application.Interfaces;
using Baiss.Infrastructure.Db;

namespace Baiss.Infrastructure.Configuration;

/// <summary>
/// Configuration extensions for Quartz.NET
/// </summary>
public static class QuartzConfiguration
{
    /// <summary>
    /// Add Quartz.NET services to the DI container using in-memory storage for reliability
    /// </summary>
    public static IServiceCollection AddQuartzServices(this IServiceCollection services)
    {
        // For now, use in-memory scheduler to avoid SQLite provider issues
        // This ensures the scheduler always works, jobs are just not persisted across restarts
        services.AddSingleton<ISchedulerFactory>(serviceProvider =>
        {
            var logger = serviceProvider.GetService<ILogger<QuartzJobSchedulerService>>();
            logger?.LogInformation("Creating in-memory Quartz scheduler for reliability");
            return QuartzJobSchedulerService.CreateInMemorySchedulerFactory();
        });

        // Register job scheduler service
        services.AddSingleton<IJobSchedulerService, QuartzJobSchedulerService>();

        // Register jobs
        services.AddTransient<LogMessageJob>();

        return services;
    }

    /// <summary>
    /// Initialize Quartz database tables in the main database if they don't exist
    /// </summary>
    public static void InitializeQuartzDatabase(ILogger logger)
    {
        try
        {
            // Use the same database as the main application
            var databasePath = DbConstants.DatabasePath;
            
            // The database should already exist from the main application initialization
            if (!File.Exists(databasePath))
            {
                logger.LogWarning("Main database does not exist at: {DatabasePath}. Quartz tables will be created when scheduler starts.", databasePath);
            }
            else
            {
                logger.LogInformation("Using existing main database for Quartz: {DatabasePath}", databasePath);
            }

            // The Quartz.NET ADO.NET job store will automatically create QRTZ_ tables
            // when the scheduler starts for the first time
            logger.LogInformation("Quartz will use main application database: {DatabasePath}", databasePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize Quartz database configuration");
            throw;
        }
    }
}
