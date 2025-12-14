
namespace Baiss.Domain.Entities;

public enum SenderType
{
	USER,
	ASSISTANT
}

public class Message
{
	public Guid Id { get; set; }
	public Guid ConversationId { get; set; }
	public SenderType SenderType { get; set; } = SenderType.USER;
	public string Content { get; set; } = string.Empty;
	public string? Sources { get; set; } // JSON string to store source information
	public DateTime SentAt { get; set; } = DateTime.UtcNow;
	public Guid? ResponseChoiceId { get; set; }

	// Navigation properties
	public Conversation? Conversation { get; set; }
	public ResponseChoice? ResponseChoice { get; set; }

	// Factory methods to create messages
	public static Message CreateUserMessage(Guid conversationId, string content, SenderType senderType = SenderType.USER)
	{
		return new Message
		{
			Id = Guid.NewGuid(),
			ConversationId = conversationId,
			SenderType = senderType,
			Content = content,
			Sources = null, // User messages typically don't have sources
			SentAt = DateTime.UtcNow
		};
	}

	public static Message CreateAssistantMessage(Guid conversationId, string content, string? sources = null, SenderType senderType = SenderType.ASSISTANT)
	{
		return new Message
		{
			Id = Guid.NewGuid(),
			ConversationId = conversationId,
			SenderType = senderType,
			Content = content,
			Sources = sources,
			SentAt = DateTime.UtcNow
		};
	}
}
