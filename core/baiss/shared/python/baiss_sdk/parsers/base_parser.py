from typing import List, Dict, Any
import pandas as pd
import re
from abc import ABC, abstractmethod

import baisstools
baisstools.insert_syspath(__file__, matcher=[r"^baiss_.*$"])

from baiss_sdk.parsers import num_tokens_from_string


class BaseParser(ABC):
    """
    A base parser with common methods for handling structured and unstructured data,
    and a built-in search functionality.
    """
    def __init__(self):
        """Initializes the parser and a simple in-memory cache for parsed files."""
        self._cache = {}

    @abstractmethod
    def parse(self, file_path: str, max_tokens_per_chunk: int) -> List[Dict[str, Any]]:
        """
        Parses a file and returns a list of chunks. This must be implemented by subclasses.
        """
        pass

    def search(self, file_path: str, query: str, max_tokens_per_chunk: int = 500, case_sensitive: bool = False, use_regex: bool = False) -> List[Dict[str, Any]]:
        """
        Searches for a query within a file by parsing it into chunks. It returns the metadata
        for every block and chunk where the query is found.

        Args:
            file_path (str): The path to the file to search.
            query (str): The string or regex pattern to search for.
            max_tokens_per_chunk (int): The token limit for chunking during parsing.
            case_sensitive (bool): Whether the search should be case-sensitive.
            use_regex (bool): Whether to treat the query as a regular expression.

        Returns:
            A list of metadata dictionaries for each chunk containing the query.
        """
        if not query:
            return []

        # Use cache if available, otherwise parse the file and cache the result.
        if file_path not in self._cache:
            print(f"Parsing and caching '{file_path}' for search...")
            self._cache[file_path] = self.parse(file_path, max_tokens_per_chunk=max_tokens_per_chunk)
        
        all_chunks = self._cache[file_path]
        found_locations = []

        for chunk in all_chunks:
            content = chunk.get("content", "")
            match_found = False
            try:
                if use_regex:
                    flags = re.IGNORECASE if not case_sensitive else 0
                    if re.search(query, content, flags):
                        match_found = True
                else:
                    # Standard string search
                    search_content = content if case_sensitive else content.lower()
                    search_query = query if case_sensitive else query.lower()
                    if search_query in search_content:
                        match_found = True
            except re.error as e:
                print(f"Regex error on query '{query}': {e}")
                return [] # Stop searching on invalid regex

            if match_found:
                found_locations.append(chunk["metadata"])
        
        return found_locations

    @staticmethod
    def is_structured(df: pd.DataFrame, nan_threshold: float = 0.6, string_col_threshold: float = 0.8) -> bool:
        """
        Determines if a DataFrame represents structured data using several heuristics.

        A DataFrame is considered structured if it passes these checks:
        1.  Low NaN Percentage: The ratio of NaN values to total cells is below `nan_threshold`.
        2.  Header Quality: The column headers are not default integer indices (e.g., 0, 1, 2...).
        3.  Data Type Consistency: A significant portion of columns can be interpreted as non-string
            (numeric, datetime, etc.), suggesting typed data rather than free-form text.
        """
        if df.empty or df.shape[0] < 2:
            return False

        # 1. NaN Percentage Check
        total_cells = df.size
        nan_count = df.isnull().sum().sum()
        nan_ratio = nan_count / total_cells
        if nan_ratio > nan_threshold:
            print(f"DEBUG: Unstructured due to high NaN ratio: {nan_ratio:.2f}")
            return False

        # 2. Header Quality Check
        # Check if columns are default integer headers assigned by pandas
        if all(isinstance(c, int) for c in df.columns):
            print("DEBUG: Unstructured due to default integer headers.")
            return False

        # 3. Data Type Consistency Check
        # Attempt to infer better dtypes on a copy of the data below the header
        df_body = df.iloc[1:].copy()
        try:
            df_inferred = df_body.infer_objects()
            # Try converting to numeric, coercing errors to NaN
            for col in df_inferred.columns:
                df_inferred[col] = pd.to_numeric(df_inferred[col], errors='coerce')
            df_inferred = df_inferred.infer_objects() # Re-infer after numeric conversion
        except Exception:
            # If conversion fails, treat as mostly string data
            df_inferred = df_body

        string_like_cols = sum(1 for dtype in df_inferred.dtypes if pd.api.types.is_string_dtype(dtype) or pd.api.types.is_object_dtype(dtype))
        total_cols = len(df_inferred.columns)

        if total_cols > 0:
            string_col_ratio = string_like_cols / total_cols
            if string_col_ratio >= string_col_threshold:
                print(f"DEBUG: Potentially unstructured due to high string column ratio: {string_col_ratio:.2f}")
                # This is a strong indicator, but we can combine it with other checks.
                # For now, if most columns are strings, we'll lean towards unstructured.
                return False

        return True
        
    def _calculate_row_tokens(self, data_rows: List[List[str]], header_with_separator: str) -> List[Dict[str, Any]]:
        """
        Calculate tokens for each row including header overhead.
        Assumes data_rows are lists of cell strings.
        
        Returns:
            List of dictionaries with row content and token counts.
        """
        header_tokens = num_tokens_from_string(header_with_separator)
        
        row_data = []
        for i, row_cells in enumerate(data_rows):
            row_str = "| " + " | ".join(map(str, row_cells)) + " |"
            row_tokens = num_tokens_from_string(row_str)
            total_tokens_with_header = header_tokens + row_tokens
            
            row_data.append({
                "row_index": i + 1,
                "content": row_str,
                "row_tokens": row_tokens,
                "total_tokens_with_header": total_tokens_with_header
            })
        
        return row_data

    def _create_chunks_by_tokens(self, header_with_separator: str, row_data: List[Dict[str, Any]], 
                                max_tokens: int = 1000, sheet_name: str = "Sheet1") -> List[Dict[str, Any]]:
        """
        Create chunks based on token limits while preserving the table header.
        
        Args:
            header_with_separator: Table header with markdown separator.
            row_data: List of row data with token information.
            max_tokens: Maximum tokens per chunk.
        
        Returns:
            List of chunk dictionaries with metadata.
        """
        chunks = []
        if not row_data:
            return chunks

        current_chunk_rows = []
        header_tokens = num_tokens_from_string(header_with_separator)
        current_chunk_tokens = header_tokens
        
        for row_info in row_data:
            if (current_chunk_tokens + row_info["row_tokens"]) > max_tokens and current_chunk_rows:
                chunk_content = header_with_separator + "\n" + "\n".join(current_chunk_rows)
                chunks.append({
                    "content": chunk_content,
                    "tokens": current_chunk_tokens,
                    "sheet_name": sheet_name
                })
                
                current_chunk_rows = [row_info["content"]]
                current_chunk_tokens = header_tokens + row_info["row_tokens"]
            else:
                current_chunk_rows.append(row_info["content"])
                current_chunk_tokens += row_info["row_tokens"]
        
        if current_chunk_rows:
            chunk_content = header_with_separator + "\n" + "\n".join(current_chunk_rows)
            chunks.append({
                "content": chunk_content,
                "tokens": current_chunk_tokens,
                "sheet_name": sheet_name
            })
        
        return chunks

    def _create_chunks_from_text(self, text: str, max_tokens: int, source: str, sheet_name: str = None) -> List[Dict[str, Any]]:
        """
        Create chunks from raw text based on token limits or paragraph breaks (two empty lines).
        
        Args:
            text: The raw text content.
            max_tokens: Maximum tokens per chunk.
            source: The file path of the document.
            sheet_name: The name of the sheet (optional).
            
        Returns:
            List of chunk dictionaries with metadata.
        """
        chunks = []
        lines = text.splitlines()
        current_chunk_lines = []
        current_chunk_tokens = 0
        empty_line_count = 0

        def create_chunk():
            nonlocal current_chunk_lines, current_chunk_tokens
            if not current_chunk_lines:
                return
            
            chunk_content = "\n".join(current_chunk_lines)
            chunks.append({
                "content": chunk_content,
                "tokens": current_chunk_tokens,
                "source": source,
                "sheet_name": sheet_name,
            })
            current_chunk_lines = []
            current_chunk_tokens = 0

        for line in lines:
            if not line.strip():
                empty_line_count += 1
                continue

            line_tokens = num_tokens_from_string(line)
            
            # If there was a significant paragraph break (2+ empty lines), create a new chunk.
            if empty_line_count >= 2 and current_chunk_lines:
                create_chunk()

            empty_line_count = 0

            # If adding the new line exceeds max_tokens, create a new chunk.
            if current_chunk_tokens + line_tokens > max_tokens and current_chunk_lines:
                create_chunk()

            current_chunk_lines.append(line)
            current_chunk_tokens += line_tokens

        # Add the last remaining chunk
        create_chunk()
            
        return chunks