using System;
using Baiss.Application.DTOs;
using Baiss.Application.Interfaces;
using Baiss.Domain.Entities;
using System.Text;
using System.Text.Json;

namespace Baiss.Application.UseCases;


/// <summary>
/// Use case for sending a message (automatically creates a conversation if necessary)
/// </summary>
public class SendMessageUseCase
{
	private readonly IConversationRepository _conversationRepository;
	private readonly IMessageRepository _messageRepository;
	private readonly IAssistantService _assistantService;
	private readonly IResponseChoiceRepository _responseChoiceRepository;
	private readonly ISearchPathScoreRepository _searchPathScoreRepository;
	private readonly ISettingsRepository _settingsRepository;




	public SendMessageUseCase(
		IConversationRepository conversationRepository,
		IMessageRepository messageRepository,
		IAssistantService assistantService,
		IResponseChoiceRepository responseChoiceRepository,
		ISearchPathScoreRepository searchPathScoreRepository,
		ISettingsRepository settingsRepository)
	{
		_conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
		_messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
		_assistantService = assistantService ?? throw new ArgumentNullException(nameof(assistantService));
		_responseChoiceRepository = responseChoiceRepository ?? throw new ArgumentNullException(nameof(responseChoiceRepository));
		_searchPathScoreRepository = searchPathScoreRepository ?? throw new ArgumentNullException(nameof(searchPathScoreRepository));
		_settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
	}


	/// <summary>
	/// Executes sending a message with streaming response
	/// </summary>
	/// <param name="dto">Message data to send</param>
	/// <returns>Async enumerable of streaming response chunks and final result</returns>
	public async IAsyncEnumerable<(string? TextChunk, SendMessageResponseDto? FinalResult)> ExecuteStreamingAsync(SendMessageDto dto)
	{
		if (string.IsNullOrWhiteSpace(dto.Content))
		{
			yield return (null, new SendMessageResponseDto
			{
				IsSuccessful = false,
				ErrorMessage = "Message cannot be empty"
			});
			yield break;
		}

		// Setup conversation and handle all setup logic
		var setupResult = await SetupConversationForStreamingAsync(dto);
		if (!setupResult.Success || setupResult.Conversation == null || setupResult.UserMessage == null)
		{
			yield return (null, setupResult.ErrorResponse ?? new SendMessageResponseDto
			{
				IsSuccessful = false,
				ErrorMessage = "Failed to setup conversation"
			});
			yield break;
		}

		// Stream the response
		await foreach (var result in StreamAssistantResponseAsync(dto.Content, setupResult.Conversation, setupResult.UserMessage, setupResult.IsNewConversation, dto.FilePaths))
		{
			yield return result;
		}
	}

	private async Task<ConversationSetupResult> SetupConversationForStreamingAsync(SendMessageDto dto)
	{
		try
		{
			// List<MessageItem> messagesHistorical = await GetConversationContext(dto.ConversationId, 10);
			Conversation conversation;
			bool isNewConversation = false;

			// If no ConversationId, create a new conversation automatically
			if (dto.ConversationId == null || dto.ConversationId == Guid.Empty)
			{
				conversation = Conversation.CreateFromFirstMessage(dto.Content);
				await _conversationRepository.CreateAsync(conversation);
				isNewConversation = true;

				var verifyConversation = await _conversationRepository.GetByIdAsync(conversation.Id);
				if (verifyConversation == null)
				{
					return new ConversationSetupResult
					{
						Success = false,
						ErrorResponse = new SendMessageResponseDto
						{
							IsSuccessful = false,
							ErrorMessage = "Failed to create conversation - conversation not found after creation"
						}
					};
				}
			}
			else
			{
				var existingConversation = await _conversationRepository.GetByIdAsync(dto.ConversationId.Value);
				if (existingConversation == null)
				{
					return new ConversationSetupResult
					{
						Success = false,
						ErrorResponse = new SendMessageResponseDto
						{
							IsSuccessful = false,
							ErrorMessage = "Conversation not found"
						}
					};
				}
				conversation = existingConversation;
			}

			// Create user message
			var userMessage = Message.CreateUserMessage(conversation.Id, dto.Content, SenderType.USER);
			await _messageRepository.CreateAsync(userMessage);

			// Update conversation timestamp
			conversation.UpdateTimestamp();
			await _conversationRepository.UpdateAsync(conversation);

			return new ConversationSetupResult
			{
				Success = true,
				Conversation = conversation,
				UserMessage = userMessage,
				IsNewConversation = isNewConversation
			};
		}
		catch (Exception ex)
		{
			return new ConversationSetupResult
			{
				Success = false,
				ErrorResponse = new SendMessageResponseDto
				{
					IsSuccessful = false,
					ErrorMessage = $"Error setting up conversation: {ex.Message}"
				}
			};
		}
	}

