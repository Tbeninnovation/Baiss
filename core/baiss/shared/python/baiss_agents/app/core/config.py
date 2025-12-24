import os
from typing import Dict, Any, Optional
from dotenv import load_dotenv
import pathlib


from baiss_sdk import get_baiss_project_path
import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])
from baiss_sdk.db import DbProxyClient


env_path = get_baiss_project_path("core","baiss","shared","python",".env")

# Load environment variables from the .env file
load_dotenv(dotenv_path=env_path)



class Config:
    """
    Application configuration management.
    Handles environment variables and default settings.
    """


    @property
    def AGENT_CONFIGS(self) -> dict:
        # Correcting path to be relative to project root (desktop-app)
        return {
            "brain": {
                "system_prompt_path": get_baiss_project_path("core" , "baiss" , "shared" , "python" , "baiss_agents" , "app" , "system_prompt" ,"brain", "brain.md")
            },
            "data_expert": {
                "system_prompt_path": get_baiss_project_path("core" , "baiss" , "shared" , "python" , "baiss_agents" , "app" , "system_prompt" ,"data", "data.md")
            },
            "metadata": {
                "system_prompt_path": get_baiss_project_path("core" , "baiss" , "shared" , "python" , "baiss_agents" , "app" , "system_prompt" ,"metadata", "metadata.md")
            }
        }




    
# Create a global config instance
config = Config()

# do not remove the stop token, used in files.py
global_token = None
def init_global_token(token):
    global global_token
    global_token = False
    return global_token

def change_global_token(new_token = True):
    global global_token
    global_token = new_token
    return global_token

embedding_url = None
def init_embedding_url(url):
    global embedding_url
    embedding_url = url
    return embedding_url


# Global variables for caching
system_prompts: Dict[str, str] = {}


def initialize_config() -> Config:
    """
    Initialize and return the global configuration instance.

    Returns:
        Config: The initialized configuration instance
    """
    global config
    return config

def load_system_prompts() -> dict[str, str]:
    """Load all system prompts into memory at startup"""
    global system_prompts
    import logging
    logger = logging.getLogger(__name__)
    system_prompts.clear()  # Clear existing prompts

    for agent_name, agent_config in config.AGENT_CONFIGS.items():
         # Paths are relative to project root, so join with root_dir
         prompt_path = agent_config.get("system_prompt_path")
         if prompt_path:
            try:
                with open(prompt_path, "r", encoding="utf-8") as f:
                    logger.info(f"Loaded system prompt for agent '{agent_name}' from {prompt_path}")
                    system_prompts[agent_name] = f.read()
            except FileNotFoundError:
                raise RuntimeError(f"System prompt file not found for agent '{agent_name}': {prompt_path}")
            except IOError as e:
                raise RuntimeError(f"Error reading system prompt for agent '{agent_name}': {e}")

    return system_prompts


def db_client(db_type: str = "duckdb", **kwargs) -> DbProxyClient:
    """
    Factory function to create and return a database client instance based on the specified type.

    Args:
        db_type (str): The type of database client to create (default is "duckdb").
        **kwargs: Additional keyword arguments to pass to the database client constructor.
    Returns:
        DbProxyClient: An instance of the specified database client.
    """
    return DbProxyClient(base=db_type, **kwargs)



# # # # # # # # # # # # # # # # # # # # # # Settings # # # # # # # # # # # # # # # # # # # # # #

from pydantic_settings import BaseSettings
from pydantic import ConfigDict
from typing import Optional
from baiss_sdk import get_baiss_project_path
 
