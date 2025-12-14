import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])
from baiss_sdk.parsers.TextDoc_extractor import TextDocumentParser
from baiss_sdk.db import DbProxyClient
from datetime import datetime
import logging
from baiss_sdk.files.embeddings import Embeddings
logger = logging.getLogger(__name__)

class TextTreeStructure:

    @staticmethod
    async def update_text_tree_structure(path: str, id: str, content_type: str, db_client: DbProxyClient):
        """
        Parses a .txt or .docx file, and inserts its content as chunks into the database.
        """
        rows = []
        from baiss_agents.app.core.config import global_token,  embedding_url
        if global_token == True:
            raise Exception("Global token set to True, operation aborted.")
        db_client.check_if_path_in_chunks_and_delete(path)
        try:
            parser = TextDocumentParser()
            embedding = Embeddings(url = embedding_url)
            # The parser returns a list of "pages", each containing chunks.
            try:
                parsed_document = parser.parse(path)
            except Exception as e:
                logger.error(f"Error parsing document at {path}: {e}", exc_info=True)
                db_client.update_document_processed_status(path, True)
                return

            for page in parsed_document:
                page_number = page.get("page_number", 1)

                for chunk_text in page.get("chunks", []):
                    if not chunk_text:
                        continue
                    
                    metadata = {
                        "page_number": page_number,
                        # token_count is not available in TextDocumentParser, so we omit it or estimate it
                    }
                    rows.append({
                        "baiss_id": id,
                        "chunk_content": chunk_text,
                        "embedding": await embedding.embed(chunk_text),
                        "metadata": metadata,
                        "path": path,
                        "keywords": None,
                        "content_type": content_type,
                        "last_modified": datetime.now()
                    })
            
            if rows:
                db_client.insert_rows("BaissChunks", rows)
                db_client.update_document_processed_status(path, True)
            else:
                logger.warning(f"No chunks were extracted from file: {path}")

        except Exception as e:
            logger.error(f"Error updating text tree structure for file {path}: {e}", exc_info=True)
            