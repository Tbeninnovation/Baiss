-- Migration 012 (simplified): Drop legacy AIModelId column only.
-- Prerequisites: AIChatModelId and AIEmbeddingModelId columns already added by earlier migrations (008).
-- NOTE: SQLite added ALTER TABLE DROP COLUMN support in 3.35. If running on older
-- SQLite, this will fail; in that case skip (leave AIModelId unused) or perform manual table recreate.

-- If column already gone, this will raise an error. DbUp does not support conditional DROP COLUMN easily.
-- Accept one-time execution. If failure indicates 'no such column', mark migration as applied manually.

ALTER TABLE Settings DROP COLUMN AIModelId;

-- End simplified Migration 012