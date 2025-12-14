
import os
import sys
import json
import baisstools

# Assuming baisstools and baiss_sdk are in the path
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])

from typing import Optional, List, Dict
# Import the ExcelParser from its assumed location
from baiss_sdk.parsers.excel_extractor import ExcelParser
from baiss_sdk.db import DbProxyClient
from baiss_sdk.files.embeddings import Embeddings
from datetime import datetime

class ExcelTreeStructure:
    """
    A class to read a JSON file structure, parse Excel files found within it,
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
    def save_excel_structure(structure: Dict[str, Dict], structure_path: str) -> bool:
        """
        Saves the updated structure dictionary to a JSON file.
        """
        with open(structure_path, "w", encoding="utf-8") as f:
            json.dump(structure, f, indent=4, ensure_ascii=False)
        return True

    @staticmethod
    def update_excel_tree_structure(structure: Dict[str, Dict]) -> Dict[str, Dict]:
        """
        Iterates through the structure, finds Excel files, parses them,
        and adds the parsed chunks back into the structure.
        """
        if "files" in structure:
            files_structure = structure["files"]
        else:
            files_structure = structure

        # Common MIME types for Excel files (.xlsx and .xls)
        excel_content_types = [
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.ms-excel"
        ]

        for filename, fileinfo in list(files_structure.items()):
            print(f"Processing: {filename}")
            try:
                # Check for file type and if content_type is one of the Excel types
                if fileinfo.get("type") == "file" and fileinfo.get("content_type") in excel_content_types:
                    excel_parser = ExcelParser()
                    # The parse method should return the chunks directly
                    parsed_document = excel_parser.parse(filename)
                    files_structure[filename]["chunks"] = parsed_document
            except Exception as e:
                print(f"Error updating Excel tree structure for file {filename}: {e}")
                continue

        structure["files"] = files_structure
        return structure

    @staticmethod
    async def update_excel_tree_structure_v2(path: str, id: str, content_type: str, db_client: DbProxyClient):

        rows = []
        from baiss_agents.app.core.config import global_token,  embedding_url
        if global_token == True:
            raise Exception("Global token set to True, operation aborted.")
        db_client.check_if_path_in_chunks_and_delete(path)
        try:
            excel_parser = ExcelParser()
            embedding = Embeddings(url=embedding_url)
            try:
                parsed_document = excel_parser.parse(path)
            except Exception as e:
                print(f"Error parsing Excel document at {path}: {e}")
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
            print(f"Error updating Excel tree structure for file {path}: {e}")


if __name__ == "__main__":
    pass
