using Baiss.Domain.Entities;

namespace Baiss.Application.Interfaces;

public interface IProviderCredentialService
{
    Task<List<ProviderCredential>> GetAllAsync();
    Task<ProviderCredential?> GetAsync(string provider);
    Task SaveAsync(string provider, string? secretPlain, string secretType, Dictionary<string,string?> extraValues, bool replaceSecret);
}