class Settings(BaseSettings):
    # API Configuration
    API_V1_STR: str = "/api/v1"
    PROJECT_NAME: str = "Baiss API"
    VERSION: str = "1.0.0"
    DESCRIPTION: str = """
    """
    # AWS Configuration
    AWS_REGION: str = "eu-west-3"
    ACCOUNT_ID: Optional[str] = None
    AWS_ACCESS_KEY_ID: Optional[str] = None
    AWS_SECRET_ACCESS_KEY: Optional[str] = None
    AWS_SESSION_TOKEN: Optional[str] = None
 
    # API Keys
    GEMINI_API_KEY: Optional[str] = None
 
    # Tools Configuration
    SANDBOX_URL: Optional[str] = "http://0.0.0.0:8080/api-code/sandbox"
 
    # GPU Configuration for Ollama
    OLLAMA_NUM_GPU: Optional[int] = 10  # Use only 1 GPU by default (0 for CPU-only)
    OLLAMA_GPU_MEMORY_FRACTION: Optional[float] = 0.2  # Use 60% of GPU memory by default
    OLLAMA_MAX_LOADED_MODELS: Optional[int] = 2  # Limit number of models loaded simultaneously
 
    client_type: str = "ollama"
    model_id: str = "qwen3:1.7b"
 
    client_type_vision: str = "None"
    model_id_vision: str = "None"
 
    @property
    def AGENT_CONFIGS(self) -> dict:
        return {
            "brain": {
                "primary": {
                    "client_type": self.client_type,
                    "model_id": self.model_id,  # Using GPU-limited variant
                    "temperature": 0.1,
                    "max_tokens": 5000
                },
                # core\baiss\shared\python\baiss_agents
                "system_prompt_path": get_baiss_project_path("core" , "baiss" , "shared" , "python" , "baiss_agents" , "app" , "system_prompt" , "brain" , "brain.md")
            },
            "data_expert": {
                "primary": {
                    "client_type": self.client_type,
                    "model_id": self.model_id,  # Using GPU-limited variant
                    "temperature": 0.1,
                    "max_tokens": 5000
                },
                "system_prompt_path": get_baiss_project_path("core" , "baiss" , "shared" , "python" , "baiss_agents" , "app" , "system_prompt" , "data" , "data.md")
            },
            "metadata": {
                "primary": {
                    "client_type": self.client_type,
                    "model_id": self.model_id,  # Using GPU-limited variant
                    "temperature": 0.1,
                    "max_tokens": 5000
                },
                "system_prompt_path": get_baiss_project_path("core" , "baiss" , "shared" , "python" , "baiss_agents" , "app" , "system_prompt" , "metadata" , "metadata.md")
            }
        }
 
    @property
    def TOOLS_CONFIG(self) -> dict:
        """Tools configuration for code execution"""
        if not self.SANDBOX_URL:
            raise ValueError("SANDBOX_URL environment variable must be set for tools configuration")
 
        return {
            "authorization": True,
            "url": self.SANDBOX_URL,
            "tools_list": {
                "tools": [
                    {
                        "toolSpec": {
                            "name": "execute_code",
                            "description": "\nExecute Python code and return the result.\n\nArgs:\n    code: Python code to execute\nReturns:\n    Dict with status, result, error, and execution_time\n",
                            "inputSchema": {
                                "json": {
                                    "properties": {
                                        "code": {
                                            "title": "Code",
                                            "type": "string"
                                        }
                                    },
                                    "required": [
                                        "code"
                                    ],
                                    "title": "execute_codeArguments",
                                    "type": "object"
                                }
                            }
                        }
                    }
                ]
            }
        }
 
# Global settings instance - initialized at startup
settings: Settings | None = None
# Global system prompts cache - loaded at startup
system_prompts: dict[str, str] | None = None
# Global client cache - initialized at startup
client_cache: dict[str, object] | None = None
 
def get_settings() -> Settings:
    if settings is None:
        init_settings()
    if settings is None:
        raise RuntimeError("Settings not initialized. Call init_settings() first.")
    return settings
 
def get_system_prompts() -> dict[str, str]:
    if system_prompts is None:
        raise RuntimeError("System prompts not loaded. Call load_system_prompts() first.")
    return system_prompts
 
def load_system_prompts() -> dict[str, str]:
     """Load all system prompts into memory at startup"""
     global system_prompts
     if system_prompts is None:
         settings = get_settings()
         system_prompts = {}
 
         for agent_name, config in settings.AGENT_CONFIGS.items():
             prompt_path = config.get("system_prompt_path")
             if prompt_path:
                try:
                    with open(prompt_path, "r", encoding="utf-8") as f:
                        system_prompts[agent_name] = f.read()
                except FileNotFoundError:
                    raise RuntimeError(f"System prompt file not found for agent '{agent_name}': {prompt_path}")
                except IOError as e:
                    raise RuntimeError(f"Error reading system prompt for agent '{agent_name}': {e}")
 
     return system_prompts
 
def init_settings() -> Settings:
    global settings
    if settings is None:
        settings = Settings()
    return settings
 
def get_client_cache() -> dict[str, object]:
    global client_cache
    if client_cache is None:
        client_cache = {}
    return client_cache
 
 
def get_tools_config() -> dict:
    """Get tools configuration from settings"""
    settings = get_settings()
    return settings.TOOLS_CONFIG
 