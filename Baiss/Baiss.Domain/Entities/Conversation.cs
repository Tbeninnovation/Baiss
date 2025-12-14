


namespace Baiss.Domain.Entities;

public class Conversation
{
	public Guid Id { get; set; }

	public required string Title { get; set; }

	public string CreatedByUserId { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

	// Navigation property (ne sera pas mappée directement par Dapper)
	public List<Message> Messages { get; set; } = new();

	// Factory method pour créer automatiquement une conversation
	public static Conversation CreateFromFirstMessage(string firstMessageContent)
	{
		return new Conversation
		{
			Id = Guid.NewGuid(),
			Title = GenerateTitle(firstMessageContent),
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow,
			Messages = new List<Message>()
		};
	}

	// Logique pour générer le titre (premiers 20 caractères)
	private static string GenerateTitle(string messageContent)
	{
		if (string.IsNullOrWhiteSpace(messageContent))
			return "New conversation";

		var title = messageContent.Length <= 20
			? messageContent.Trim()
			: messageContent.Substring(0, 20).Trim() + "...";

		return title;
	}

	public void UpdateTimestamp()
	{
		UpdatedAt = DateTime.UtcNow;
	}
}
