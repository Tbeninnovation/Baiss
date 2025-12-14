import os
import sys
import json
import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])
from typing import Optional, List, Dict
from baiss_sdk.parsers.pdf_extractor import PDFParser
from baiss_sdk.db import DbProxyClient
from baiss_sdk.files.embeddings import Embeddings
from datetime import datetime
import logging
class PdfTreeStructure:

    @staticmethod
    def read_structure(structure_path: str) -> Dict[str, Dict]:
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
            except Exception as e:
                # If both fail, try cp1252 (Windows default) with error handling
                with open(structure_path, "r", encoding="cp1252", errors="ignore") as f:
                    structure = json.load(f)
                return structure

    @staticmethod
    def save_pdf_structure(structure: Dict[str, Dict], structure_path: str) -> None:
        with open(structure_path, "w", encoding="utf-8") as f:
            json.dump(structure, f, indent=4, ensure_ascii=False)
        return True


    @staticmethod
    async def update_pdf_tree_structure_v2(path: str, id: str, content_type: str, db_client: DbProxyClient):

        rows = []
        from baiss_agents.app.core.config import global_token,  embedding_url
        if global_token == True:
            raise Exception("Global token set to True, operation aborted.")
        db_client.check_if_path_in_chunks_and_delete(path)
        try:
            pdf_parser = PDFParser()
            embedding = Embeddings(url = embedding_url)
            try:
                parsed_document = pdf_parser.parse_pdf(path)
            except Exception as e:
                print(f"Error parsing PDF document at {path}: {e}")
                db_client.update_document_processed_status(path, True)
                return
            for page in parsed_document:
                # logging.info(f"page content: {page.keys()}")
                for chunk in page["chunks"]:
                    metadata = {
                            "page_number": page["page_number"],
                            "token_count": chunk["token_count"]
                        }
                    rows.append({
                            "baiss_id": id,
                            "chunk_content": chunk["full_text"],
                            "embedding": await embedding.embed(chunk["full_text"]),
                            "metadata": metadata,
                            "path": path,
                            "keywords": None, # TODO: add function to extract keywords
                            "content_type": content_type,
                            "last_modified": datetime.now()
                        })
            if rows:
                db_client.insert_rows("BaissChunks", rows)
                db_client.update_document_processed_status(path, True)
        except Exception as e:
            print(f"Error updating PDF tree structure for file {path}: {e}")

if __name__ == "__main__":
    pass
