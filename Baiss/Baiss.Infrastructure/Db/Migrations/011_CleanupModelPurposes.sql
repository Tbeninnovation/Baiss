-- Migration 011: Cleanup model purposes - remove 'both' purpose and clean existing data
-- This migration ensures all models have either 'chat' or 'embedding' purpose, never 'both'

-- First, let's see what we have
-- SELECT Id, Name, Provider, Purpose FROM Models;

-- Delete all models with 'both' purpose since we don't want them anymore
DELETE FROM Models 
WHERE Purpose = 'both';

-- For any remaining models that might have null or empty Purpose, set a default based on provider
-- Databricks models without purpose will be set to 'chat' (user can add embedding models manually)
UPDATE Models 
SET Purpose = 'chat' 
WHERE (Purpose IS NULL OR Purpose = '' OR Purpose = 'both') 
  AND Provider = 'databricks';

-- OpenAI models without purpose will be set to 'chat' 
UPDATE Models 
SET Purpose = 'chat' 
WHERE (Purpose IS NULL OR Purpose = '' OR Purpose = 'both') 
  AND Provider = 'openai';

-- Python/Ollama models without purpose will be set to 'chat'
UPDATE Models 
SET Purpose = 'chat' 
WHERE (Purpose IS NULL OR Purpose = '' OR Purpose = 'both') 
  AND Provider IN ('python', 'ollama');

-- Add a constraint to ensure Purpose can only be 'chat' or 'embedding'
-- Note: SQLite doesn't support CHECK constraints in ALTER TABLE, so we'll rely on application logic

-- Verify the cleanup
-- SELECT Id, Name, Provider, Purpose FROM Models ORDER BY Provider, Purpose;