
import os
import sys
import json
import baisstools

# Assuming baisstools and baiss_sdk are in the path
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])

from typing import Optional, List, Dict
# Import the CSVParser from its location
from baiss_sdk.parsers.csv_extractor import CSVParser
from baiss_sdk.db import DbProxyClient
from baiss_sdk.files.embeddings import Embeddings
from datetime import datetime
import logging
class CsvTreeStructure:
    """
    A class to read a JSON file structure, parse CSV files found within it,
    and update the structure with the parsed content.
    """

    @staticmethod
    def read_structure(structure_path: str) -> Dict[str, Dict]:
        """
        Reads a JSON structure file with robust encoding fallbacks.
        """
        try:
            # Try UTF-8 first
            with open(structure_path, "r", encoding="utf-8") as f:
                structure = json.load(f)
            return structure
        except UnicodeDecodeError:
            # Fallback to latin-1 if UTF-8 fails
            try:
                with open(structure_path, "r", encoding="latin-1") as f:
                    structure = json.load(f)
                return structure
            except Exception:
                # If both fail, try cp1252 (Windows default) with error handling
                with open(structure_path, "r", encoding="cp1252", errors="ignore") as f:
                    structure = json.load(f)
                return structure

    @staticmethod
    def save_csv_structure(structure: Dict[str, Dict], structure_path: str) -> bool:
        """
        Saves the updated structure dictionary to a JSON file.
        """
        with open(structure_path, "w", encoding="utf-8") as f:
            json.dump(structure, f, indent=4, ensure_ascii=False)
        return True

    @staticmethod
    def update_csv_tree_structure(structure: Dict[str, Dict]) -> Dict[str, Dict]:
        """
        Iterates through the structure, finds CSV files, parses them,
        and adds the parsed chunks back into the structure.
        """
        if "files" in structure:
            files_structure = structure["files"]
        else:
            files_structure = structure

        for filename, fileinfo in list(files_structure.items()):
            print(f"Processing: {filename}")
            try:
                # Check for file type and the correct CSV content type
                if fileinfo.get("type") == "file" and (fileinfo.get("content_type") in ["text/csv", "application/vnd.ms-excel"] or filename.lower().endswith('.csv')):
                    csv_parser = CSVParser()
                    # The parse method returns the chunks directly
                    parsed_document = csv_parser.parse(filename)
                    files_structure[filename]["chunks"] = parsed_document
            except Exception as e:
                print(f"Error updating CSV tree structure for file {filename}: {e}")
                continue

        structure["files"] = files_structure
        return structure

    @staticmethod
    async def update_csv_tree_structure_v2(path: str, id: str, content_type: str, db_client: DbProxyClient):

        rows = []
        from baiss_agents.app.core.config import global_token,  embedding_url
        if global_token == True:
            raise Exception("Global token set to True, operation aborted.")
        db_client.check_if_path_in_chunks_and_delete(path)
        try:
            csv_parser = CSVParser()
            embedding = Embeddings(url = embedding_url)
            try:
                parsed_document = csv_parser.parse(path)
            except Exception as e:
                print(f"Error parsing CSV document at {path}: {e}")
                db_client.update_document_processed_status(path, True)
                return
            for row in parsed_document:
                rows.append({
                    "baiss_id": id,
                    "chunk_content": row["content"],
                    "embedding": await embedding.embed(row["content"]),
                    "metadata": row["metadata"],
                    "path": path,
                    "keywords": None,  # TODO: add function to extract keywords
                    "content_type": content_type,
                    "last_modified": datetime.now()
                    })
            db_client.insert_rows("BaissChunks", rows)
            db_client.update_document_processed_status(path, True)
        except Exception as e:
            print(f"Error updating CSV tree structure for file {path}: {e}")


if __name__ == "__main__":
    pass


