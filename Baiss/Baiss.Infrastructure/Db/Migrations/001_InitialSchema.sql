-- Initial database schema for Baiss application
-- This migration creates the foundational tables for conversations, messages, and settings

-- Conversations table
CREATE TABLE Conversations (
    Id TEXT PRIMARY KEY,
    Title TEXT NOT NULL,
    CreatedByUserId TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT
);

-- Messages table
CREATE TABLE Messages (
    Id TEXT PRIMARY KEY,
    ConversationId TEXT NOT NULL,
    SenderType TEXT NOT NULL DEFAULT 'USER',
    Content TEXT NOT NULL,
    SentAt TEXT NOT NULL,
    FOREIGN KEY (ConversationId) REFERENCES Conversations(Id) ON DELETE CASCADE
);

-- Settings table for application configuration
CREATE TABLE Settings (
    Performance INTEGER NOT NULL DEFAULT 0,
    AllowedPaths TEXT NOT NULL DEFAULT '[]',
    AllowedApplications TEXT NOT NULL DEFAULT '[]',
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT
);

-- Create indexes for better performance
CREATE INDEX IX_Conversations_CreatedByUserId ON Conversations(CreatedByUserId);
CREATE INDEX IX_Messages_ConversationId ON Messages(ConversationId);
CREATE INDEX IX_Messages_SenderType ON Messages(SenderType);
CREATE INDEX IX_Messages_SentAt ON Messages(SentAt);

-- Insert default settings
INSERT INTO Settings (Performance, AllowedPaths, AllowedApplications, CreatedAt) VALUES
    (0, '[]', '[]', datetime('now'));
