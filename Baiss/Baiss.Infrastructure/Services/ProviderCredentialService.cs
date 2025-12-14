using System.Linq;
using System.Text.Json;
using Baiss.Application.Interfaces;
using Baiss.Application.Models.AI;
using Baiss.Domain.Entities;
using Baiss.Infrastructure.Services.AI;
using Microsoft.Extensions.Logging;

namespace Baiss.Infrastructure.Services;

public class ProviderCredentialService : IProviderCredentialService
{
    private readonly IProviderCredentialRepository _repository;
    private readonly ICredentialEncryptionService _encryption;
    private readonly ILogger<ProviderCredentialService> _logger;
    // private readonly SemanticKernelConfig _semanticKernelConfig;

    public ProviderCredentialService(
        IProviderCredentialRepository repository,
        ICredentialEncryptionService encryption,
        ILogger<ProviderCredentialService> logger)
    {
        _repository = repository;
        _encryption = encryption;
        // _semanticKernelConfig = semanticKernelConfig;
        _logger = logger;
    }

    public async Task<List<ProviderCredential>> GetAllAsync()
    {
        var creds = await _repository.GetAllAsync();
        foreach (var c in creds)
        {
            c.SecretPlain = _encryption.Decrypt(c.EncryptedSecret);
        }
        return creds;
    }

    public async Task<ProviderCredential?> GetAsync(string provider)
    {
        var cred = await _repository.GetAsync(provider);
        if (cred != null)
        {
            cred.SecretPlain = _encryption.Decrypt(cred.EncryptedSecret);
        }
        return cred;
    }

    public async Task SaveAsync(string provider, string? secretPlain, string secretType, Dictionary<string,string?> extraValues, bool replaceSecret)
    {
        provider = provider.ToLower();
        var existing = await _repository.GetAsync(provider);
        string encryptedSecret;
        if (existing != null && !replaceSecret)
        {
            encryptedSecret = existing.EncryptedSecret; // keep old
        }
        else
        {
            if (string.IsNullOrWhiteSpace(secretPlain)) throw new ArgumentException("Secret must be provided when replacing.");
            encryptedSecret = _encryption.Encrypt(secretPlain);
        }
        string? extraJson = null;
        if (extraValues.Any(v => !string.IsNullOrWhiteSpace(v.Value)))
        {
            extraJson = JsonSerializer.Serialize(extraValues);
        }
        await _repository.UpsertAsync(new ProviderCredential
        {
            Provider = provider,
            SecretType = secretType,
            EncryptedSecret = encryptedSecret,
            ExtraJson = extraJson
        });
        _logger.LogInformation("Saved credentials for provider {Provider}", provider);

        // await SemanticKernelConfiguration.RefreshSemanticKernelConfigAsync(
        //     // _semanticKernelConfig,
        //     _repository,
        //     _encryption,
        //     _logger);
    }
}
