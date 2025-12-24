import os
import sys


import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])

import logging
from dotenv import load_dotenv
from typing import List, Dict, Any
from baiss_sdk.db.base_db import BaseDb
from baiss_sdk import get_baiss_project_path
import duckdb
import uuid
import time
import math
class DuckDb(BaseDb):
    def __init__(self, db_path: str, **kwargs):
        super().__init__()
        self.db_path = db_path
        self.connection = None
        self.embedding_dim = None 


    def connect(self):
        """Connect to the DuckDB database."""
        try:
            
            self.connection = duckdb.connect(self.db_path)
            
            self.connection.execute("INSTALL vss;")
            self.connection.execute("LOAD vss;")
            self.connection.execute("SET hnsw_enable_experimental_persistence = true;")
            logging.info(f"Connected to DuckDB database at {self.db_path}")
            logging.info("VSS extension loaded and HNSW experimental persistence enabled")
            if not self.check_tables_exist():
                self.create_db_and_tables()
            else:
                self._migrate_schema()
        except Exception as e:
            logging.error(f"Failed to connect to DuckDB database: {e}")
            raise

    def _migrate_schema(self):
        """
        Handles schema migrations for existing databases.
        Checks for missing columns and adds them safely.
        """
        try:
            # 1. Get list of existing columns in BaissDocuments
            columns_info = self.connection.execute("DESCRIBE BaissDocuments").fetchall()
            existing_columns = [col[0] for col in columns_info]

            # 2. Define the new column(s) you want to add
            new_columns = [
                ("processed", "BOOLEAN DEFAULT FALSE"),
            ]

            for col_name, col_def in new_columns:
                if col_name not in existing_columns:
                    logging.info(f"Migrating schema: Adding column '{col_name}' to BaissDocuments")
                    
                    # Wrap in transaction for safety
                    self.connection.execute("BEGIN TRANSACTION;")
                    try:
                        self.connection.execute(f"ALTER TABLE BaissDocuments ADD COLUMN {col_name} {col_def}")
                        self.connection.execute("COMMIT;")
                        logging.info(f"Successfully added column '{col_name}'")
                    except Exception as e:
                        self.connection.execute("ROLLBACK;")
                        raise e
                        
        except Exception as e:
            logging.error(f"Schema migration failed: {e}")
            raise

    def disconnect(self):
        """Disconnect from the DuckDB database."""
        if self.connection:
            self.connection.close()
            logging.info("Disconnected from DuckDB database")
            self.connection = None

    def execute_query(self, query: str) -> Any:
        """
        Execute a query against the DuckDB database.

        Args:
            query (str): The SQL query to execute.

        Returns:
            Any: The result of the query execution.
        """
        if not self.connection:
            raise ConnectionError("Database connection is not established.")

        try:
            result = self.connection.execute(query).fetchall()
            # logging.info(f"Executed query: {query}")
            return result
        except Exception as e:
            logging.error(f"Failed to execute query '{query}': {e}")
            raise

    def database_exists(self) -> bool:
        """Check if the database file exists."""
        return os.path.exists(self.db_path)

    def check_tables_exist(self) -> bool:
        """Check if the required tables exist in the database."""
        if not self.connection:
            raise ConnectionError("Database connection is not established.")
        if not self.database_exists():
            return False
        try:
            result = self.connection.execute("""show tables""").fetchall()
            for table in ["BaissDocuments", "BaissChunks"]:
                if (table,) not in result:
                    return False
            return True
        except Exception as e:
            logging.error(f"Failed to check tables existence: {e}")
            raise


    def create_db_and_tables(self):
        """Create the database and necessary tables if they do not exist."""
        # connect to the database if not already connected
        if not self.connection:
            raise ConnectionError("Database connection is not established.")
        if self.database_exists():
            if self.check_tables_exist():
                logging.info("Database and tables already exist.")
                return
        try:
            self.execute_query("""
                CREATE TABLE IF NOT EXISTS BaissDocuments (
                    id BIGINT PRIMARY KEY,
                    path TEXT,
                    hash TEXT,
                    depth INTEGER,
                    name TEXT,
                    type TEXT,
                    keywords JSON,
                    content_type TEXT,
                    last_modified TIMESTAMP,
                    processed BOOLEAN DEFAULT FALSE
                )
            """)
            self.execute_query("""
                CREATE TABLE IF NOT EXISTS BaissChunks (
                    id BIGINT PRIMARY KEY,
                    baiss_id BIGINT,
                    chunk_content TEXT,
                    embedding FLOAT[],
                    metadata JSON,
                    path TEXT,
                    keywords JSON,
                    content_type TEXT,
                    last_modified TIMESTAMP,
                    FOREIGN KEY (baiss_id) REFERENCES BaissDocuments(id)
                )
            """)

            logging.info("Database and tables created or verified successfully.")
        except Exception as e:
            raise ValueError(f"Failed to create database and tables: {e}")

    def _get_next_id(self, table: str) -> int:
        """Get the next available ID for the table."""
        try:
            result = self.connection.execute(f"SELECT MAX(id) FROM {table}").fetchone()
            max_id = result[0] if result and result[0] is not None else 0
            return max_id + 1
        except Exception as e:
            # If table doesn't exist or is empty, start from 1
            return 1

    def insert_rows(self, table: str, rows: List[Dict[str, Any]]):
        """Insert multiple rows into the specified table.
        Args:
            table (str): The name of the table to insert rows into.
            rows (List[Dict[str, Any]]): A list of dictionaries representing the rows to insert.
        Raises:
            ValueError: If the table name is not supported or if insertion fails.
        """
        if not self.connection:
            raise ConnectionError("Database connection is not established.")

        if table not in ["BaissDocuments", "BaissChunks"]:
            raise ValueError(f"Unsupported table: {table}")

        if not rows:
            logging.warning("No rows provided for insertion.")
            return

        try:
            # Get columns from the table schema
            column_info = self.connection.execute(f"DESCRIBE {table}").fetchall()
            columns = [col[0] for col in column_info]

            # Get the next available ID
            next_id = self._get_next_id(table)

            # Generate unique IDs if 'id' column exists but no ID is provided
            for i, row in enumerate(rows):
                if 'id' in columns and (row.get('id') is None or row.get('id') == ''):
                    row['id'] = next_id + i

            placeholders = ", ".join(["?"] * len(columns))
            column_names = ", ".join(columns)
            query = f"INSERT INTO {table} VALUES ({placeholders})"
            try:
                values = [[row.get(col) for col in columns] for row in rows]
            except Exception as e:
                logging.error(f"Error preparing values for insertion: {e}")
                raise

            self.connection.executemany(query, values)
            logging.info(f"Inserted {len(rows)} rows into {table}.")
        except Exception as e:
            logging.error(f"Failed to insert rows into {table}: {e}")
            raise
    
    def check_if_paths_exists(self, paths: List[str]):
        """Check if any of the given paths exist in the BaissDocuments table.
        Args:
            paths (List[str]): The list of paths to check.
        Returns:
            Dict[str, bool]: A dictionary mapping each path to a boolean indicating its existence.
        """
        
        if not self.connection:
            raise ConnectionError("Database connection is not established.")
        try:
            placeholders = ", ".join(["?"] * len(paths))
            query = f"SELECT path FROM BaissDocuments WHERE path IN ({placeholders})"
            result = self.connection.execute(query, paths).fetchall()
            existing_paths = {row[0] for row in result}
            path_existence = {path: (path in existing_paths) for path in paths}
            return path_existence
        except Exception as e:
            logging.error(f"Failed to check path existence: {e}")
            raise
    
    def check_if_path_exist_or_changed(self, path: str, file_hash: str) -> bool:
        """Check if the given path exists in the BaissDocuments table and if its hash has changed.
        Args:
            path (str): The path to check.
            file_hash (str): The hash of the file to compare.
        Returns:
            bool: True if the path exists and the hash matches, False otherwise.
        """
        if not self.connection:
            raise ConnectionError("Database connection is not established.")
        try:
            query = "SELECT hash FROM BaissDocuments WHERE path = ?"
            result = self.connection.execute(query, [path]).fetchone()
            if result:
                existing_hash = result[0]
                return existing_hash == file_hash
            else:
                return False
        except Exception as e:
            logging.error(f"Failed to check path existence or hash change: {e}")
            return False

    def retrieve_unprocessed_files(self, extensions: List[str]) -> List[tuple]:
        """Retrieve files that have not been processed (i.e., no corresponding chunks).
        Returns:
            List[tuple]: A list of tuples representing unprocessed files.
        Raises:
            ValueError: If retrieval fails.
        """
        if not self.connection:
            raise ConnectionError("Database connection is not established.")
        allowed_extensions = ["csv", "pdf", "xlsx", "xls", "txt", "docx", "md"]
        for ext in extensions:
            if ext not in allowed_extensions:
                raise ValueError(f"Unsupported extension: {ext}. Supported extensions are: {allowed_extensions}")

        try:
            placeholders = ", ".join(["?"] * len(extensions))
            query = f"""
                SELECT bd.path, bd.id, bd.content_type FROM BaissDocuments bd
                WHERE bd.content_type IN ({placeholders}) and bd.processed is FALSE
            """

            result = self.connection.execute(query, extensions).fetchall()
            paths = [(row[0], row[1], row[2]) for row in result]
            logging.info(f"Retrieved {paths} unprocessed files for extensions: {extensions}")
            return paths
        except Exception as e:
            logging.error(f"Failed to retrieve unprocessed files: {e}")
            raise


    def delete_by_paths(self, paths: List[str]):
        """Delete documents and chunks by paths.
        Args:
            paths (List[str]): The list of paths to delete documents and chunks for.
        Raises:
            ValueError: If deletion fails.
        """
        if not self.connection:
            raise ConnectionError("Database connection is not established.")

        try:

            # First delete chunks (to avoid foreign key constraint issues)
            self.connection.executemany("DELETE FROM BaissChunks WHERE path LIKE ?", [[f"%{path}%"] for path in paths])

            # Then delete documents
            self.connection.executemany("DELETE FROM BaissDocuments WHERE path LIKE ?", [[f"%{path}%"] for path in paths])

            logging.info(f"Deleted records for paths: {paths}")
        except Exception as e:
            logging.error(f"Failed to delete records for paths {paths}: {e}")
            raise ValueError(f"Failed to delete records for paths {paths}: {e}")

    def delete_by_extensions(self, extensions: List[str]):
        """Delete documents and chunks by file extensions.
        Args:
            extensions (List[str]): The list of file extensions to delete documents and chunks for.
        """
        if not self.connection:
            raise ConnectionError("Database connection is not established.")

        allowed_extensions = ["csv", "pdf", "xlsx", "xls", "txt", "docx", "md" ]
        for ext in extensions:
            if ext not in allowed_extensions:
                raise ValueError(f"Unsupported extension: {ext}. Supported extensions are: {allowed_extensions}")
        try:
            # Create placeholders for the extensions
            placeholders = ", ".join(["?"] * len(extensions))

            # First delete chunks (to avoid foreign key constraint issues)
            self.connection.execute(f"DELETE FROM BaissChunks WHERE content_type IN ({placeholders})", extensions)

            # Then delete documents
            self.connection.execute(f"DELETE FROM BaissDocuments WHERE content_type IN ({placeholders})", extensions)

            logging.info(f"Deleted records for extensions: {extensions}")
        except Exception as e:
            logging.error(f"Failed to delete records for extensions {extensions}: {e}")
            raise ValueError(f"Failed to delete records for extensions {extensions}: {e}")

    def get_all_paths_wo_embeddings(self) -> List[str]:
        """Get all paths from the BaissDocuments table that do not have corresponding embeddings in BaissChunks.
        Returns:
            List[str]: A list of paths without embeddings.
        """
        if not self.connection:
            raise ConnectionError("Database connection is not established.")
        try:
            query = """
                SELECT DISTINCT bd.id, bd.chunk_content FROM BaissChunks bd
                WHERE bd.embedding IS NULL
            """
            result = self.connection.execute(query).fetchall()
            resu = [(row[0], row[1]) for row in result]
            return resu
        except Exception as e:
            logging.error(f"Failed to retrieve paths without embeddings: {e}")
            raise

    def get_chunks_by_paths(self, paths: List[str]) -> List[Dict[str, Any]]:
        """Get all chunks for a given document path.
        Args:
            paths (List[str]): The document paths to retrieve chunks for.
        Returns:
            List[Dict[str, Any]]: A list of dictionaries representing the chunks.
        """
        if not self.connection:
            raise ConnectionError("Database connection is not established.")
        try:
            placeholders = ", ".join(["?"] * len(paths))
            query = f"SELECT DISTINCT bc.path, bc.id, bc.chunk_content FROM BaissChunks bc WHERE bc.path IN ({placeholders})"
            result = self.connection.execute(query, paths).fetchall()
            data = [{"path": row[0], "id": row[1], "chunk_content": row[2]} for row in result]
            return data
        except Exception as e:
            logging.error(f"Failed to retrieve chunks for paths {paths}: {e}")
            raise
    
    def check_if_path_in_chunks_and_delete(self, path: str):
        """Check if the given path exists in the BaissChunks table and delete corresponding chunks if found.
        Args:
            path (str): The document path to check and delete chunks for.
        """
        if not self.connection:
            raise ConnectionError("Database connection is not established.")
        try:
            query = "SELECT COUNT(*) FROM BaissChunks WHERE path = ?"
            result = self.connection.execute(query, [path]).fetchone()
            count = result[0] if result else 0
            if count > 0:
                self.connection.execute("DELETE FROM BaissChunks WHERE path = ?", [path])
                logging.info(f"Deleted {count} chunks for path: {path}")
        except Exception as e:
            logging.error(f"Failed to check and delete chunks for path {path}: {e}")
            raise


    def fill_in_missing_embeddings(self, id: str, embedding: List[float]):
        """Fill missing embeddings for chunks corresponding to the given document path.
        Args:
            id (str): The document path to fill embeddings for.
            embedding (List[float]): The embedding vector to fill in.
        """
        if not self.connection:
            raise ConnectionError("Database connection is not established.")
        try:
            # Convert embedding to float32 to match FLOAT[] column type (avoids DOUBLE[] cast error)
            embedding_float32 = [float(x) for x in embedding]
            query = "UPDATE BaissChunks SET embedding = ? WHERE id = ? AND embedding IS NULL"
            self.connection.execute(query, [embedding_float32, id])
            logging.info(f"Filled missing embeddings for id: {id}")
        except Exception as e:
            logging.error(f"Failed to fill missing embeddings for id {id}: {e}")
            raise
    
    def get_all_paths(self) -> List[str]:
        """Get all paths from the BaissDocuments table.
        Returns:
            List[str]: A list of all document paths.
        """
        if not self.connection:
            raise ConnectionError("Database connection is not established.")
        try:
            query = "SELECT DISTINCT path FROM BaissDocuments"
            result = self.connection.execute(query).fetchall()
            paths = [row[0] for row in result]
            return paths
        except Exception as e:
            logging.error(f"Failed to retrieve all document paths: {e}")
            raise

    def update_document_processed_status(self, path: str, processed: bool):
        """Update the processed status of a document.
        Args:
            path (str): The document path to update.
            processed (bool): The processed status to set.
        """
        if not self.connection:
            raise ConnectionError("Database connection is not established.")
        try:
            query = "UPDATE BaissDocuments SET processed = ? WHERE path = ?"
            self.connection.execute(query, [processed, path])
            logging.info(f"Updated processed status for path: {path} to {processed}")
        except Exception as e:
            logging.error(f"Failed to update processed status for path {path}: {e}")
            raise

    def setup_extensions(self):
        """Setup required DuckDB extensions for similarity search."""
        if not self.connection:
            raise ConnectionError("Database connection is not established.")
        
        try:
            # Enable experimental HNSW persistence FIRST
            self.connection.execute("SET hnsw_enable_experimental_persistence = true;")
            
            
            # Install and load FTS extension for BM25
            self.connection.execute("INSTALL fts;")
            self.connection.execute("LOAD fts;")
            
            logging.info("Extensions (vss, fts) loaded successfully with HNSW persistence enabled")
        except Exception as e:
            logging.error(f"Failed to load extensions: {e}")
            raise

    def create_hnsw_index(self, force_recreate=False):
        """Create HNSW index on embeddings for faster similarity search."""
        if not self.connection:
            raise ConnectionError("Database connection is not established.")
        
        try:
            # Check if index already exists
            #indexes = self.connection.execute("SELECT index_name FROM duckdb_indexes() WHERE index_name = 'hnsw_embedding_idx';").fetchall()
            
            #if indexes and not force_recreate:
            #    logging.info("HNSW index already exists")
            #    return
                
            #if indexes and force_recreate:
            #    self.connection.execute("DROP INDEX IF EXISTS hnsw_embedding_idx;")
            
            # Create HNSW index with cosine distance
            #self.connection.execute("""
            #    CREATE INDEX hnsw_embedding_idx 
            #    ON BaissChunks 
            #    USING HNSW (embedding) 
            #    WITH (metric = 'cosine');
            #""")
            logging.info("Skipping HNSW index creation (using direct vector similarity)")
        except Exception as e:
            logging.error(f"Failed to create HNSW index: {e}")
            raise

    def create_fts_index(self, force_recreate=False):
        """Create FTS index on chunk content for BM25 search."""
        if not self.connection:
            raise ConnectionError("Database connection is not established.")
        
        try:
            # Check if FTS index already exists
            index_exists = False
            try:
                self.connection.execute("SELECT 1 FROM fts_main_BaissChunks.docs LIMIT 1;")
                index_exists = True
            except:
                pass  # Index doesn't exist

            if index_exists and not force_recreate:
                # Validate the index works with the current DuckDB version
                try:
                    # Try a dummy BM25 search to ensure macro and table references are correct
                    self.connection.execute("SELECT fts_main_BaissChunks.match_bm25(id, 'test', fields := 'chunk_content') FROM BaissChunks LIMIT 1;")
                    logging.info("FTS index already exists and is valid")
                    return
                except Exception as e:
                    logging.warning(f"FTS index exists but validation failed: {e}. Recreating index...")
                    force_recreate = True
            
            if force_recreate:
                try:
                    self.connection.execute("PRAGMA drop_fts_index('BaissChunks');")
                except Exception as e:
                    logging.warning(f"Failed to drop FTS index during recreation: {e}")
            
            # Create FTS index on chunk_content
            self.connection.execute("""
                PRAGMA create_fts_index(
                    'BaissChunks', 'id', 'chunk_content',
                    stemmer = 'none',
                    stopwords = 'none',
                    ignore = '',
                    strip_accents = 0,
                    lower = 1,
                    overwrite = 0
                );
            """)
            logging.info("FTS index created successfully")
        except Exception as e:
            logging.error(f"Failed to create FTS index: {e}")
            raise

    def similarity_search_bm25(self, query: str, top_k: int = 5, score_threshold: float = 0.0):
        """
        Perform BM25 text similarity search using FTS index.
        """
        if not self.connection:
            raise ConnectionError("Database connection is not established.")
        
        start_time = time.time()
        
        try:
            # Escape single quotes in query to prevent SQL injection and syntax errors
            escaped_query = query.replace("'", "''").lower()
            
            search_query = f"""
                SELECT 
                    bc.chunk_content,
                    bc.path,
                    fts_main_BaissChunks.match_bm25(id, '{escaped_query}', fields := 'chunk_content') as bm25_score,
                    bc.id,
                    bc.metadata
                FROM BaissChunks bc
                WHERE bm25_score IS NOT NULL AND bm25_score > 0
                ORDER BY bm25_score DESC
                LIMIT {top_k};
            """
            logging.info(f"Executing BM25 search query: {escaped_query}")
            # execute_query returns a list directly
            result = self.execute_query(search_query)
    
            logging.info(f"BM25 search returned {len(result)} results")
            
            # Handle encoding issues and filter by score
            filtered_results = []
            for row in result:
                try:
                    content = row[0]
                    path = row[1]
                    score = float(row[2])
                    chunk_id = row[3]
                    metadata = row[4]
                    
                    # Clean content if it has encoding issues
                    if isinstance(content, bytes):
                        content = content.decode('utf-8', errors='replace')
                    elif isinstance(content, str):
                        content = content.encode('utf-8', errors='replace').decode('utf-8', errors='replace')
                    
                    # Clean path similarly
                    if isinstance(path, bytes):
                        path = path.decode('utf-8', errors='replace')
                    elif isinstance(path, str):
                        path = path.encode('utf-8', errors='replace').decode('utf-8', errors='replace')
                    
                    if score >= score_threshold:
                        filtered_results.append((content, path, score, chunk_id, metadata))
                        
                except Exception as e:
                    logging.warning(f"Skipping row due to encoding issue: {e}")
                    continue
                
            end_time = time.time()
            logging.info(f"BM25 search took {end_time - start_time:.4f} seconds")
            return filtered_results
        except Exception as e:
            logging.error(f"Failed to perform BM25 search: {e}")
            raise

    def similarity_search_cosine(self, query_embedding: List[float], top_k: int = 5, score_threshold: float = 0.0):
        """
        Perform cosine similarity search using direct vector operations.
        Uses DuckDB's VSS extension with array_cosine_distance for proper similarity scoring.
        Optimized with Late Materialization and Dimension Caching.
        """
        if not self.connection:
            raise ConnectionError("Database connection is not established.")
        
        start_time = time.time()
        
        try:
            # 1. Dimension Caching Strategy
            if self.embedding_dim is None:
                # Try to detect dimension from existing data
                sample_result = self.connection.execute("""
                    SELECT len(embedding) as dim
                    FROM BaissChunks 
                    WHERE embedding IS NOT NULL 
                    LIMIT 1;
                """).fetchall()
                
                if sample_result:
                    self.embedding_dim = sample_result[0][0]
                else:
                    # Fallback: use query dimension if DB is empty
                    self.embedding_dim = len(query_embedding)
            
            db_dimension = self.embedding_dim
            query_dimension = len(query_embedding)
            
            # Adjust query embedding to match database dimension
            if query_dimension != db_dimension:
                if query_dimension > db_dimension:
                    query_embedding = query_embedding[:db_dimension]
                    logging.info(f"Truncated query embedding from {query_dimension} to {db_dimension}")
                else:
                    query_embedding = query_embedding + [0.0] * (db_dimension - query_dimension)
                    logging.info(f"Padded query embedding from {query_dimension} to {db_dimension}")
            
            # 2. Late Materialization Query
            # Step A: Calculate scores and find top IDs (Lightweight)
            # Step B: Join to get content for winners only (Heavy)
            search_query = f"""
                WITH TopChunks AS (
                    SELECT 
                        id,
                        (1.0 - array_cosine_distance(embedding::FLOAT[{db_dimension}], ?::FLOAT[{db_dimension}])) as similarity_score
                    FROM BaissChunks 
                    WHERE embedding IS NOT NULL
                    ORDER BY similarity_score DESC
                    LIMIT ?
                )
                SELECT 
                    bc.chunk_content,
                    bc.path,
                    tc.similarity_score,
                    bc.id,
                    bc.metadata
                FROM TopChunks tc
                JOIN BaissChunks bc ON tc.id = bc.id
                WHERE tc.similarity_score >= ?
                ORDER BY tc.similarity_score DESC;
            """
            
            # Execute with parameters: embedding, top_k, score_threshold
            result = self.connection.execute(search_query, [query_embedding, top_k, score_threshold]).fetchall()
            
            # Process results
            filtered_results = []
            for row in result:
                try:
                    content = row[0]
                    path = row[1]
                    score = float(row[2])
                    chunk_id = row[3]
                    metadata = row[4]
                    
                    # Clean encoding issues
                    if isinstance(content, bytes):
                        content = content.decode('utf-8', errors='replace')
                    if isinstance(path, bytes):
                        path = path.decode('utf-8', errors='replace')
                    
                    # Threshold check is handled in SQL, but keeping for safety/consistency
                    if score >= score_threshold:
                        filtered_results.append((content, path, score, chunk_id, metadata))
                            
                except Exception as e:
                    logging.warning(f"Skipping row due to issue: {e}")
                    continue
            
            end_time = time.time()
            logging.info(f"Cosine search took {end_time - start_time:.4f} seconds")
            return filtered_results
                
        except Exception as e:
            logging.error(f"Failed to perform cosine similarity search: {e}")
            raise

    def hybrid_similarity_search(self, query_text: str, query_embedding: List[float], top_k: int = 5, 
                            cosine_weight: float = 0.3, score_threshold: float = 0.0, k: int = 60):
        """
        Perform hybrid similarity search using Z-Score Normalization with Sigmoid.
        This method improves upon Min-Max normalization by being more robust to outliers
        and score distribution differences between BM25 and Cosine similarity.
        
        Args:
            query_text: Text query for BM25 search
            query_embedding: Embedding vector for cosine similarity search
            top_k: Number of top results to return
            cosine_weight: Weight for cosine similarity (0.0 to 1.0). BM25 weight will be (1.0 - cosine_weight).
            score_threshold: Minimum hybrid score threshold for filtering results
            k: Multiplier for fetching candidates (default 60)
            
        Returns:
            List of tuples: (content, path, hybrid_score, chunk_id, metadata, cosine_score, bm25_score)
        """
        start_time = time.time()
        try:
            # Fetch a larger pool of candidates to calculate statistics
            limit = top_k * k
            cosine_results = self.similarity_search_cosine(query_embedding, limit, 0.0)
            bm25_results = self.similarity_search_bm25(query_text, limit, 0.0)
            
            cosine_scores_map = {chunk_id: score for _, _, score, chunk_id, _ in cosine_results}
            bm25_scores_map = {chunk_id: score for _, _, score, chunk_id, _ in bm25_results}
            
            all_chunk_ids = set(cosine_scores_map.keys()) | set(bm25_scores_map.keys())
            
            if not all_chunk_ids:
                return []

            def get_stats(scores):
                if not scores:
                    return 0.0, 1.0
                mean = sum(scores) / len(scores)
                variance = sum((x - mean) ** 2 for x in scores) / len(scores)
                std_dev = math.sqrt(variance)
                return mean, std_dev

            def sigmoid(x):
                return 1 / (1 + math.exp(-x))

            c_scores = list(cosine_scores_map.values())
            b_scores = list(bm25_scores_map.values())
            
            c_mean, c_std = get_stats(c_scores)
            b_mean, b_std = get_stats(b_scores)
            
            min_c = min(c_scores) if c_scores else 0.0
            min_b = min(b_scores) if b_scores else 0.0
            
            doc_data = {}
            for content, path, _, chunk_id, metadata in cosine_results:
                doc_data[chunk_id] = {'content': content, 'path': path, 'metadata': metadata}
            for content, path, _, chunk_id, metadata in bm25_results:
                if chunk_id not in doc_data:
                    doc_data[chunk_id] = {'content': content, 'path': path, 'metadata': metadata}
            
            results = []
            
            for chunk_id in all_chunk_ids:
                c_raw = cosine_scores_map.get(chunk_id, min_c)
                b_raw = bm25_scores_map.get(chunk_id, min_b)
                
                # Normalize Cosine Score (Z-Score + Sigmoid)
                if c_std > 1e-6:
                    c_norm = sigmoid((c_raw - c_mean) / c_std)
                else:
                    c_norm = 0.5 
                
                # Normalize BM25 Score (Z-Score + Sigmoid)
                if b_std > 1e-6:
                    b_norm = sigmoid((b_raw - b_mean) / b_std)
                else:
                    b_norm = 0.5
                
                hybrid_score = (cosine_weight * c_norm) + ((1.0 - cosine_weight) * b_norm)
                
                if hybrid_score >= score_threshold:
                    results.append((
                        doc_data[chunk_id]['content'],
                        doc_data[chunk_id]['path'],
                        hybrid_score,
                        chunk_id,
                        doc_data[chunk_id]['metadata'],
                        c_raw,
                        b_raw
                    ))
            
            results.sort(key=lambda x: x[2], reverse=True)
            
            logging.info(f"Hybrid search (Z-Score) returned {len(results[:top_k])} results")
            
            end_time = time.time()
            logging.info(f"Hybrid similarity search took {end_time - start_time:.4f} seconds")
            
            return results[:top_k]
            
        except Exception as e:
            logging.error(f"Failed to perform hybrid similarity search: {e}")
            raise


if __name__ == "__main__":
    pass