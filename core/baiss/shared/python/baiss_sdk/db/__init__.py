import os
import sys

import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])
from typing import List, Dict, Any
from baiss_sdk.db.base_db import BaseDb
from baiss_sdk.db.duck_db import DuckDb
from baiss_sdk import get_baiss_project_path


class DbProxyClient(BaseDb):
    def __init__(self, base: str = "duckdb", **kwargs):
        # super().__init__()
        base = str(base).strip().lower()
        
        path = get_baiss_project_path("local-data", base)
        if not os.path.exists(path):
            os.makedirs(path, exist_ok=True)
        if ("duckdb" in base):
            db_name = "baiss.duckdb"
            db_path = os.path.join(path, db_name)
            self._client = DuckDb(db_path=db_path, **kwargs)
        else:
            raise ValueError(f"Unsupported database type: {base}")
        
    def name(self) -> str:
        """
        Return the name of the underlying database client.
        This method should be overridden by subclasses if needed.
        """
        return self._client.__class__.__name__

    def connect(self):
        return self._client.connect()
    
    def disconnect(self):
        return self._client.disconnect()

    def execute_query(self, query: str):
        return self._client.execute_query(query)

    def create_db_and_tables(self):
        return self._client.create_db_and_tables()
    
    def insert_rows(self, table: str, rows: List[Dict[str, Any]]):
        return self._client.insert_rows(table, rows)
    
    def delete_by_paths(self, paths: List[str]):
        return self._client.delete_by_paths(paths)

    def retrieve_unprocessed_files(self, extensions: List[str]) -> List[tuple]:
        return self._client.retrieve_unprocessed_files(extensions)

    def delete_by_extensions(self, extensions: List[str]):
        return self._client.delete_by_extensions(extensions)
    
    def get_all_paths_wo_embeddings(self) -> List[str]:
        return self._client.get_all_paths_wo_embeddings()
    
    def get_chunks_by_paths(self, paths: List[str]) -> List[Dict[str, Any]]:
        return self._client.get_chunks_by_paths(paths)

    def fill_in_missing_embeddings(self, id: str, embedding: List[float]):
        return self._client.fill_in_missing_embeddings(id, embedding)

    
    def setup_extensions(self):
        return self._client.setup_extensions()

    def create_hnsw_index(self, force_recreate=False):
        return self._client.create_hnsw_index(force_recreate)

    def create_fts_index(self, force_recreate=False):
        return self._client.create_fts_index(force_recreate)

    def similarity_search_cosine(self, query_embedding, top_k=5, score_threshold=0.0):
        return self._client.similarity_search_cosine(query_embedding, top_k, score_threshold)

    def similarity_search_bm25(self, query, top_k=5, score_threshold=0.0):
        return self._client.similarity_search_bm25(query, top_k, score_threshold)

    def hybrid_similarity_search(self, query_text, query_embedding, top_k=5, 
                               cosine_weight=0.3, score_threshold=0.0, k=60):
        return self._client.hybrid_similarity_search(query_text, query_embedding, top_k, 
                                              cosine_weight, score_threshold, k)
    def check_if_paths_exists(self, paths: List[str]):
        return self._client.check_if_paths_exists(paths)

    def check_if_path_exist_or_changed(self, path: str, file_hash: str) -> bool:
        return self._client.check_if_path_exist_or_changed(path, file_hash)
    
    def get_all_paths(self) -> List[str]:
        return self._client.get_all_paths()
    
    def update_document_processed_status(self, path: str, processed: bool):
        return self._client.update_document_processed_status(path, processed)
    
    def check_if_path_in_chunks_and_delete(self, path: str):
        return self._client.check_if_path_in_chunks_and_delete(path)

if __name__ == "__main__":
    db_client = DbProxyClient(base="duckdb")
    db_client.create_db_and_tables()
