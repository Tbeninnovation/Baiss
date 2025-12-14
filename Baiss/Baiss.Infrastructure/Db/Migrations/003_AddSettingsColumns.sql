-- Add missing columns to Settings table
-- This migration adds Id, EnableAutoUpdate, and AppVersion columns to align with the Settings entity

-- Add Id column as primary key
ALTER TABLE Settings ADD COLUMN Id TEXT;

-- Add EnableAutoUpdate column with default value
ALTER TABLE Settings ADD COLUMN EnableAutoUpdate INTEGER NOT NULL DEFAULT 1;

-- Add AppVersion column with default value
ALTER TABLE Settings ADD COLUMN AppVersion TEXT NOT NULL DEFAULT '1.0.0';

-- Update existing record to have the standard Id
UPDATE Settings SET Id = 'app-settings-global' WHERE Id IS NULL;

-- Create unique index on Id column to enforce uniqueness
CREATE UNIQUE INDEX IX_Settings_Id ON Settings(Id);
