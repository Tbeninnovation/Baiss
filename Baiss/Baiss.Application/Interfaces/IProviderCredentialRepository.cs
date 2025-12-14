using Baiss.Domain.Entities;

namespace Baiss.Application.Interfaces;

public interface IProviderCredentialRepository
{
    Task<ProviderCredential?> GetAsync(string provider);
    Task<List<ProviderCredential>> GetAllAsync();
    Task UpsertAsync(ProviderCredential credential);
    Task ImportFromEnvironmentIfEmptyAsync();
}
