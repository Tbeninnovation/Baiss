-- Add AI Model configuration settings
-- This migration adds AI model type and model ID to Settings table and creates Models table

-- Add AI model configuration columns to Settings table
ALTER TABLE Settings ADD COLUMN AIModelType TEXT NOT NULL DEFAULT 'local';
ALTER TABLE Settings ADD COLUMN AIModelId TEXT NOT NULL DEFAULT '';

-- Create Models table to store available AI models
CREATE TABLE Models (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Type TEXT NOT NULL, -- 'local' or 'hosted'
    Provider TEXT NOT NULL, -- 'python', 'openai', 'anthropic', 'databricks', etc.
    Description TEXT,
    IsActive INTEGER NOT NULL DEFAULT 1,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT
);

-- Create index for better performance
CREATE INDEX IX_Models_Type ON Models(Type);
CREATE INDEX IX_Models_Provider ON Models(Provider);
CREATE INDEX IX_Models_IsActive ON Models(IsActive);

-- Insert default models
INSERT INTO Models (Id, Name, Type, Provider, Description, IsActive, CreatedAt) VALUES
    -- Local models (Python-based)
    ('local-python-default', 'Local AI Model', 'local', 'python', 'Default local AI model via Python FastAPI', 1, datetime('now')),
    ('ollama-qwen3-1.7b', 'Qwen 3 1.7B', 'local', 'ollama', 'Qwen 3 1.7B via Ollama', 1, datetime('now')),

    -- Hosted models
    ('openai-gpt-4', 'GPT-4', 'hosted', 'openai', 'OpenAI GPT-4 model', 1, datetime('now')),
    ('openai-gpt-4-turbo', 'GPT-4 Turbo', 'hosted', 'openai', 'OpenAI GPT-4 Turbo model', 1, datetime('now')),
    ('openai-gpt-3.5-turbo', 'GPT-3.5 Turbo', 'hosted', 'openai', 'OpenAI GPT-3.5 Turbo model', 1, datetime('now')),

    ('anthropic-claude-3-sonnet', 'Claude 3 Sonnet', 'hosted', 'anthropic', 'Anthropic Claude 3 Sonnet model', 1, datetime('now')),
    ('anthropic-claude-3-haiku', 'Claude 3 Haiku', 'hosted', 'anthropic', 'Anthropic Claude 3 Haiku model', 1, datetime('now')),

    ('databricks-gemma-3-12b', 'Databricks Gemma 3 12B', 'hosted', 'databricks', 'Databricks hosted Gemma 3 12B model', 1, datetime('now')),

    ('azure-gpt-4', 'Azure GPT-4', 'hosted', 'azure', 'Azure OpenAI GPT-4 model', 1, datetime('now')),
    ('azure-gpt-35-turbo', 'Azure GPT-3.5 Turbo', 'hosted', 'azure', 'Azure OpenAI GPT-3.5 Turbo model', 1, datetime('now'));

-- Update existing settings to use local model by default
UPDATE Settings SET AIModelType = 'local', AIModelId = 'local-python-default' WHERE AIModelType IS NULL OR AIModelType = '';