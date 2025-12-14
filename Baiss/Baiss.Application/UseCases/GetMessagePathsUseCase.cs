using Baiss.Application.DTOs;
using Baiss.Application.Interfaces;
using Baiss.Domain.Entities;

namespace Baiss.Application.UseCases;

public class GetMessagePathsUseCase
{
    private readonly IMessageRepository _messageRepository;
    private readonly IResponseChoiceRepository _responseChoiceRepository;
    private readonly ISearchPathScoreRepository _searchPathScoreRepository;

    public GetMessagePathsUseCase(
        IMessageRepository messageRepository,
        IResponseChoiceRepository responseChoiceRepository,
        ISearchPathScoreRepository searchPathScoreRepository)
    {
        _messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
        _responseChoiceRepository = responseChoiceRepository ?? throw new ArgumentNullException(nameof(responseChoiceRepository));
        _searchPathScoreRepository = searchPathScoreRepository ?? throw new ArgumentNullException(nameof(searchPathScoreRepository));
    }

    public async Task<List<PathScoreDto>> GetPathsByMessageIdAsync(Guid messageId)
    {
        try
        {
            var message = await _messageRepository.GetByIdAsync(messageId);
            if (message?.ResponseChoiceId == null)
            {
                return new List<PathScoreDto>();
            }

            var responseChoice = await _responseChoiceRepository.GetByIdAsync(message.ResponseChoiceId.Value);
            if (responseChoice == null)
            {
                return new List<PathScoreDto>();
            }

            var searchPathScores = await _searchPathScoreRepository.GetByResponseChoiceIdAsync(responseChoice.Id);

            return searchPathScores.Select(sps => new PathScoreDto
            {
                Path = sps.Path,
                Score = sps.Score
            }).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving paths for message {messageId}: {ex.Message}");
            return new List<PathScoreDto>();
        }
    }

    public async Task<Dictionary<Guid, List<PathScoreDto>>> GetPathsByConversationIdAsync(Guid conversationId)
    {
        try
        {
            var messages = await _messageRepository.GetByConversationIdAsync(conversationId);
            var result = new Dictionary<Guid, List<PathScoreDto>>();

            foreach (var message in messages)
            {
                if (message.ResponseChoiceId.HasValue)
                {
                    var paths = await GetPathsByMessageIdAsync(message.Id);
                    if (paths.Any())
                    {
                        result[message.Id] = paths;
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving paths for conversation {conversationId}: {ex.Message}");
            return new Dictionary<Guid, List<PathScoreDto>>();
        }
    }
}
