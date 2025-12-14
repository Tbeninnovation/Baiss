
using System.Data;
using Dapper;
using Baiss.Domain.Entities;
using Baiss.Infrastructure.Interfaces;
using Baiss.Application.Interfaces;

namespace Baiss.Infrastructure.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MessageRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Message> CreateAsync(Message message)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO Messages (Id, ConversationId, SenderType, Content, Sources, SentAt, ResponseChoiceId)
            VALUES (@Id, @ConversationId, @SenderType, @Content, @Sources, @SentAt, @ResponseChoiceId)";

        // Create parameters with explicit conversions to string for consistency
        var parameters = new
        {
            Id = message.Id.ToString(),
            ConversationId = message.ConversationId.ToString(),
            SenderType = message.SenderType.ToString(), // Explicit conversion to string
            Content = message.Content,
            Sources = message.Sources, // Include Sources field
            SentAt = message.SentAt.ToString("yyyy-MM-dd HH:mm:ss"),
            ResponseChoiceId = message.ResponseChoiceId?.ToString()
        };

        await connection.ExecuteAsync(sql, parameters);
        return message;
    }

    public async Task<IEnumerable<Message>> GetByConversationIdAsync(Guid conversationId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT Id, ConversationId, SenderType, Content, Sources, SentAt, ResponseChoiceId
            FROM Messages
            WHERE ConversationId = @ConversationId
            ORDER BY SentAt";

        var results = await connection.QueryAsync(sql, new { ConversationId = conversationId.ToString() });

        return results.Select(result => new Message
        {
            Id = Guid.Parse(result.Id),
            ConversationId = Guid.Parse(result.ConversationId),
            SenderType = Enum.TryParse<SenderType>(result.SenderType, true, out SenderType senderType) ? senderType : SenderType.USER,
            Content = result.Content,
            Sources = result.Sources, // Include Sources field
            SentAt = DateTime.Parse(result.SentAt),
            ResponseChoiceId = result.ResponseChoiceId != null ? Guid.Parse(result.ResponseChoiceId) : null
        });
    }

    public async Task<Message?> GetByIdAsync(Guid messageId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT Id, ConversationId, SenderType, Content, Sources, SentAt, ResponseChoiceId
            FROM Messages
            WHERE Id = @MessageId";

        var result = await connection.QueryFirstOrDefaultAsync(sql, new { MessageId = messageId.ToString() });

        if (result == null) return null;

        return new Message
        {
            Id = Guid.Parse(result.Id),
            ConversationId = Guid.Parse(result.ConversationId),
            SenderType = Enum.TryParse<SenderType>(result.SenderType, true, out SenderType senderType) ? senderType : SenderType.USER,
            Content = result.Content,
            Sources = result.Sources, // Include Sources field
            SentAt = DateTime.Parse(result.SentAt),
            ResponseChoiceId = result.ResponseChoiceId != null ? Guid.Parse(result.ResponseChoiceId) : null
        };
    }

    public async Task UpdateAsync(Message message)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE Messages
            SET Content = @Content, SenderType = @SenderType, Sources = @Sources, ResponseChoiceId = @ResponseChoiceId
            WHERE Id = @Id";

        var parameters = new
        {
            Id = message.Id.ToString(),
            Content = message.Content,
            SenderType = message.SenderType.ToString(),
            Sources = message.Sources, // Include Sources field in updates
            ResponseChoiceId = message.ResponseChoiceId?.ToString()
        };

        await connection.ExecuteAsync(sql, parameters);
    }

    // Legacy methods pour compatibilité - à supprimer si pas utilisés
    public async Task AddAsync(Message message)
    {
        await CreateAsync(message);
    }

    public async Task<IEnumerable<Message>> GetByConversationAsync(Guid conversationId)
    {
        return await GetByConversationIdAsync(conversationId);
    }
}
