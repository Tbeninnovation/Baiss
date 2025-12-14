-- Add separate chat and embedding model configuration
-- This migration adds AIChatModelId and AIEmbeddingModelId to Settings table for model specialization

-- Add separate chat and embedding model columns to Settings table
ALTER TABLE Settings ADD COLUMN AIChatModelId TEXT;
ALTER TABLE Settings ADD COLUMN AIEmbeddingModelId TEXT;

-- Migrate existing AIModelId to AIChatModelId for backward compatibility
-- Only do this for Databricks models since that's where we're implementing the separation
UPDATE Settings 
SET AIChatModelId = AIModelId 
WHERE AIModelType = 'hosted' 
  AND AIModelId IS NOT NULL 
  AND AIModelId != ''
  AND EXISTS (
    SELECT 1 FROM Models 
    WHERE Models.Id = Settings.AIModelId 
    AND Models.Provider = 'databricks'
  );

-- Add some default Databricks embedding models
INSERT INTO Models (Id, Name, Type, Provider, Description, IsActive, CreatedAt)
SELECT 'databricks-bge-large-en', 'BGE Large EN', 'hosted', 'databricks',
       'Databricks BGE Large English embedding model', 1, datetime('now')
WHERE NOT EXISTS (
    SELECT 1 FROM Models WHERE Id = 'databricks-bge-large-en'
);

INSERT INTO Models (Id, Name, Type, Provider, Description, IsActive, CreatedAt)
SELECT 'databricks-gte-large-en', 'GTE Large EN', 'hosted', 'databricks',
       'Databricks GTE Large English embedding model', 1, datetime('now')
WHERE NOT EXISTS (
    SELECT 1 FROM Models WHERE Id = 'databricks-gte-large-en'
);

INSERT INTO Models (Id, Name, Type, Provider, Description, IsActive, CreatedAt)
SELECT 'databricks-text-embedding-ada-002', 'Text Embedding Ada 002', 'hosted', 'databricks',
       'Databricks Text Embedding Ada 002 compatible model', 1, datetime('now')
WHERE NOT EXISTS (
    SELECT 1 FROM Models WHERE Id = 'databricks-text-embedding-ada-002'
);