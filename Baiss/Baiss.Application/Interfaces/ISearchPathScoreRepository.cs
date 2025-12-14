using Baiss.Domain.Entities;

namespace Baiss.Application.Interfaces;

public interface ISearchPathScoreRepository
{
    Task<SearchPathScore> CreateAsync(SearchPathScore searchPathScore);
    Task<SearchPathScore?> GetByIdAsync(Guid id);
    Task<IEnumerable<SearchPathScore>> GetByResponseChoiceIdAsync(Guid responseChoiceId);
    Task<IEnumerable<SearchPathScore>> GetAllAsync();
    Task UpdateAsync(SearchPathScore searchPathScore);
    Task DeleteAsync(Guid id);
    Task DeleteByResponseChoiceIdAsync(Guid responseChoiceId);
}
