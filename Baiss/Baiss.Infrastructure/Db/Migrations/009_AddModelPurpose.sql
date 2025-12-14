-- Add Purpose field to Models table to distinguish between chat and embedding models
-- This migration adds Purpose column to Models table for explicit model type classification

-- Add Purpose column to Models table
ALTER TABLE Models ADD COLUMN Purpose TEXT; -- 'chat', 'embedding', or 'both'

-- Update existing Databricks models in the migration to set appropriate purposes
-- BGE and GTE models are typically embedding models
UPDATE Models 
SET Purpose = 'embedding' 
WHERE Provider = 'databricks' 
  AND (Name LIKE '%bge%' OR Name LIKE '%gte%' OR Name LIKE '%embedding%');

-- Other Databricks models are typically chat models
UPDATE Models 
SET Purpose = 'chat' 
WHERE Provider = 'databricks' 
  AND Purpose IS NULL;

-- Set default purpose for non-Databricks models (they can be both until explicitly set)
UPDATE Models 
SET Purpose = 'both' 
WHERE Purpose IS NULL;