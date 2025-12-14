using Baiss.Application.DTOs;

namespace Baiss.Application.Interfaces;

public interface IConfigChatService
{
    Task<ChatConfigDto?> GetChatConfigAsync();
}
