namespace Baiss.Application.Interfaces;

public interface ICredentialEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
