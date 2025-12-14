-- Adds default RunPod provider models for hosted AI configuration
INSERT INTO Models (Id, Name, Type, Provider, Description, IsActive, CreatedAt)
SELECT 'runpod-generic-endpoint', 'RunPod Hosted Endpoint', 'hosted', 'runpod',
       'Generic RunPod Serverless endpoint. Configure Endpoint ID in Settings.', 1, CURRENT_TIMESTAMP
WHERE NOT EXISTS (
    SELECT 1 FROM Models WHERE Id = 'runpod-generic-endpoint'
);

INSERT INTO Models (Id, Name, Type, Provider, Description, IsActive, CreatedAt)
SELECT 'runpod-openai-compatible', 'RunPod OpenAI-Compatible Endpoint', 'hosted', 'runpod',
       'RunPod endpoint using OpenAI-compatible interface.', 0, CURRENT_TIMESTAMP
WHERE NOT EXISTS (
    SELECT 1 FROM Models WHERE Id = 'runpod-openai-compatible'
);

-- Ensure existing hosted settings with RunPod provider default to the generic endpoint if no model is set
UPDATE Settings
SET AIModelId = 'runpod-generic-endpoint'
WHERE AIModelType = 'hosted'
  AND (AIModelId IS NULL OR AIModelId = '')
  AND EXISTS (
        SELECT 1 FROM Models WHERE Id = 'runpod-generic-endpoint'
    );