	private async IAsyncEnumerable<(string? TextChunk, SendMessageResponseDto? FinalResult)> StreamAssistantResponseAsync(
		string userContent,
		Conversation conversation,
		Message userMessage,
		bool isNewConversation,
		List<string>? filePaths = null)
	{
		var fullAssistantResponseBuilder = new StringBuilder();
		var streamingError = false;
		string? errorMessage = null;

		// Get conversation context for streaming
		var contextResult = await GetConversationContextSafelyAsync(conversation.Id);
		if (!contextResult.Success)
		{
			yield return (null, new SendMessageResponseDto
			{
				IsSuccessful = false,
				ErrorMessage = contextResult.ErrorMessage ?? "Error getting conversation context"
			});
			yield break;
		}

		if (_assistantService.IsReady)
		{
			await using var streamEnumerator = _assistantService
				.GenerateStreamingResponseAsync(userContent, contextResult.MessagesHistorical, filePaths)
				.GetAsyncEnumerator();

			while (true)
			{
				string? chunk;
				try
				{
					if (!await streamEnumerator.MoveNextAsync())
					{
						break;
					}

					chunk = streamEnumerator.Current;
				}
				catch (Exception ex)
				{
					streamingError = true;
					errorMessage = ex.Message;
					break;
				}

				if (string.IsNullOrEmpty(chunk))
				{
					continue;
				}

				if (chunk.StartsWith("[ERROR:", StringComparison.OrdinalIgnoreCase))
				{
					streamingError = true;
					if (string.IsNullOrEmpty(errorMessage))
					{
						var trimmedChunk = chunk.Trim('[', ']');
						const string prefix = "ERROR:";
						if (trimmedChunk.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
						{
							errorMessage = trimmedChunk.Substring(prefix.Length).Trim();
						}
						else
						{
							errorMessage = trimmedChunk;
						}
					}
				}
				// Console.WriteLine($"Chunk received: -------------------------------------------------->>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>  {chunk}");
				fullAssistantResponseBuilder.Append(chunk);
				yield return (chunk, null);
			}
		}
		else
		{
			streamingError = true;
			errorMessage = "Assistant service is not ready";
		}

		// Retrieve paths from the assistant service
		var pathsReturned = _assistantService.GetLastReceivedPaths();

		var finalResult = await FinalizeStreamingResponseAsync(
			conversation,
			userMessage,
			isNewConversation,
			fullAssistantResponseBuilder.ToString(),
			streamingError,
			errorMessage,
			pathsReturned,
			filePaths
			);

		if (pathsReturned != null && pathsReturned.Any())
		{
			finalResult.Paths = pathsReturned;
		}

		yield return (null, finalResult);
	}

	private async Task<ConversationContextResult> GetConversationContextSafelyAsync(Guid conversationId)
	{
		try
		{
			var messagesHistorical = await GetConversationContext(conversationId, 10);
			return new ConversationContextResult
			{
				Success = true,
				MessagesHistorical = messagesHistorical
			};
		}
		catch (Exception ex)
		{
			return new ConversationContextResult
			{
				Success = false,
				ErrorMessage = ex.Message
			};
		}
	}
	private async Task<SendMessageResponseDto> FinalizeStreamingResponseAsync(
		Conversation conversation,
		Message userMessage,
		bool isNewConversation,
		string fullAssistantResponse,
		bool streamingError,
		string? errorMessage,
		List<PathScoreDto>? pathsReturned = null,
		List<string>? filePaths = null)
	{
		try
		{
			ContentResponse assistantContentResponse;

			// Create ContentResponse after streaming is complete
			if (!streamingError && !string.IsNullOrWhiteSpace(fullAssistantResponse))
			{
				assistantContentResponse = new ContentResponse
				{
					Status = 200,
					Success = true,
					Response = new ResponseData
					{
						Messages = new List<MessageItem>
						{
							new MessageItem
							{
								Role = "assistant",
								Content = new List<ContentItem>
								{
									new ContentItem { Text = fullAssistantResponse }
								}
							}
						}
					},
					Usage = new UsageData(),
					Sources = new List<SourceItem>(),
					StopReason = "complete"
				};
			}
			else
			{
				// Handle error case
				string responseText = streamingError && !string.IsNullOrEmpty(errorMessage)
					? $"The assistant encountered an error: {errorMessage}"
					: "The assistant cannot respond at the moment. Please try again later.";

				fullAssistantResponse = string.IsNullOrWhiteSpace(fullAssistantResponse)
					? responseText
					: fullAssistantResponse;

				assistantContentResponse = CreateErrorContentResponse(503, "Assistant service error", fullAssistantResponse, "error");
			}

			// Create assistant message
			var assistantMessage = Message.CreateAssistantMessage(conversation.Id, fullAssistantResponse, null);
			await _messageRepository.CreateAsync(assistantMessage);

			// Save pathsReturned if available
			if (pathsReturned != null && pathsReturned.Any())
			{
				await SavePathsToDatabase(assistantMessage.Id, pathsReturned);
			}

			// Save file paths to settings.AllowedPaths if files are attached
			if (filePaths != null && filePaths.Any())
			{
				await SaveFilePathsToSettingsAsync(filePaths);
			}

			// Return final result
			return new SendMessageResponseDto
			{
				IsSuccessful = true,
				ConversationId = conversation.Id,
				ConversationTitle = conversation.Title,
				MessageId = userMessage.Id,
				IsNewConversation = isNewConversation,
				Content = assistantContentResponse,
				Sources = assistantContentResponse?.Sources ?? new List<SourceItem>()
			};
		}
		catch (Exception ex)
		{
			return new SendMessageResponseDto
			{
				IsSuccessful = false,
				ErrorMessage = $"Error finalizing message: {ex.Message}"
			};
		}
	}

	private class ConversationSetupResult
	{
		public bool Success { get; set; }
		public Conversation? Conversation { get; set; }
		public Message? UserMessage { get; set; }
		public bool IsNewConversation { get; set; }
		public SendMessageResponseDto? ErrorResponse { get; set; }
	}

	private class ConversationContextResult
	{
		public bool Success { get; set; }
		public List<MessageItem> MessagesHistorical { get; set; } = new List<MessageItem>();
		public string? ErrorMessage { get; set; }
	}

	/// <summary>
	/// Creates a default ContentResponse for error cases
	/// </summary>
	/// <param name="status">HTTP status code</param>
	/// <param name="error">Error message</param>
	/// <param name="assistantResponse">Assistant response text</param>
	/// <param name="stopReason">Stop reason for the response</param>
	/// <returns>A ContentResponse with error information</returns>
	private static ContentResponse CreateErrorContentResponse(int status, string error, string assistantResponse, string stopReason)
	{
		return new ContentResponse
		{
			Status = status,
			Success = false,
			Error = error,
			Response = new ResponseData
			{
				Messages = new List<MessageItem>
				{
					new MessageItem
					{
						Role = "assistant",
						Content = new List<ContentItem>
						{
							new ContentItem { Text = assistantResponse }
						}
					}
				}
			},
			Usage = new UsageData(),
			Sources = new List<SourceItem>(), // Empty sources list for error cases
											  // Dashboard = new List<object>(),
			StopReason = stopReason
		};
	}

	private async Task<List<MessageItem>> GetConversationContext(Guid? conversationId, int messageCount = 10)
	{

		if (conversationId == null || conversationId == Guid.Empty)
		{
			return new List<MessageItem>();
		}

		var previousMessages = await _messageRepository.GetByConversationIdAsync(conversationId.Value);
		var messagesToProcess = previousMessages
			.OrderByDescending(m => m.SentAt)
			.Take(messageCount)
			.OrderBy(m => m.SentAt)
			.Select(m => new
			{
				m.SenderType,
				m.Content
			}).ToList();

		if (!messagesToProcess.Any())
		{
			return new List<MessageItem>();
		}
		var messagesHistorical = messagesToProcess.Select(msg =>
		{
			var content = new List<ContentItem>();

			// Add text content only if it exists
			if (!string.IsNullOrEmpty(msg.Content))
			{
				content.Add(new ContentItem { Text = msg.Content });
			}

			return new MessageItem
			{
				Role = msg.SenderType == SenderType.USER ? "user" : "assistant",
				Content = content
			};
		}).ToList();

		// print conversation context for debugging
		// foreach (var msg in messagesHistorical)
		// {
		// 	Console.WriteLine($"Role: {msg.Role}, Content: {msg.Content.Select(c => c.Text).FirstOrDefault()}");
		// }

		return messagesHistorical;
	}

	private async Task SavePathsToDatabase(Guid messageId, List<PathScoreDto> paths)
	{
		try
		{
			// Create ResponseChoice
			var responseChoice = new ResponseChoice
			{
				Id = Guid.NewGuid(),
				MessageId = messageId,
				CreatedAt = DateTime.UtcNow
			};

			await _responseChoiceRepository.CreateAsync(responseChoice);

			// Create SearchPathScores
			foreach (var pathDto in paths)
			{
				var searchPathScore = new SearchPathScore
				{
					Id = Guid.NewGuid(),
					Path = pathDto.Path,
					Score = (float)pathDto.Score,
					ResponseChoiceId = responseChoice.Id,
					CreatedAt = DateTime.UtcNow
				};

				await _searchPathScoreRepository.CreateAsync(searchPathScore);
			}

			// Update message with ResponseChoiceId
			var message = await _messageRepository.GetByIdAsync(messageId);
			if (message != null)
			{
				message.ResponseChoiceId = responseChoice.Id;
				await _messageRepository.UpdateAsync(message);
			}
		}
		catch (Exception ex)
		{
			// Log error but don't fail the entire operation
			Console.WriteLine($"Error saving paths to database: {ex.Message}");
		}
	}

	/// <summary>
	/// Saves file paths to settings.AllowedPaths if they don't already exist
	/// </summary>
	private async Task SaveFilePathsToSettingsAsync(List<string> filePaths)
	{
		try
		{
			var settings = await _settingsRepository.GetAsync();
			if (settings == null)
			{
				Console.WriteLine("Settings not found, cannot save file paths");
				return;
			}

			bool settingsUpdated = false;

			foreach (var filePath in filePaths)
			{
				if (!string.IsNullOrWhiteSpace(filePath) && !settings.AllowedPaths.Contains(filePath))
				{
					settings.AllowedPaths.Add(filePath);
					settingsUpdated = true;
				}
			}

			if (settingsUpdated)
			{
				await _settingsRepository.SaveAsync(settings);
				Console.WriteLine($"Added {filePaths.Count} file path(s) to settings.AllowedPaths");
			}
		}
		catch (Exception ex)
		{
			// Log error but don't fail the entire operation
			Console.WriteLine($"Error saving file paths to settings: {ex.Message}");
		}
	}

}

