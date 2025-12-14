-- Migration 014: Add LocalPath column to Models table
-- This column stores the file system path for local models

-- Add LocalPath column to Models table
-- This column is optional and only used when Type = 'local'
ALTER TABLE Models ADD COLUMN LocalPath TEXT;

-- Create index for better performance when querying local models by path
CREATE INDEX IX_Models_LocalPath ON Models(LocalPath) WHERE LocalPath IS NOT NULL;

-- Note: Existing local models will have NULL LocalPath initially
-- Users can set the path through the UI when configuring local models
