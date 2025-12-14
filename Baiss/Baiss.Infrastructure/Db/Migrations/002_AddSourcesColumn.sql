-- Add Sources column to Messages table
-- This migration adds support for storing source information with assistant messages

ALTER TABLE Messages ADD COLUMN Sources TEXT;

-- Index for source queries (optional, for performance if sources are frequently queried)
CREATE INDEX IX_Messages_Sources ON Messages(Sources);
