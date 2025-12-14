namespace Baiss.Domain.Entities;

/// <summary>
/// Represents stored (encrypted) provider credential plus optional extra configuration.
/// </summary>
public class ProviderCredential
{
    public string Provider { get; set; } = string.Empty; // e.g. openai, anthropic, azure, databricks
    public string SecretType { get; set; } = "api_key"; // api_key | token
    public string EncryptedSecret { get; set; } = string.Empty; // stored encrypted
    public string? SecretPlain { get; set; } // Only populated after decrypt (not persisted)
    public string? ExtraJson { get; set; } // Provider specific non-secret fields (endpoint, deployment, etc.)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
