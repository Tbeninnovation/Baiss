using System.Reflection;
using DbUp;
using DbUp.Engine;
using Microsoft.Extensions.Logging;

namespace Baiss.Infrastructure.Db;

/// <summary>
/// Handles database migration operations using DbUp with embedded SQL scripts.
/// </summary>
public class MigrationRunner
{
    private readonly ILogger<MigrationRunner>? _logger;
    
    public MigrationRunner(ILogger<MigrationRunner>? logger = null)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Runs all pending database migrations.
    /// </summary>
    /// <returns>A result indicating success or failure of the migration process.</returns>
    /// <exception cref="DatabaseMigrationException">Thrown when migration fails.</exception>
    public MigrationResult RunMigrations()
    {
        try
        {
            _logger?.LogInformation("Starting database migration process...");
            
            // Ensure the database directory exists
            DbConstants.EnsureDatabaseDirectoryExists();
            
            _logger?.LogInformation("Database path: {DatabasePath}", DbConstants.DatabasePath);
            
            // Configure DbUp for SQLite with embedded resources
            var upgrader = DeployChanges.To
                .SqliteDatabase(DbConstants.ConnectionString)
                .WithScriptsEmbeddedInAssembly(
                    Assembly.GetExecutingAssembly(),
                    scriptName => scriptName.Contains(".Migrations.") && scriptName.EndsWith(".sql"))
                .WithTransactionPerScript()
                .LogToConsole()
                .Build();
            
            // Check if migration is required
            var scriptsToExecute = upgrader.GetScriptsToExecute();
            if (scriptsToExecute.Count == 0)
            {
                _logger?.LogInformation("Database is up to date. No migrations to run.");
                return new MigrationResult { Success = true, Message = "Database is up to date." };
            }
            
            _logger?.LogInformation("Found {ScriptCount} migration(s) to execute", scriptsToExecute.Count);
            
            // Perform the upgrade
            var result = upgrader.PerformUpgrade();
            
            if (result.Successful)
            {
                _logger?.LogInformation("Database migration completed successfully!");
                return new MigrationResult 
                { 
                    Success = true, 
                    Message = "Database migration completed successfully.",
                    ScriptsExecuted = scriptsToExecute.Count
                };
            }
            else
            {
                var errorMessage = $"Database migration failed: {result.Error?.Message ?? "Unknown error"}";
                _logger?.LogError(result.Error, "Database migration failed");
                throw new DatabaseMigrationException(errorMessage, result.Error);
            }
        }
        catch (DatabaseMigrationException)
        {
            // Re-throw our custom exceptions
            throw;
        }
        catch (Exception ex)
        {
            var errorMessage = $"Unexpected error during database migration: {ex.Message}";
            _logger?.LogError(ex, "Unexpected error during database migration");
            throw new DatabaseMigrationException(errorMessage, ex);
        }
    }
    
    /// <summary>
    /// Checks if the database requires migration.
    /// </summary>
    /// <returns>True if migration is required, false otherwise.</returns>
    public bool IsMigrationRequired()
    {
        try
        {
            var upgrader = DeployChanges.To
                .SqliteDatabase(DbConstants.ConnectionString)
                .WithScriptsEmbeddedInAssembly(
                    Assembly.GetExecutingAssembly(),
                    scriptName => scriptName.Contains(".Migrations.") && scriptName.EndsWith(".sql"))
                .Build();
            
            return upgrader.GetScriptsToExecute().Count > 0;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not determine if migration is required");
            return true; // Assume migration is needed if we can't determine
        }
    }
}

/// <summary>
/// Represents the result of a database migration operation.
/// </summary>
public class MigrationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the migration was successful.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Gets or sets a message describing the result of the migration.
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the number of scripts executed during the migration.
    /// </summary>
    public int ScriptsExecuted { get; set; }
}

/// <summary>
/// Exception thrown when database migration fails.
/// </summary>
public class DatabaseMigrationException : Exception
{
    public DatabaseMigrationException(string message) : base(message)
    {
    }
    
    public DatabaseMigrationException(string message, Exception? innerException) : base(message, innerException)
    {
    }
}
