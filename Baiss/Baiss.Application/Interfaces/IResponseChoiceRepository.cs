using Baiss.Domain.Entities;

namespace Baiss.Application.Interfaces;

public interface IResponseChoiceRepository
{
    Task<ResponseChoice> CreateAsync(ResponseChoice responseChoice);
    Task<ResponseChoice?> GetByIdAsync(Guid id);
    Task<ResponseChoice?> GetByMessageIdAsync(Guid messageId);
    Task<IEnumerable<ResponseChoice>> GetAllAsync();
    Task UpdateAsync(ResponseChoice responseChoice);
    Task DeleteAsync(Guid id);
}
