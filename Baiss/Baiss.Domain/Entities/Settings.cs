namespace Baiss.Domain.Entities;

public enum PerformanceLevel
{
    Small = 0,
    Medium = 1,
    High = 2
}

public class Settings
{
    public string Id { get; set; } = "app-settings-global";

    public PerformanceLevel Performance { get; set; } = PerformanceLevel.Small;


    // ? READ ACCESS
    public bool AllowFileReading { get; set; } = false;
    public List<string> AllowedPaths { get; set; } = new List<string>();


    // ? WRITE PERMISSION
    public bool AllowUpdateCreatedFiles { get; set; } = false;
    public bool AllowCreateNewFiles { get; set; } = false;

    //  Path to save new files created by ai
    public string NewFilesSavePath { get; set; } = string.Empty;

    public List<string> AllowedApplications { get; set; } = new List<string>();

    public List<string> AllowedFileExtensions { get; set; } = new List<string>
    {
        "docx", "xls", "xlsx", "pdf", "txt", "csv", "md"
    };
    public bool EnableAutoUpdate { get; set; } = true;

    public string AppVersion { get; set; } = "1.0.0";

    // AI Model Configuration
    /// <summary>
    /// Type of AI model: "local" or "hosted"
    /// </summary>
    public string AIModelType { get; set; } = ModelTypes.Local;

    /// <summary>
    /// High-level provider scope selection for UI radio buttons (e.g., local | hosted | databricks)
    /// Distinct from AIModelType which remains local/hosted for broader logic.
    /// </summary>
    public string AIModelProviderScope { get; set; } = "local"; // local | hosted | databricks

    public string HuggingfaceApiKey { get; set; } = string.Empty;

    public string TreeStructureSchedule { get; set; } = "0 0 0 * * ?";
    public bool TreeStructureScheduleEnabled { get; set; } = false;

    /// <summary>
    /// Selected AI chat model identifier
    /// </summary>
    public string AIChatModelId { get; set; } = string.Empty;

    /// <summary>
    /// Selected AI embedding model identifier
    /// </summary>
    public string AIEmbeddingModelId { get; set; } = string.Empty;

    // Legacy AIModelId removed (migration 012). Use AIChatModelId / AIEmbeddingModelId only.

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Flag to track if the "You're almost set" modal has been shown
    /// </summary>
    public bool HasShownWelcomeModal { get; set; } = false;

}

