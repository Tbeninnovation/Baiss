-- Remove default Databricks models to allow user to add them manually
-- This migration removes all default Databricks models from the database

-- Remove all existing Databricks models (they will be added manually by user)
DELETE FROM Models WHERE Provider = 'databricks';

-- Also remove any other default hosted models that should be manually added
-- Keep only essential local models
DELETE FROM Models WHERE Provider = 'databricks' OR Provider = 'runpod';

-- Update the embedding models added in previous migration since they're removed
-- (User will add them manually with explicit chat/embedding purpose)