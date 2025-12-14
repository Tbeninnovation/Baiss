using System.Data;
using Dapper;
using Baiss.Domain.Entities;
using Baiss.Infrastructure.Interfaces;
using Baiss.Application.Interfaces;

namespace Baiss.Infrastructure.Repositories;

public class ResponseChoiceRepository : IResponseChoiceRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ResponseChoiceRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<ResponseChoice> CreateAsync(ResponseChoice responseChoice)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO ResponseChoices (Id, MessageId, CreatedAt)
            VALUES (@Id, @MessageId, @CreatedAt)";

        var parameters = new
        {
            Id = responseChoice.Id.ToString(),
            MessageId = responseChoice.MessageId.ToString(),
            CreatedAt = responseChoice.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        };

        await connection.ExecuteAsync(sql, parameters);
        return responseChoice;
    }

    public async Task<ResponseChoice?> GetByIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT Id, MessageId, CreatedAt
            FROM ResponseChoices
            WHERE Id = @Id";

        var result = await connection.QueryFirstOrDefaultAsync(sql, new { Id = id.ToString() });

        if (result == null) return null;

        return new ResponseChoice
        {
            Id = Guid.Parse(result.Id),
            MessageId = Guid.Parse(result.MessageId),
            CreatedAt = DateTime.Parse(result.CreatedAt)
        };
    }

    public async Task<ResponseChoice?> GetByMessageIdAsync(Guid messageId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT Id, MessageId, CreatedAt
            FROM ResponseChoices
            WHERE MessageId = @MessageId";

        var result = await connection.QueryFirstOrDefaultAsync(sql, new { MessageId = messageId.ToString() });

        if (result == null) return null;

        return new ResponseChoice
        {
            Id = Guid.Parse(result.Id),
            MessageId = Guid.Parse(result.MessageId),
            CreatedAt = DateTime.Parse(result.CreatedAt)
        };
    }

    public async Task<IEnumerable<ResponseChoice>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT Id, MessageId, CreatedAt
            FROM ResponseChoices
            ORDER BY CreatedAt DESC";

        var results = await connection.QueryAsync(sql);

        return results.Select(result => new ResponseChoice
        {
            Id = Guid.Parse(result.Id),
            MessageId = Guid.Parse(result.MessageId),
            CreatedAt = DateTime.Parse(result.CreatedAt)
        });
    }

    public async Task UpdateAsync(ResponseChoice responseChoice)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE ResponseChoices
            SET MessageId = @MessageId, CreatedAt = @CreatedAt
            WHERE Id = @Id";

        var parameters = new
        {
            Id = responseChoice.Id.ToString(),
            MessageId = responseChoice.MessageId.ToString(),
            CreatedAt = responseChoice.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        };

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task DeleteAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            DELETE FROM ResponseChoices
            WHERE Id = @Id";

        await connection.ExecuteAsync(sql, new { Id = id.ToString() });
    }
}
