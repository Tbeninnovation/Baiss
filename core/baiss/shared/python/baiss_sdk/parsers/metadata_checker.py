import json
import logging
import os
import threading
from typing import Dict, Optional

logger = logging.getLogger(__name__)

class MetadataChecker:
    """
    Simple class to check if a file has gotten its metadata or not.
    """
    
    def __init__(self, cache_file: str = "metadata_status.json"):
        """
        Initialize the metadata checker.
        
        Args:
            cache_file: File to store metadata status
        """
        self.cache_file = cache_file
        self._status: Dict[str, bool] = {}
        self._lock = threading.Lock()
        self._load_cache()
    
    def _load_cache(self):
        """Load metadata status from cache file."""
        with self._lock:
            try:
                with open(self.cache_file, 'r') as f:
                    self._status = json.load(f)
            except (FileNotFoundError, json.JSONDecodeError):
                self._status = {}
    
    def _save_cache(self):
        """Save metadata status to cache file."""
        with self._lock:
            try:
                with open(self.cache_file, 'w') as f:
                    json.dump(self._status, f, indent=2)
            except Exception as e:
                logger.error(f"Failed to save cache: {e}")
    
    def has_metadata(self, filepath: str) -> bool:
        """
        Check if a file has metadata.
        
        Args:
            filepath: Path to the file
            
        Returns:
            True if file has metadata, False otherwise
        """
        with self._lock:
            return self._status.get(filepath, False)
    
    def mark_metadata_success(self, filepath: str):
        """
        Mark that a file successfully got its metadata.
        
        Args:
            filepath: Path to the file
        """
        with self._lock:
            self._status[filepath] = True
        self._save_cache()
    
    def mark_metadata_failed(self, filepath: str):
        """
        Mark that a file failed to get its metadata.
        
        Args:
            filepath: Path to the file
        """
        with self._lock:
            self._status[filepath] = False
        self._save_cache()
    
    def check_metadata_response(self, filepath: str, response_data: dict) -> bool:
        """
        Check if metadata response is valid and mark status accordingly.
        
        Args:
            filepath: Path to the file
            response_data: Response from get_metadata endpoint
            
        Returns:
            True if metadata was successfully retrieved, False otherwise
        """
        try:
            if (response_data.get("success") and 
                response_data.get("result") and
                response_data["result"].get("metadata") and
                response_data["result"].get("general_description")):
                
                self.mark_metadata_success(filepath)
                return True
            else:
                self.mark_metadata_failed(filepath)
                return False
        except Exception as e:
            logger.error(f"Error checking metadata response for {filepath}: {e}")
            self.mark_metadata_failed(filepath)
            return False