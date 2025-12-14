import os
import sys


import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])

import logging
from dotenv import load_dotenv
from typing import List, Dict, Any


class BaseDb:
    """
    BaseDb is a base class for database interactions.
    It should be extended by specific database implementations.
    """

    def __init__(self):
        pass

    def connect(self):
        """
        Connect to the database.
        This method should be implemented by subclasses.
        """
        raise NotImplementedError("Subclasses must implement this method.")

    def disconnect(self):
        """
        Disconnect from the database.
        This method should be implemented by subclasses.
        """
        raise NotImplementedError("Subclasses must implement this method.")

    def execute_query(self, query: str) -> Any:
        """
        Execute a query against the database.
        This method should be implemented by subclasses.

        Args:
            query (str): The SQL query to execute.

        Returns:
            Any: The result of the query execution.
        """
        raise NotImplementedError("Subclasses must implement this method.")
    
    def create_db_and_tables(self):
        """
        Create the database and necessary tables if they do not exist.
        This method should be implemented by subclasses.
        """
        raise NotImplementedError("Subclasses must implement this method.")

    def insert_rows(self, table: str, rows: List[Dict[str, Any]]):
        """
        Insert multiple rows into a specified table.

        Args:
            table (str): The name of the table to insert rows into.
            rows (List[Dict[str, Any]]): A list of dictionaries representing the rows to insert.
        """
        raise NotImplementedError("Subclasses must implement this method.")

    def delete_by_paths(self, paths: List[str]):
        """
        Delete rows from a specified table based on the 'path' field.

        Args:
            table (str): The name of the table to delete rows from.
            rows (List[Dict[str, Any]]): A list of dictionaries representing the rows to delete.
        """
        raise NotImplementedError("Subclasses must implement this method.")

    def retrieve_unprocessed_files(self, extensions: List[str]) -> List[tuple]:
        """
        Retrieve files with specified extensions that have not been processed yet.

        Args:
            extensions (List[str]): A list of file extensions to filter by.

        Returns:
            List[tuple]: A list of tuples containing the paths and IDs of unprocessed files.
        """
        raise NotImplementedError("Subclasses must implement this method.")
    
    def delete_by_extensions(self, extensions: List[str]):
        """
        Delete rows from a specified table based on the 'path' field.

        Args:
            table (str): The name of the table to delete rows from.
            rows (List[Dict[str, Any]]): A list of dictionaries representing the rows to delete.
        """
        raise NotImplementedError("Subclasses must implement this method.")
    
    def get_all_paths_wo_embeddings(self) -> List[str]:
        """
        Get all paths from the table that do not have embeddings.

        Returns:
            List[str]: A list of paths without embeddings.
        """
        raise NotImplementedError("Subclasses must implement this method.")
    
    def get_chunks_by_paths(self, paths: List[str]) -> List[Dict[str, Any]]:
        """
        Get all chunks from the table that match the given paths.

        Args:
            paths (List[str]): A list of file paths to filter by.

        Returns:
            List[Dict[str, Any]]: A list of dictionaries representing the chunks that match the given paths.
        """
        raise NotImplementedError("Subclasses must implement this method.")
    

    def fill_in_missing_embeddings(self, id: str, embedding: List[float]):
        """
        Fill in missing embeddings for a specific row identified by its ID.

        Args:
            id (str): The ID of the row to update.
            embedding (List[float]): The embedding vector to fill in.
        """
        raise NotImplementedError("Subclasses must implement this method.")
    

    def setup_extensions(self):
        """Setup required database extensions for similarity search."""
        raise NotImplementedError("Subclasses must implement this method.")

    def create_hnsw_index(self, force_recreate=False):
        """Create HNSW index for vector similarity search."""
        raise NotImplementedError("Subclasses must implement this method.")

    def create_fts_index(self, force_recreate=False):
        """Create FTS index for text search."""
        raise NotImplementedError("Subclasses must implement this method.")

    def similarity_search_cosine(self, query_embedding: List[float], top_k: int = 5, score_threshold: float = 0.0):
        """Perform cosine similarity search."""
        raise NotImplementedError("Subclasses must implement this method.")

    def similarity_search_bm25(self, query: str, top_k: int = 5, score_threshold: float = 0.0):
        """Perform BM25 text similarity search."""
        raise NotImplementedError("Subclasses must implement this method.")

    def hybrid_similarity_search(self, query_text: str, query_embedding: List[float], top_k: int = 5, 
                            k: int = 2, score_threshold: float = 0.0):
        """Perform hybrid similarity search combining cosine and BM25."""
        raise NotImplementedError("Subclasses must implement this method.")
    
    def check_if_paths_exists(self, paths: List[str]):
        """Check which paths exist in the database.

        Args:
            paths (List[str]): A list of file paths to check.

        Returns:
            List[str]: A list of paths that exist in the database.
        """
        raise NotImplementedError("Subclasses must implement this method.")

    def check_if_path_exist_or_changed(self, path: str, file_hash: str) -> bool:
        """Check if the given path exists in the BaissDocuments table and if its hash has changed.
        Args:
            path (str): The path to check.
            file_hash (str): The hash of the file to compare.
        Returns:
            bool: True if the path exists and the hash matches, False otherwise.
        """
        raise NotImplementedError("Subclasses must implement this method.")
    def get_all_paths(self) -> List[str]:
        """Get all paths from the BaissDocuments table.
        Returns:
            List[str]: A list of all document paths.
        """
        raise NotImplementedError("Subclasses must implement this method.")
    def update_document_processed_status(self, path: str, processed: bool):
        """Update the processed status of a document.
        Args:
            path (str): The document path to update.
            processed (bool): The processed status to set.
        """
        raise NotImplementedError("Subclasses must implement this method.")
    
    def check_if_path_in_chunks_and_delete(self, path: str):
        """Check if the given path exists in the BaissChunks table and delete corresponding chunks if found.
        Args:
            path (str): The document path to check and delete chunks for.
        """
        raise NotImplementedError("Subclasses must implement this method.")