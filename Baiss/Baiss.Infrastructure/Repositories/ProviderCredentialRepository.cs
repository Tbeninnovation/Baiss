using System.Data;
using System.Text.Json;
using Dapper;
using Baiss.Application.Interfaces;
using Baiss.Domain.Entities;
using Baiss.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace Baiss.Infrastructure.Repositories;

public class ProviderCredentialRepository : IProviderCredentialRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<ProviderCredentialRepository> _logger;

    public ProviderCredentialRepository(IDbConnectionFactory connectionFactory, ILogger<ProviderCredentialRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<ProviderCredential?> GetAsync(string provider)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = "SELECT Provider, EncryptedSecret, SecretType, ExtraJson, CreatedAt, UpdatedAt FROM ProviderCredentials WHERE Provider = @Provider";
        var row = await conn.QueryFirstOrDefaultAsync(sql, new { Provider = provider.ToLower() });
        if (row == null) return null;
        return new ProviderCredential
        {
            Provider = row.Provider,
            EncryptedSecret = row.EncryptedSecret,
            SecretType = row.SecretType,
            ExtraJson = row.ExtraJson,
            CreatedAt = DateTime.Parse(row.CreatedAt),
            UpdatedAt = row.UpdatedAt != null ? DateTime.Parse(row.UpdatedAt) : null
        };
    }

    public async Task<List<ProviderCredential>> GetAllAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = "SELECT Provider, EncryptedSecret, SecretType, ExtraJson, CreatedAt, UpdatedAt FROM ProviderCredentials";
        var rows = await conn.QueryAsync(sql);
        return rows.Select(r => new ProviderCredential
        {
            Provider = r.Provider,
            EncryptedSecret = r.EncryptedSecret,
            SecretType = r.SecretType,
            ExtraJson = r.ExtraJson,
            CreatedAt = DateTime.Parse(r.CreatedAt),
            UpdatedAt = r.UpdatedAt != null ? DateTime.Parse(r.UpdatedAt) : null
        }).ToList();
    }

    public async Task UpsertAsync(ProviderCredential credential)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string update = @"UPDATE ProviderCredentials SET EncryptedSecret=@EncryptedSecret, SecretType=@SecretType, ExtraJson=@ExtraJson, UpdatedAt=@UpdatedAt WHERE Provider=@Provider";
        var affected = await conn.ExecuteAsync(update, new
        {
            credential.EncryptedSecret,
            credential.SecretType,
            credential.ExtraJson,
            UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            Provider = credential.Provider.ToLower()
        });
        if (affected == 0)
        {
            const string insert = @"INSERT INTO ProviderCredentials (Provider, EncryptedSecret, SecretType, ExtraJson, CreatedAt, UpdatedAt) VALUES (@Provider, @EncryptedSecret, @SecretType, @ExtraJson, @CreatedAt, @UpdatedAt)";
            await conn.ExecuteAsync(insert, new
            {
                Provider = credential.Provider.ToLower(),
                credential.EncryptedSecret,
                credential.SecretType,
                credential.ExtraJson,
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }
    }

    public Task ImportFromEnvironmentIfEmptyAsync() => Task.CompletedTask; // Responsibility moved to SemanticKernelConfiguration builder
}
