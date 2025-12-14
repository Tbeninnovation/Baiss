-- Migration 013: Add AIModelProviderScope column to Settings table
-- Supports persisting third radio button state (local | hosted | databricks)

-- SQLite older versions (and some builds used via Microsoft.Data.Sqlite) don't support
-- "ALTER TABLE ... ADD COLUMN IF NOT EXISTS". DbUp guarantees this script runs only once,
-- so we can safely add the column without the IF NOT EXISTS guard.
ALTER TABLE Settings ADD COLUMN AIModelProviderScope TEXT DEFAULT 'local';

-- Backfill existing rows: if AIModelType = 'hosted' AND Databricks models previously used, you may choose to set to 'databricks'.
-- For now we leave defaults to 'local' to avoid incorrect assumptions.
