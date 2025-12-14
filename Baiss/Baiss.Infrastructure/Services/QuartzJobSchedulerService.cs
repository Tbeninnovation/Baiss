using Baiss.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;

namespace Baiss.Infrastructure.Services;

/// <summary>
/// Quartz.NET implementation of job scheduler service
/// </summary>
public class QuartzJobSchedulerService : IJobSchedulerService
{
    private readonly ILogger<QuartzJobSchedulerService> _logger;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IServiceProvider _serviceProvider;
    private IScheduler? _scheduler;

    public QuartzJobSchedulerService(ILogger<QuartzJobSchedulerService> logger, ISchedulerFactory schedulerFactory, IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public bool IsRunning => _scheduler?.IsStarted == true && !_scheduler.IsShutdown;

    public async Task StartAsync()
    {
        try
        {
            _logger.LogInformation("Starting Quartz Scheduler...");
            
            if (_scheduler == null)
            {
                try
                {
                    // Try to get scheduler from the factory (might be SQLite or fallback)
                    _scheduler = await _schedulerFactory.GetScheduler();
                }
                catch (Exception ex)
                {
                    // If factory fails, create an in-memory scheduler directly
                    _logger.LogWarning(ex, "Failed to create scheduler from factory, creating in-memory scheduler");
                    var inMemoryFactory = CreateInMemorySchedulerFactory();
                    _scheduler = await inMemoryFactory.GetScheduler();
                }
                
                // Set up job factory to use DI container
                _scheduler.JobFactory = new MicrosoftDependencyInjectionJobFactory(_serviceProvider);
            }

            if (!_scheduler.IsStarted)
            {
                await _scheduler.Start();
                _logger.LogInformation("Quartz Scheduler started successfully");
            }
            else
            {
                _logger.LogInformation("Quartz Scheduler is already running");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Quartz Scheduler");
            throw;
        }
    }

    public async Task StopAsync()
    {
        try
        {
            if (_scheduler != null && _scheduler.IsStarted && !_scheduler.IsShutdown)
            {
                _logger.LogInformation("Stopping Quartz Scheduler...");
                await _scheduler.Shutdown(waitForJobsToComplete: true);
                _logger.LogInformation("Quartz Scheduler stopped successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop Quartz Scheduler");
            throw;
        }
    }

    public async Task ScheduleOneTimeJobAsync<TJob>(string jobKey, TimeSpan delay, object? jobData = null) where TJob : class, IJob
    {
        if (_scheduler == null)
        {
            throw new InvalidOperationException("Scheduler not initialized. Call StartAsync first.");
        }

        try
        {
            var job = JobBuilder.Create<TJob>()
                .WithIdentity(jobKey, "OneTimeJobs")
                .Build();

            // Add job data if provided
            if (jobData != null)
            {
                AddJobDataToJobDetail(job, jobData);
            }

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"{jobKey}-trigger", "OneTimeJobs")
                .StartAt(DateTimeOffset.UtcNow.Add(delay))
                .Build();

            await _scheduler.ScheduleJob(job, trigger);
            
            _logger.LogInformation("Scheduled one-time job [{JobKey}] to run in {Delay}", jobKey, delay);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule one-time job [{JobKey}]", jobKey);
            throw;
        }
    }

    public async Task ScheduleRecurringJobAsync<TJob>(string jobKey, string cronExpression, object? jobData = null) where TJob : class, IJob
    {
        if (_scheduler == null)
        {
            throw new InvalidOperationException("Scheduler not initialized. Call StartAsync first.");
        }

        try
        {
            var job = JobBuilder.Create<TJob>()
                .WithIdentity(jobKey, "RecurringJobs")
                .Build();

            // Add job data if provided
            if (jobData != null)
            {
                AddJobDataToJobDetail(job, jobData);
            }

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"{jobKey}-trigger", "RecurringJobs")
                .WithCronSchedule(cronExpression)
                .Build();

            await _scheduler.ScheduleJob(job, trigger);
            
            _logger.LogInformation("Scheduled recurring job [{JobKey}] with CRON expression: {CronExpression}", jobKey, cronExpression);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule recurring job [{JobKey}] with CRON expression: {CronExpression}", jobKey, cronExpression);
            throw;
        }
    }

    public async Task<bool> CancelJobAsync(string jobKey)
    {
        if (_scheduler == null)
        {
            throw new InvalidOperationException("Scheduler not initialized. Call StartAsync first.");
        }

        try
        {
            // Try both job groups
            var oneTimeJobKey = new JobKey(jobKey, "OneTimeJobs");
            var recurringJobKey = new JobKey(jobKey, "RecurringJobs");

            bool cancelledOneTime = await _scheduler.DeleteJob(oneTimeJobKey);
            bool cancelledRecurring = await _scheduler.DeleteJob(recurringJobKey);

            bool result = cancelledOneTime || cancelledRecurring;
            
            if (result)
            {
                _logger.LogInformation("Successfully cancelled job [{JobKey}]", jobKey);
            }
            else
            {
                _logger.LogWarning("Job [{JobKey}] not found for cancellation", jobKey);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel job [{JobKey}]", jobKey);
            throw;
        }
    }

    private static void AddJobDataToJobDetail(IJobDetail job, object jobData)
    {
        if (jobData is System.Collections.IDictionary dictionary)
        {
            foreach (System.Collections.DictionaryEntry entry in dictionary)
            {
                job.JobDataMap.Put(entry.Key?.ToString() ?? "", entry.Value);
            }
        }
        else
        {
            // For simple objects, use reflection to add properties
            var properties = jobData.GetType().GetProperties();
            foreach (var property in properties)
            {
                if (property.CanRead)
                {
                    var value = property.GetValue(jobData);
                    job.JobDataMap.Put(property.Name, value);
                }
            }
        }
    }

    /// <summary>
    /// Creates a configured Quartz scheduler factory for SQLite persistence using Microsoft.Data.Sqlite
    /// </summary>
    public static ISchedulerFactory CreateSchedulerFactory(string databasePath)
    {
        var properties = new NameValueCollection
        {
            // Scheduler configuration
            ["quartz.scheduler.instanceName"] = "BaissScheduler",
            ["quartz.scheduler.instanceId"] = "AUTO",
            
            // Thread pool configuration
            ["quartz.threadPool.type"] = "Quartz.Simpl.DefaultThreadPool, Quartz",
            ["quartz.threadPool.threadCount"] = "5",
            ["quartz.threadPool.threadPriority"] = "Normal",
            
            // Job store configuration for SQLite using System.Data.SQLite (Quartz supported)
            ["quartz.jobStore.type"] = "Quartz.Impl.AdoJobStore.JobStoreTX, Quartz",
            ["quartz.jobStore.driverDelegateType"] = "Quartz.Impl.AdoJobStore.SQLiteDelegate, Quartz",
            ["quartz.jobStore.dataSource"] = "default",
            ["quartz.jobStore.tablePrefix"] = "QRTZ_",
            ["quartz.jobStore.useProperties"] = "true",
            
            // System.Data.SQLite data source configuration (supported by Quartz)
            ["quartz.dataSource.default.connectionString"] = $"Data Source={databasePath};Version=3;",
            ["quartz.dataSource.default.provider"] = "SQLite-10"
            
            // Remove JSON serialization - use default binary serialization
        };

        return new StdSchedulerFactory(properties);
    }

    /// <summary>
    /// Creates a fallback in-memory scheduler if SQLite persistence fails
    /// </summary>
    public static ISchedulerFactory CreateInMemorySchedulerFactory()
    {
        var properties = new NameValueCollection
        {
            // Scheduler configuration
            ["quartz.scheduler.instanceName"] = "BaissScheduler",
            ["quartz.scheduler.instanceId"] = "AUTO",
            
            // Thread pool configuration
            ["quartz.threadPool.type"] = "Quartz.Simpl.DefaultThreadPool, Quartz",
            ["quartz.threadPool.threadCount"] = "5",
            ["quartz.threadPool.threadPriority"] = "Normal",
            
            // In-memory job store (no persistence)
            ["quartz.jobStore.type"] = "Quartz.Simpl.RAMJobStore, Quartz",
            
            // JSON serialization (now with proper package)
            ["quartz.serializer.type"] = "json"
        };

        return new StdSchedulerFactory(properties);
    }
}

/// <summary>
/// Custom job factory that uses Microsoft DI container
/// </summary>
public class MicrosoftDependencyInjectionJobFactory : IJobFactory
{
    private readonly IServiceProvider _serviceProvider;

    public MicrosoftDependencyInjectionJobFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        var jobType = bundle.JobDetail.JobType;
        
        try
        {
            return (IJob)(_serviceProvider.GetService(jobType) ?? Activator.CreateInstance(jobType)!);
        }
        catch (Exception ex)
        {
            throw new SchedulerException($"Failed to create job instance of type {jobType.Name}", ex);
        }
    }

    public void ReturnJob(IJob job)
    {
        // DI container handles cleanup
        if (job is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
