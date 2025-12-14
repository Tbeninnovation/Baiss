-- Adds ProviderCredentials table to store encrypted provider secrets and extra configuration
CREATE TABLE ProviderCredentials (
    Provider TEXT PRIMARY KEY,
    EncryptedSecret TEXT NOT NULL,
    SecretType TEXT NOT NULL,
    ExtraJson TEXT,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT
);
