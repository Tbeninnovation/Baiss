using Quartz;
using Microsoft.Extensions.Logging;
using Baiss.Application.Interfaces;

namespace Baiss.Infrastructure.Jobs;

public class UpdateTreeStructureJob : IJob
{
    private readonly ILogger<UpdateTreeStructureJob> _logger;
    private readonly ITreeStructureService _treeStructureService;
    private readonly ISettingsRepository _settingsRepository;

    public UpdateTreeStructureJob(
        ILogger<UpdateTreeStructureJob> logger,
        ITreeStructureService treeStructureService,
        ISettingsRepository settingsRepository)
    {
        _logger = logger;
        _treeStructureService = treeStructureService;
        _settingsRepository = settingsRepository;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting scheduled tree structure update job");

        try
        {
            var settings = await _settingsRepository.GetAsync();
            if (settings == null)
            {
                _logger.LogWarning("Settings not found, skipping tree structure update");
                return;
            }

            // Note: We check enabled status here as a safeguard,
            // but ideally the job shouldn't be scheduled if disabled.
            if (!settings.TreeStructureScheduleEnabled)
            {
                 _logger.LogInformation("Tree structure update is disabled in settings, skipping");
                 return;
            }

            if (settings.AllowedPaths == null || !settings.AllowedPaths.Any())
            {
                _logger.LogInformation("No allowed paths configured, skipping tree structure update");
                return;
            }

            _logger.LogInformation("Updating tree structure for {Count} paths", settings.AllowedPaths.Count);

            await _treeStructureService.UpdateTreeStructureAsync(
                settings.AllowedPaths,
                settings.AllowedFileExtensions,
                context.CancellationToken);

            _logger.LogInformation("Tree structure update job completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tree structure update job");
        }
    }
}
