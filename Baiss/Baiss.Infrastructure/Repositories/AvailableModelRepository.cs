using Baiss.Application.Interfaces;
using Baiss.Domain.Entities;
using Baiss.Infrastructure.Interfaces;
using Dapper;
using Microsoft.Extensions.Logging;

namespace Baiss.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for managing available models from HuggingFace
/// </summary>
public class AvailableModelRepository : IAvailableModelRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<AvailableModelRepository> _logger;

    public AvailableModelRepository(IDbConnectionFactory connectionFactory, ILogger<AvailableModelRepository> logger)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<AvailableModel>> GetAllAsync()
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT Id, IsDownloaded, IsValid, Metadata, UpdatedAt, CreatedAt
                FROM AvailableModels
                ORDER BY Id";

            var models = await connection.QueryAsync<AvailableModel>(sql);

            _logger.LogDebug("Retrieved {Count} available models", models.Count());
            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all available models");
            throw;
        }
    }

    public async Task<AvailableModel?> GetByIdAsync(string id)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT Id, IsDownloaded, IsValid, Metadata, UpdatedAt, CreatedAt
                FROM AvailableModels
                WHERE Id = @Id";

            var model = await connection.QueryFirstOrDefaultAsync<AvailableModel>(sql, new { Id = id });

            return model;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available model by ID {Id}", id);
            throw;
        }
    }

    public async Task<AvailableModel> AddAsync(AvailableModel model)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                INSERT INTO AvailableModels (Id, IsDownloaded, IsValid, Metadata, CreatedAt, UpdatedAt)
                VALUES (@Id, @IsDownloaded, @IsValid, @Metadata, @CreatedAt, @UpdatedAt)";

            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;

            await connection.ExecuteAsync(sql, model);

            _logger.LogInformation("Added available model {Id}", model.Id);
            return model;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding available model {Id}", model.Id);
            throw;
        }
    }

    public async Task<AvailableModel> UpdateAsync(AvailableModel model)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                UPDATE AvailableModels
                SET IsDownloaded = @IsDownloaded,
                    IsValid = @IsValid,
                    Metadata = @Metadata,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";

            model.UpdatedAt = DateTime.UtcNow;

            await connection.ExecuteAsync(sql, model);

            _logger.LogInformation("Updated available model {Id}", model.Id);
            return model;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating available model {Id}", model.Id);
            throw;
        }
    }

    public async Task BulkUpsertAsync(IEnumerable<AvailableModel> models)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                INSERT INTO AvailableModels (Id, IsDownloaded, IsValid, Metadata, CreatedAt, UpdatedAt)
                VALUES (@Id, @IsDownloaded, @IsValid, @Metadata, @CreatedAt, @UpdatedAt)
                ON CONFLICT(Id) DO UPDATE SET
                    IsDownloaded = excluded.IsDownloaded,
                    IsValid = excluded.IsValid,
                    Metadata = excluded.Metadata,
                    UpdatedAt = excluded.UpdatedAt";

            var now = DateTime.UtcNow;
            foreach (var model in models)
            {
                if (model.CreatedAt == default)
                    model.CreatedAt = now;
                model.UpdatedAt = now;
            }

            await connection.ExecuteAsync(sql, models);

            _logger.LogInformation("Bulk upserted {Count} available models", models.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk upserting available models");
            throw;
        }
    }

    public async Task DeleteAsync(string id)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = "DELETE FROM AvailableModels WHERE Id = @Id";

            await connection.ExecuteAsync(sql, new { Id = id });

            _logger.LogInformation("Deleted available model {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting available model {Id}", id);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string id)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = "SELECT COUNT(1) FROM AvailableModels WHERE Id = @Id";

            var count = await connection.ExecuteScalarAsync<int>(sql, new { Id = id });

            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if available model exists {Id}", id);
            throw;
        }
    }

    public async Task DeleteAllAsync()
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = "DELETE FROM AvailableModels";

            await connection.ExecuteAsync(sql);

            _logger.LogInformation("Deleted all available models");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all available models");
            throw;
        }
    }
}
