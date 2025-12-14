using Baiss.Application.Interfaces;
using Baiss.Infrastructure.Jobs;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Baiss.Infrastructure.Services;

/// <summary>
/// Service to demonstrate job scheduling capabilities
/// </summary>
public class JobDemoService
{
    private readonly IJobSchedulerService _jobScheduler;
    private readonly ILogger<JobDemoService> _logger;

    public JobDemoService(IJobSchedulerService jobScheduler, ILogger<JobDemoService> logger)
    {
        _jobScheduler = jobScheduler ?? throw new ArgumentNullException(nameof(jobScheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Schedule various demo jobs to showcase functionality
    /// </summary>
    public async Task ScheduleDemoJobsAsync()
    {
        try
        {
            // One-time job - runs once after 5 seconds
            await _jobScheduler.ScheduleOneTimeJobAsync<LogMessageJob>(
                "demo-onetime-welcome",
                TimeSpan.FromSeconds(5),
                new { message = "Welcome to Baiss! This is a one-time job." });

            // One-time job - runs once after 30 seconds
            await _jobScheduler.ScheduleOneTimeJobAsync<LogMessageJob>(
                "demo-onetime-reminder",
                TimeSpan.FromSeconds(30),
                new { message = "This is a reminder from a delayed one-time job!" });

            // Recurring job - every 2 minutes
            await _jobScheduler.ScheduleRecurringJobAsync<LogMessageJob>(
                "demo-recurring-status",
                "0 */2 * * * ?", // Every 2 minutes
                new { message = "System status check - recurring job running every 2 minutes" });

            // Recurring job - every 10 seconds (for demo purposes, normally would be longer)
            await _jobScheduler.ScheduleRecurringJobAsync<LogMessageJob>(
                "demo-recurring-heartbeat",
                "*/10 * * * * ?", // Every 10 seconds
                new { message = "Heartbeat - I'm alive!" });

            // Daily job - runs at 9:00 AM every day
            await _jobScheduler.ScheduleRecurringJobAsync<LogMessageJob>(
                "demo-daily-report",
                "0 0 9 * * ?", // Daily at 9:00 AM
                new { message = "Daily report generation (9:00 AM)" });

            _logger.LogInformation("All demo jobs scheduled successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule demo jobs");
            throw;
        }
    }

    /// <summary>
    /// Cancel a specific demo job
    /// </summary>
    public async Task<bool> CancelDemoJobAsync(string jobKey)
    {
        try
        {
            bool cancelled = await _jobScheduler.CancelJobAsync(jobKey);
            if (cancelled)
            {
                _logger.LogInformation("Successfully cancelled demo job: {JobKey}", jobKey);
            }
            else
            {
                _logger.LogWarning("Demo job not found for cancellation: {JobKey}", jobKey);
            }
            return cancelled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel demo job: {JobKey}", jobKey);
            throw;
        }
    }

    /// <summary>
    /// Cancel all demo jobs
    /// </summary>
    public async Task CancelAllDemoJobsAsync()
    {
        var demoJobs = new[]
        {
            "demo-onetime-welcome",
            "demo-onetime-reminder",
            "demo-recurring-status",
            "demo-recurring-heartbeat",
            "demo-daily-report"
        };

        foreach (var jobKey in demoJobs)
        {
            try
            {
                await _jobScheduler.CancelJobAsync(jobKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cancel demo job: {JobKey}", jobKey);
            }
        }

        _logger.LogInformation("Attempted to cancel all demo jobs");
    }
}
