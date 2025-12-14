using Baiss.Application.DTOs;
using Baiss.Application.Interfaces;


using Microsoft.Extensions.Logging;

namespace Baiss.Application.UseCases;

/// <summary>
/// Use case for retrieving all conversations
/// </summary>
public class GetConversationsUseCase
{
	private readonly IConversationRepository _conversationRepository;
	private readonly IMessageRepository _messageRepository;
	private readonly ILogger<GetConversationsUseCase> _logger;


	public GetConversationsUseCase(
		IConversationRepository conversationRepository,
		IMessageRepository messageRepository,
		ILogger<GetConversationsUseCase> logger)
	{
		_conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
		_messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Retrieves all active conversations with their messages
	/// </summary>
	/// <returns>List of conversations</returns>
	public async Task<IEnumerable<ConversationDto>> ExecuteAsync()
	{
		try
		{
			var conversations = await _conversationRepository.GetAllActiveAsync();
			var conversationDtos = new List<ConversationDto>();

			foreach (var conversation in conversations)
			{
				// var messages = await _messageRepository.GetByConversationIdAsync(conversation.Id);

				var conversationDto = new ConversationDto
				{
					ConversationId = conversation.Id,
					Title = conversation.Title,
					CreatedAt = conversation.CreatedAt,
					UpdatedAt = conversation.UpdatedAt,
				};

				conversationDtos.Add(conversationDto);
			}
			_logger.LogInformation($"Loaded conversationDtos {conversationDtos.Count()} conversations");
			return conversationDtos.OrderByDescending(c => c.UpdatedAt);
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error loading conversations: {ex.Message}");
            return new List<ConversationDto>();
		}
	}
}

/// <summary>
/// Use case for retrieving a specific conversation
/// </summary>
public class GetConversationByIdUseCase
{
	private readonly IConversationRepository _conversationRepository;
	private readonly IMessageRepository _messageRepository;
	private readonly IResponseChoiceRepository _responseChoiceRepository;
	private readonly ISearchPathScoreRepository _searchPathScoreRepository;
	private readonly ILogger<GetConversationByIdUseCase> _logger;

	public GetConversationByIdUseCase(
		IConversationRepository conversationRepository,
		IMessageRepository messageRepository,
		IResponseChoiceRepository responseChoiceRepository,
		ISearchPathScoreRepository searchPathScoreRepository,
		ILogger<GetConversationByIdUseCase> logger)
	{
		_conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
		_messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
		_responseChoiceRepository = responseChoiceRepository ?? throw new ArgumentNullException(nameof(responseChoiceRepository));
		_searchPathScoreRepository = searchPathScoreRepository ?? throw new ArgumentNullException(nameof(searchPathScoreRepository));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Retrieves a conversation by its ID with all its messages
	/// </summary>
	/// <param name="conversationId">Conversation ID</param>
	/// <returns>The conversation with its messages</returns>
	public async Task<ConversationDto?> ExecuteAsync(Guid conversationId)
	{
		try
		{
			var conversation = await _conversationRepository.GetByIdAsync(conversationId);
			if (conversation == null)
				return null;

			var messages = await _messageRepository.GetByConversationIdAsync(conversationId);

			var messageDtos = new List<MessageDto>();
			foreach (var m in messages)
			{
				var messageDto = new MessageDto
				{
					MessageId = m.Id,
					Content = m.Content,
					SenderType = m.SenderType,
					SentAt = m.SentAt,
					Sources = m.Sources // Include sources from the message
				};

				// Retrieve paths if ResponseChoiceId exists
				if (m.ResponseChoiceId.HasValue)
				{
					try
					{
						var responseChoice = await _responseChoiceRepository.GetByIdAsync(m.ResponseChoiceId.Value);
						if (responseChoice != null)
						{
							var searchPathScores = await _searchPathScoreRepository.GetByResponseChoiceIdAsync(responseChoice.Id);
							messageDto.Paths = searchPathScores.Select(sps => new PathScoreDto
							{
								Path = sps.Path,
								Score = sps.Score
							}).ToList();
						}
					}
					catch (Exception ex)
					{
						_logger.LogWarning($"Error retrieving paths for message {m.Id}: {ex.Message}");
					}
				}

				messageDtos.Add(messageDto);
			}

			return new ConversationDto
			{
				ConversationId = conversation.Id,
				Title = conversation.Title,
				CreatedAt = conversation.CreatedAt,
				UpdatedAt = conversation.UpdatedAt,
				Messages = messageDtos.OrderBy(m => m.SentAt).ToList()
			};
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error loading conversation by ID {conversationId}: {ex.Message}");
			return null;
		}
	}
}

/// <summary>
/// Use case for conversation management (rename and delete)
/// </summary>
public class ConversationManagementUseCase
{
	private readonly IConversationRepository _conversationRepository;
	private readonly ILogger<ConversationManagementUseCase> _logger;

	public ConversationManagementUseCase(
		IConversationRepository conversationRepository,
		ILogger<ConversationManagementUseCase> logger)
	{
		_conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Renames the title of a conversation
	/// </summary>
	/// <param name="conversationId">Conversation ID</param>
	/// <param name="newTitle">New conversation title</param>
	/// <returns>True if the update succeeded, False otherwise</returns>
	public async Task<bool> RenameTitleAsync(Guid conversationId, string newTitle)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(newTitle))
			{
				_logger.LogWarning("Cannot rename conversation: new title is null or empty");
				return false;
			}

			if (newTitle.Trim().Length > 20)
			{
				_logger.LogWarning($"Cannot rename conversation: new title exceeds maximum length of 20 characters (current: {newTitle.Trim().Length})");
				return false;
			}

			// Get the existing conversation
			var conversation = await _conversationRepository.GetByIdAsync(conversationId);
			if (conversation == null)
			{
				_logger.LogWarning($"Conversation with ID {conversationId} not found for title update");
				return false;
			}

			// Update the title and modification date
			conversation.Title = newTitle.Trim();
			conversation.UpdatedAt = DateTime.UtcNow;

			// Save the changes
			await _conversationRepository.UpdateAsync(conversation);

			_logger.LogInformation($"Conversation {conversationId} title updated to '{newTitle}'");
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error updating conversation {conversationId} title: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// Deletes a conversation by its ID
	/// </summary>
	/// <param name="conversationId">ID of the conversation to delete</param>
	/// <returns>True if the deletion succeeded, False otherwise</returns>
	public async Task<bool> DeleteConversationAsync(Guid conversationId)
	{
		try
		{
			// Verify that the conversation exists
			var conversation = await _conversationRepository.GetByIdAsync(conversationId);
			if (conversation == null)
			{
				_logger.LogWarning($"Conversation with ID {conversationId} not found for deletion");
				return false;
			}

			// Delete the conversation
			await _conversationRepository.DeleteAsync(conversationId);

			_logger.LogInformation($"Conversation {conversationId} deleted successfully");
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError($"Error deleting conversation {conversationId}: {ex.Message}");
			return false;
		}
	}
}
