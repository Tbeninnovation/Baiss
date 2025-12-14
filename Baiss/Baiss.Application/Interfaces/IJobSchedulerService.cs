using System;
using System.Threading.Tasks;
using Quartz;

namespace Baiss.Application.Interfaces;

/// <summary>
/// Interface for scheduling background jobs
/// </summary>
public interface IJobSchedulerService
{
    /// <summary>
    /// Schedule a one-time job
    /// </summary>
    /// <typeparam name="TJob">The job type</typeparam>
    /// <param name="jobKey">Unique job key</param>
    /// <param name="delay">Delay before execution</param>
    /// <param name="jobData">Optional job data</param>
    Task ScheduleOneTimeJobAsync<TJob>(string jobKey, TimeSpan delay, object? jobData = null) where TJob : class, IJob;

    /// <summary>
    /// Schedule a recurring job using CRON expression
    /// </summary>
    /// <typeparam name="TJob">The job type</typeparam>
    /// <param name="jobKey">Unique job key</param>
    /// <param name="cronExpression">CRON expression for scheduling</param>
    /// <param name="jobData">Optional job data</param>
    Task ScheduleRecurringJobAsync<TJob>(string jobKey, string cronExpression, object? jobData = null) where TJob : class, IJob;

    /// <summary>
    /// Cancel a scheduled job
    /// </summary>
    /// <param name="jobKey">Job key to cancel</param>
    Task<bool> CancelJobAsync(string jobKey);

    /// <summary>
    /// Start the scheduler
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stop the scheduler
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Check if scheduler is running
    /// </summary>
    bool IsRunning { get; }
}
