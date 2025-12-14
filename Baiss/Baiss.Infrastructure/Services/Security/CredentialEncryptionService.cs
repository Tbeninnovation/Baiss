using System.Security.Cryptography;
using System.Text;
using Baiss.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Baiss.Infrastructure.Services.Security;

public class CredentialEncryptionService : ICredentialEncryptionService
{
    private readonly byte[] _key; // 32 bytes
    private readonly ILogger<CredentialEncryptionService> _logger;
    private static readonly string KeyFilePath = Path.Combine(AppContext.BaseDirectory, "credentials.key");

    public CredentialEncryptionService(ILogger<CredentialEncryptionService> logger)
    {
        _logger = logger;
        _key = LoadOrCreateKey();
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();
        using var enc = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipher = enc.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        // Prepend IV + HMAC for integrity
        var iv = aes.IV;
        var hmac = new HMACSHA256(_key);
        var full = iv.Concat(cipher).ToArray();
        var tag = hmac.ComputeHash(full);
        var final = new byte[tag.Length + full.Length];
        Buffer.BlockCopy(tag,0,final,0,tag.Length);
        Buffer.BlockCopy(full,0,final,tag.Length,full.Length);
        return Convert.ToBase64String(final);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;
        try
        {
            var all = Convert.FromBase64String(cipherText);
            var tag = all.Take(32).ToArray();
            var rest = all.Skip(32).ToArray();
            var hmac = new HMACSHA256(_key);
            var calc = hmac.ComputeHash(rest);
            if (!calc.SequenceEqual(tag)) throw new CryptographicException("HMAC validation failed");
            var iv = rest.Take(16).ToArray();
            var cipher = rest.Skip(16).ToArray();
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using var dec = aes.CreateDecryptor();
            var plain = dec.TransformFinalBlock(cipher,0,cipher.Length);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt provider credential");
            return string.Empty; // fail closed
        }
    }

    private static byte[] LoadOrCreateKey()
    {
        if (File.Exists(KeyFilePath))
        {
            try
            {
                var existing = File.ReadAllBytes(KeyFilePath);
                if (existing.Length == 32) return existing;
            }
            catch { /* fallback to regenerate */ }
        }
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            File.WriteAllBytes(KeyFilePath, key);
        }
        catch { /* ignore inability to persist; ephemeral key will break decrypt after restart */ }
        return key;
    }
}
