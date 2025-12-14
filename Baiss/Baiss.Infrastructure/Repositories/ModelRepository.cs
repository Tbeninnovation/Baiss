using Baiss.Application.Interfaces;
using Baiss.Domain.Entities;
using Baiss.Infrastructure.Interfaces;
using Dapper;
using Microsoft.Extensions.Logging;

namespace Baiss.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for managing AI models
/// </summary>
public class ModelRepository : IModelRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<ModelRepository> _logger;

    public ModelRepository(IDbConnectionFactory connectionFactory, ILogger<ModelRepository> logger)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<Model>> GetAllModelsAsync()
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT Id, Name, Type, Provider, Description, Purpose, IsActive, CreatedAt, UpdatedAt, LocalPath
                FROM Models
                WHERE IsActive = 1
                ORDER BY Type, Provider, Name";

            var models = await connection.QueryAsync<Model>(sql);

            _logger.LogDebug("Retrieved {Count} active models", models.Count());
            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all models");
            throw;
        }
    }

    public async Task<IEnumerable<Model>> GetModelsByTypeAsync(string type)
    {
        try
        {
            // ! test
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT Id, Name, Type, Provider, Description, Purpose, IsActive, CreatedAt, UpdatedAt, LocalPath
                FROM Models
                WHERE Type = @Type AND IsActive = 1
                ORDER BY Provider, Name";

            var models = await connection.QueryAsync<Model>(sql, new { Type = type });

            _logger.LogDebug("Retrieved {Count} models of type {Type}", models.Count(), type);
            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving models by type {Type}", type);
            throw;
        }
    }

    public async Task<IEnumerable<Model>> GetModelsByProviderAsync(string provider)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT Id, Name, Type, Provider, Description, Purpose, IsActive, CreatedAt, UpdatedAt, LocalPath
                FROM Models
                WHERE Provider = @Provider AND IsActive = 1
                ORDER BY Type, Name";

            var models = await connection.QueryAsync<Model>(sql, new { Provider = provider });

            _logger.LogDebug("Retrieved {Count} models for provider {Provider}", models.Count(), provider);
            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving models by provider {Provider}", provider);
            throw;
        }
    }

    public async Task<Model?> GetModelByIdAsync(string id)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT Id, Name, Type, Provider, Description, Purpose, IsActive, CreatedAt, UpdatedAt, LocalPath
                FROM Models
                WHERE Id = @Id";

            var model = await connection.QuerySingleOrDefaultAsync<Model>(sql, new { Id = id });

            if (model != null)
                _logger.LogDebug("Retrieved model {Id}", id);
            else
                _logger.LogWarning("Model {Id} not found", id);

            return model;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving model {Id}", id);
            throw;
        }
    }

    public async Task<Model> AddModelAsync(Model model)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                INSERT INTO Models (Id, Name, Type, Provider, Description, Purpose, IsActive, CreatedAt, UpdatedAt, LocalPath)
                VALUES (@Id, @Name, @Type, @Provider, @Description, @Purpose, @IsActive, @CreatedAt, @UpdatedAt, @LocalPath)";

            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;

            await connection.ExecuteAsync(sql, model);

            _logger.LogInformation("Added new model {Id}: {Name}", model.Id, model.Name);
            return model;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding model {Id}", model.Id);
            throw;
        }
    }

    public async Task<Model> UpdateModelAsync(Model model)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                UPDATE Models
                SET Name = @Name, Type = @Type, Provider = @Provider,
                    Description = @Description, Purpose = @Purpose, IsActive = @IsActive, UpdatedAt = @UpdatedAt
                WHERE Id = @Id";

            model.UpdatedAt = DateTime.UtcNow;

            var rowsAffected = await connection.ExecuteAsync(sql, model);

            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"Model {model.Id} not found for update");
            }

            _logger.LogInformation("Updated model {Id}: {Name}", model.Id, model.Name);
            return model;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating model {Id}", model.Id);
            throw;
        }
    }

    public async Task<IEnumerable<Model>> GetModelsByPurposeAsync(string purpose)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT Id, Name, Type, Provider, Description, Purpose, IsActive, CreatedAt, UpdatedAt , LocalPath
                FROM Models
                WHERE Purpose = @Purpose AND IsActive = 1
                ORDER BY Provider, Name";

            var models = await connection.QueryAsync<Model>(sql, new { Purpose = purpose });

            _logger.LogDebug("Retrieved {Count} models with purpose {Purpose}", models.Count(), purpose);
            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving models by purpose {Purpose}", purpose);
            throw;
        }
    }

    public async Task<IEnumerable<Model>> GetModelsByProviderAndPurposeAsync(string provider, string purpose)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT Id, Name, Type, Provider, Description, Purpose, IsActive, CreatedAt, UpdatedAt, LocalPath
                FROM Models
                WHERE Provider = @Provider AND Purpose = @Purpose AND IsActive = 1
                ORDER BY Name";

            var models = await connection.QueryAsync<Model>(sql, new { Provider = provider, Purpose = purpose });

            _logger.LogDebug("Retrieved {Count} {Purpose} models for provider {Provider}", models.Count(), purpose, provider);
            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {Purpose} models by provider {Provider}", purpose, provider);
            throw;
        }
    }

    public async Task DeleteModelAsync(string id)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = "DELETE FROM Models WHERE Id = @Id";

            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });

            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"Model {id} not found for deletion");
            }

            _logger.LogInformation("Deleted model {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model {Id}", id);
            throw;
        }
    }

    public async Task<bool> ModelExistsAsync(string id)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = "SELECT COUNT(1) FROM Models WHERE Id = @Id";

            var count = await connection.QuerySingleAsync<int>(sql, new { Id = id });

            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if model exists {Id}", id);
            throw;
        }
    }

    public async Task<string> GetPathByModelIdAsync(string id)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = "SELECT LocalPath FROM Models WHERE Id = @Id";

            var path = await connection.QuerySingleOrDefaultAsync<string>(sql, new { Id = id });

            if (path == null)
            {
                _logger.LogWarning("Model path not found for Id {Id}", id);
                throw new InvalidOperationException($"Model path not found for Id {id}");
            }

            _logger.LogDebug("Retrieved model path for Id {Id}", id);
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving model path for Id {Id}", id);
            throw;
        }
    }
}
