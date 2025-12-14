-- Add AvailableModels table to store HuggingFace model metadata as JSONB
-- This table caches available models from the HuggingFace API with full metadata

CREATE TABLE AvailableModels (
    Id TEXT PRIMARY KEY,                    -- model_id from HuggingFace
    Metadata TEXT NOT NULL,                 -- Full JSON metadata (JSONB storage as JsonDocument)
    CreatedAt TEXT NOT NULL,                -- When first added to database
    UpdatedAt TEXT NOT NULL                 -- When last updated
);

-- Create index for timestamps
CREATE INDEX IX_AvailableModels_UpdatedAt ON AvailableModels(UpdatedAt);
