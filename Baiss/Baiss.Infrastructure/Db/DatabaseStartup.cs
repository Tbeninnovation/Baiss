using Microsoft.Extensions.Logging;

namespace Baiss.Infrastructure.Db;

/// <summary>
/// Provides database startup and initialization methods for the application.
/// </summary>
public static class DatabaseStartup
{
    /// <summary>
    /// Initializes the database synchronously by running all pending migrations.
    /// This method should be called early in the application startup process.
    /// </summary>
    /// <param name="logger">Optional logger for tracking migration progress.</param>
    /// <exception cref="DatabaseMigrationException">Thrown when migration fails.</exception>
    public static void InitializeDatabase(ILogger<MigrationRunner>? logger = null)
    {
        // Initialize Dapper configuration first
        DapperConfiguration.Initialize();

        var migrationRunner = new MigrationRunner(logger);
        var result = migrationRunner.RunMigrations();

        if (!result.Success)
        {
            throw new DatabaseMigrationException(result.Message);
        }
    }

    /// <summary>
    /// Checks if the database exists and is accessible.
    /// </summary>
    /// <returns>True if the database is accessible, false otherwise.</returns>
    public static bool IsDatabaseAccessible()
    {
        try
        {
            using var connection = new DapperConnectionFactory().CreateConnection();
            return connection.State == System.Data.ConnectionState.Open;
        }
        catch
        {
            return false;
        }
    }
}
