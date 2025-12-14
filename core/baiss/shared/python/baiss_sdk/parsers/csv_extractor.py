import pandas as pd
import os
from typing import List, Dict, Any
import sys
import numpy as np
from baiss_sdk.files.file_reader import FileReader



import baisstools
baisstools.insert_syspath(__file__, matcher=[r"^baiss_.*$"])

from baiss_sdk.parsers.base_parser import BaseParser

class CSVParser(BaseParser):
    """
    A CSV parser that reads a file, detects data blocks separated by empty lines,
    determines if each block is structured, and splits it into chunks.
    """
    def __init__(self):
        """Initializes the parser."""
        super().__init__()
        print("Block-based CSV Parser initialized.")

    def parse(self, file_path: str, max_tokens_per_chunk: int = 500) -> List[Dict[str, Any]]:
        """
        Parses a CSV file, detects data blocks, determines if each is structured,
        and splits it into chunks.
        """
        file_path = FileReader.update_file_path(file_path)
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"CSV file not found at: {file_path}")

        try:
            # Read the entire CSV, keeping blank lines to detect blocks
            df = pd.read_csv(file_path, sep=None, engine='python', on_bad_lines='skip', header=None, skip_blank_lines=False)

            if df.empty:
                return []

            # Find indices of rows that are entirely empty (our block separators)
            empty_row_indices = df[df.isnull().all(axis=1)].index.tolist()

            # Split the DataFrame into blocks based on the empty rows
            data_blocks = []
            start_idx = 0
            for end_idx in empty_row_indices:
                block_df = df.iloc[start_idx:end_idx]
                if not block_df.empty:
                    data_blocks.append(block_df)
                start_idx = end_idx + 1

            # Add the last block if the file doesn't end with an empty line
            last_block_df = df.iloc[start_idx:]
            if not last_block_df.empty:
                data_blocks.append(last_block_df)

            all_results = []
            # print(f"Found {len(data_blocks)} data block(s) in CSV file '{file_path}'.")

            for block_index, block_df in enumerate(data_blocks):
                # Clean up and prepare the block DataFrame
                block_df.dropna(how='all', inplace=True)
                block_df.dropna(how='all', axis=1, inplace=True)
                block_df.reset_index(drop=True, inplace=True)

                if block_df.empty:
                    continue

                # Promote the first row to header
                block_df.columns = block_df.iloc[0]
                block_df = block_df[1:].reset_index(drop=True)

                # print(f"  - Processing block {block_index} with shape {block_df.shape}")
                is_block_structured = self.is_structured(block_df)
                chunks = []

                if is_block_structured:
                    print(f"    Block {block_index} is structured. Processing as a table.")
                    # Re-add header for markdown conversion
                    df_with_header = pd.concat([pd.DataFrame([block_df.columns], columns=block_df.columns), block_df], ignore_index=True)
                    markdown_content = df_with_header.to_markdown(index=False, tablefmt="pipe")

                    if markdown_content and markdown_content.strip():
                        markdown_lines = markdown_content.split('\n')
                        header_line = markdown_lines[0]
                        separator_line = markdown_lines[1]
                        header_with_separator = f"{header_line}\n{separator_line}"

                        data_rows = [row.strip().split('|')[1:-1] for row in markdown_lines[2:]]
                        data_rows_cleaned = [[cell.strip() for cell in row] for row in data_rows]

                        row_token_data = self._calculate_row_tokens(data_rows_cleaned, header_with_separator)
                        chunks = self._create_chunks_by_tokens(header_with_separator, row_token_data, max_tokens_per_chunk, sheet_name="CSV")
                else:
                    print(f"    Block {block_index} is unstructured. Processing as text.")
                    raw_text = block_df.to_string(index=False, header=False)
                    chunks = self._create_chunks_from_text(raw_text, max_tokens_per_chunk, source=file_path, sheet_name="CSV")

                for i, chunk in enumerate(chunks):
                    all_results.append({
                        "content": chunk["content"],
                        "metadata": {
                            "source": file_path,
                            "block_index": block_index,
                            "chunk_index": i,
                            "tokens": chunk["tokens"],
                            "is_structured": is_block_structured
                        }
                    })
            return all_results
        except Exception as e:
            raise ValueError(f"Failed to parse CSV file: {e}")

# Example usage
if __name__ == "__main__":
    pass
