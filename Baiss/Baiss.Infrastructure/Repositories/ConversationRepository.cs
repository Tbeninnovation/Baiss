using System.Data;
using Dapper;
using Baiss.Domain.Entities;
using Baiss.Infrastructure.Interfaces;
using Baiss.Application.Interfaces;

namespace Baiss.Infrastructure.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ConversationRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Conversation?> GetByIdAsync(Guid conversationId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT Id, Title, CreatedByUserId, CreatedAt, UpdatedAt
            FROM Conversations
            WHERE Id = @ConversationId";

        var result = await connection.QueryFirstOrDefaultAsync(sql, new { ConversationId = conversationId.ToString() });

        if (result == null) return null;

        return new Conversation
        {
            Id = Guid.Parse(result.Id),
            Title = result.Title,
            CreatedByUserId = result.CreatedByUserId,
            CreatedAt = DateTime.Parse(result.CreatedAt),
            UpdatedAt = result.UpdatedAt != null ? DateTime.Parse(result.UpdatedAt) : DateTime.Parse(result.CreatedAt)
        };
    }

    public async Task<IEnumerable<Conversation>> GetAllActiveAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT Id, Title, CreatedByUserId, CreatedAt, UpdatedAt
            FROM Conversations
            ORDER BY UpdatedAt DESC";

        var results = await connection.QueryAsync(sql);

        return results.Select(result => new Conversation
        {
            Id = Guid.Parse(result.Id),
            Title = result.Title,
            CreatedByUserId = result.CreatedByUserId,
            CreatedAt = DateTime.Parse(result.CreatedAt),
            UpdatedAt = result.UpdatedAt != null ? DateTime.Parse(result.UpdatedAt) : DateTime.Parse(result.CreatedAt)
        });
    }

    public async Task<Conversation> CreateAsync(Conversation conversation)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO Conversations (Id, Title, CreatedByUserId, CreatedAt, UpdatedAt)
            VALUES (@Id, @Title, @CreatedByUserId, @CreatedAt, @UpdatedAt)";

        var parameters = new
        {
            Id = conversation.Id.ToString(),
            Title = conversation.Title,
            CreatedByUserId = conversation.CreatedByUserId,
            CreatedAt = conversation.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            UpdatedAt = conversation.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        };

        await connection.ExecuteAsync(sql, parameters);
        return conversation;
    }

    public async Task UpdateAsync(Conversation conversation)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE Conversations
            SET Title = @Title, UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        var parameters = new
        {
            Id = conversation.Id.ToString(),
            Title = conversation.Title,
            UpdatedAt = conversation.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        };

        await connection.ExecuteAsync(sql, parameters);
    }

    public async Task DeleteAsync(Guid conversationId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            DELETE FROM Conversations
            WHERE Id = @ConversationId";

        await connection.ExecuteAsync(sql, new { ConversationId = conversationId.ToString() });
    }

    // Legacy method pour compatibilité - à supprimer si pas utilisé
    public async Task<Conversation?> GetWithMessagesAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();

        var lookup = new Dictionary<Guid, Conversation>();

        await connection.QueryAsync<Conversation, Message, Conversation>(
            sql: "GetConversationWithMessages",
            map: (conv, msg) =>
            {
                if (!lookup.TryGetValue(conv.Id, out var conversation))
                {
                    conversation = conv;
                    lookup.Add(conv.Id, conversation);
                }

                if (msg != null)
                    conversation.Messages.Add(msg);

                return conversation;
            },
            param: new { Id = id },
            splitOn: "Id"
        );

        return lookup.Values.FirstOrDefault();
    }
}
