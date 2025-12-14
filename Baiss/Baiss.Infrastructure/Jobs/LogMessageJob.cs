using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Threading.Tasks;

namespace Baiss.Infrastructure.Jobs;

/// <summary>
/// Sample job that logs messages
/// </summary>
[DisallowConcurrentExecution]
public class LogMessageJob : IJob
{
    private readonly ILogger<LogMessageJob> _logger;

    public LogMessageJob(ILogger<LogMessageJob> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobKey = context.JobDetail.Key;
        var jobData = context.JobDetail.JobDataMap;
        
        var customMessage = jobData.GetString("message") ?? "Default log message";
        var timestamp = DateTime.UtcNow;

        _logger.LogInformation("Executing LogMessageJob [{JobKey}] at {Timestamp}: {Message}", 
            jobKey, timestamp, customMessage);

        // Write to console for desktop app visibility
        Console.WriteLine($"[{timestamp:yyyy-MM-dd HH:mm:ss}] LogMessageJob [{jobKey}]: {customMessage}");

        // Simulate some work
        await Task.Delay(1000, context.CancellationToken);

        _logger.LogInformation("LogMessageJob [{JobKey}] completed successfully", jobKey);
    }
}
