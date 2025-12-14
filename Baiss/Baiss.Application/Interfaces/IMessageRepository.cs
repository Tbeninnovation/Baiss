using Baiss.Domain.Entities;

namespace Baiss.Application.Interfaces;

public interface IMessageRepository
{
	Task<Message> CreateAsync(Message message);
	Task<IEnumerable<Message>> GetByConversationIdAsync(Guid conversationId);
	Task<Message?> GetByIdAsync(Guid messageId);
	Task UpdateAsync(Message message);
}
