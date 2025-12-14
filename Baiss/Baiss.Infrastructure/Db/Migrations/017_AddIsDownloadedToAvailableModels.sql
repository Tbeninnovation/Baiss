-- Add IsDownloaded column to existing AvailableModels table
-- This tracks whether a model from the catalog has been downloaded locally

ALTER TABLE AvailableModels ADD COLUMN IsDownloaded INTEGER NOT NULL DEFAULT 0;

-- Create index for IsDownloaded for efficient filtering
CREATE INDEX IX_AvailableModels_IsDownloaded ON AvailableModels(IsDownloaded);
