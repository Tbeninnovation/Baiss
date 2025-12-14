-- Add file permission and extension settings columns to Settings table
-- This migration adds the remaining columns to align with the current Settings entity

-- Add file reading permission column
ALTER TABLE Settings ADD COLUMN AllowFileReading INTEGER NOT NULL DEFAULT 0;

-- Add file writing permission columns
ALTER TABLE Settings ADD COLUMN AllowUpdateCreatedFiles INTEGER NOT NULL DEFAULT 0;
ALTER TABLE Settings ADD COLUMN AllowCreateNewFiles INTEGER NOT NULL DEFAULT 0;

-- Add new files save path column
ALTER TABLE Settings ADD COLUMN NewFilesSavePath TEXT NOT NULL DEFAULT '';

-- Add allowed file extensions column (stored as JSON array)
ALTER TABLE Settings ADD COLUMN AllowedFileExtensions TEXT NOT NULL DEFAULT '["docx","xls","xlsx","pdf","txt","csv","md"]';

-- Update existing settings record with default values if it exists
UPDATE Settings
SET
    AllowFileReading = 0,
    AllowUpdateCreatedFiles = 0,
    AllowCreateNewFiles = 0,
    NewFilesSavePath = '',
    AllowedFileExtensions = '["docx","xls","xlsx","pdf","txt","csv","md"]',
    UpdatedAt = datetime('now')
WHERE Id = 'app-settings-global';
