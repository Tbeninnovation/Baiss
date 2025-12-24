-- Add IsValid column to existing AvailableModels table
-- This tracks whether a model is considered valid

ALTER TABLE AvailableModels ADD COLUMN IsValid INTEGER NOT NULL DEFAULT 0;
