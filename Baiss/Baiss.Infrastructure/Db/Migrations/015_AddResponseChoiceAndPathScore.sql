-- Migration 015: Add ResponseChoice and SearchPathScore tables
-- These tables store search response data with paths and scores for each message

-- ResponseChoice table
CREATE TABLE ResponseChoices (
    Id TEXT PRIMARY KEY,
    MessageId TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    FOREIGN KEY (MessageId) REFERENCES Messages(Id) ON DELETE CASCADE
);

-- SearchPathScore table
CREATE TABLE SearchPathScores (
    Id TEXT PRIMARY KEY,
    Path TEXT NOT NULL,
    Score REAL NOT NULL,
    ResponseChoiceId TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    FOREIGN KEY (ResponseChoiceId) REFERENCES ResponseChoices(Id) ON DELETE CASCADE
);

-- Add ResponseChoiceId to Messages table
ALTER TABLE Messages ADD COLUMN ResponseChoiceId TEXT;

-- Create indexes for better performance
CREATE INDEX IX_ResponseChoices_MessageId ON ResponseChoices(MessageId);
CREATE INDEX IX_SearchPathScores_ResponseChoiceId ON SearchPathScores(ResponseChoiceId);
CREATE INDEX IX_SearchPathScores_Score ON SearchPathScores(Score);
CREATE INDEX IX_Messages_ResponseChoiceId ON Messages(ResponseChoiceId);
