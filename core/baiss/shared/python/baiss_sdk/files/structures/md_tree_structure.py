import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])
from baiss_sdk.parsers import extract_chunks as extract_chunks_from_plain_txt
from baiss_sdk.files.file_reader import FileReader
from baiss_sdk.db import DbProxyClient
from baiss_sdk.files.embeddings import Embeddings
from datetime import datetime
import logging

logger = logging.getLogger(__name__)

class MdTreeStructure:

    @staticmethod
    async def update_md_tree_structure_v2(path: str, id: str, content_type: str, db_client: DbProxyClient):
        """
        Reads a .md file, splits it into chunks, and inserts the content into the database.
        """
        rows = []
        from baiss_agents.app.core.config import global_token,  embedding_url
        if global_token == True:
            raise Exception("Global token set to True, operation aborted.")
        db_client.check_if_path_in_chunks_and_delete(path)
        try:
            # Read the content of the markdown file
            try:
                file_content = FileReader(path).content.decode("utf-8", errors="ignore")
            except Exception as e:
                print(f"Error reading markdown file at {path}: {e}")
                db_client.update_document_processed_status(path, True)
                return
            embedding = Embeddings(url = embedding_url)
            # Split the content into chunks using the existing function
            chunks = extract_chunks_from_plain_txt(file_content)

            for chunk_text in chunks:
                if not chunk_text:
                    continue
                chunk_embedding = await embedding.embed(chunk_text["full_text"])
                
                rows.append({
                    "baiss_id": id,
                    "chunk_content": chunk_text["full_text"],
                    "embedding": chunk_embedding,
                    "metadata": {"token_count": chunk_text["token_count"]}, # No specific metadata like page numbers for MD
                    "path": path,
                    "keywords": None, # To be added later
                    "content_type": content_type,
                    "last_modified": datetime.now()
                })
            
            if rows:
                db_client.insert_rows("BaissChunks", rows)
                db_client.update_document_processed_status(path, True)
            else:
                logger.warning(f"No chunks were extracted from file: {path}")

        except Exception as e:
            logger.error(f"Error updating markdown tree structure for file {path}: {e}", exc_info=True)