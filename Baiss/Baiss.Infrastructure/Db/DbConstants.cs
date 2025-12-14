using System.Reflection;
using System.IO;

namespace Baiss.Infrastructure.Db;

/// <summary>
/// Database constants and connection string provider for cross-platform SQLite operations.
/// </summary>
public static class DbConstants
{
    /// <summary>
    /// Gets the SQLite database file name.
    /// </summary>
    public const string DatabaseFileName = "baiss.db";
    
    /// <summary>
    /// Gets the Quartz.NET SQLite database file name.
    /// </summary>
    public const string QuartzDatabaseFileName = "baiss_quartz.db";
    
    /// <summary>
    /// Gets the full path to the SQLite database file.
    /// The database file is stored in the same directory as the executable for portability.
    /// </summary>
    public static string DatabasePath => Path.Combine(GetExecutableDirectory(), DatabaseFileName);
    
    /// <summary>
    /// Gets the full path to the Quartz.NET SQLite database file.
    /// The database file is stored in the same directory as the executable for portability.
    /// </summary>
    public static string QuartzDatabasePath => Path.Combine(GetExecutableDirectory(), QuartzDatabaseFileName);
    
    /// <summary>
    /// Gets the SQLite connection string configured for the application.
    /// </summary>
    public static string ConnectionString => $"Data Source={DatabasePath};Cache=Shared;";
    
    /// <summary>
    /// Gets the directory where the executable is located.
    /// This ensures the database is stored alongside the application for portability.
    /// </summary>
    private static string GetExecutableDirectory()
    {
        // For single-file deployments, always use AppContext.BaseDirectory
        // This avoids IL3000 warnings and ensures compatibility across all deployment types
        return AppContext.BaseDirectory;
    }
    
    /// <summary>
    /// Ensures the database directory exists.
    /// </summary>
    public static void EnsureDatabaseDirectoryExists()
    {
        var databaseDirectory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(databaseDirectory) && !Directory.Exists(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }
    }
}
