using Baiss.Domain.Entities;

namespace Baiss.Application.Interfaces;

public interface IConversationRepository
{
	Task<Conversation?> GetByIdAsync(Guid conversationId);
	Task<IEnumerable<Conversation>> GetAllActiveAsync();
	Task<Conversation> CreateAsync(Conversation conversation);
	Task UpdateAsync(Conversation conversation);
	Task DeleteAsync(Guid conversationId);
}
