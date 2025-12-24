import json
from typing import Dict, Any
import os
import sys
import multiprocessing

# Required for multiprocessing on macOS/Windows
if __name__ == "__main__":
    multiprocessing.freeze_support()

sys.path.insert(0, 
	os.path.dirname(
		os.path.dirname(
            os.path.dirname(
				os.path.abspath(__file__)
			)
		)
	)
)

import json
from baiss_sdk.search.pipeline import SearchPipeline
from baiss_sdk.sandbox.python_sandbox import PythonSandbox
from baiss_sdk.files.embeddings import Embeddings
import logging
from baiss_sdk.db import DbProxyClient
logger = logging.getLogger(__name__)

class allTools:
    def __init__(self, url_embedding: str = None):
        self.url_embedding    = url_embedding


    async def searchlocaldocuments(self, query: str, k: int = 5, search_type="hybrid") -> list[dict[str, Any]]:
        """
        """
        if self.url_embedding is None:
            search_type = "bm25"
        

        db_client = DbProxyClient()
        db_client.connect()
        
        # Setup extensions
        db_client.setup_extensions()
        # Create FTS index if not exists
        db_client.create_fts_index()
        

        if search_type == "hybrid":
            query_embedding = await Embeddings(url = self.url_embedding).embed(query)

            search_pipeline = SearchPipeline(db_client)

             # Perform hybrid search
            results = search_pipeline.search(
                query_text=query,
                query_embedding=query_embedding,
                final_top_k=k
            )
            logger.info(f"Hybrid search returned {results} results.")
            # exit(0)
            
            # Format results
            formatted_results = [
                {
                    "chunk_content": result["chunk_content"],
                    "path": result["path"] if not result["path"].startswith("file://") else result["path"][7:],
                    "score": result["score"],
                    "id": result["id"],
                    "metadata": result["metadata"],
                    # "cosine_score": result["cosine_score"],
                    # "bm25_score": result["bm25_score"],
                    # "search_type": "hybrid"
                }
                for result in results
            ]
        elif search_type == "bm25":
            results = db_client.similarity_search_bm25(
                query=query,
                top_k=k
            )
            
            
            # Format results
            formatted_results = [
                {
                    "chunk_content": result[0],
                    "path": result[1] if not result[1].startswith("file://") else result[1][7:],
                    "score": result[2],
                    "id": result[3],
                    "metadata": result[4],
                    # "search_type": "bm25"
                }
                for result in results
            ]
        else:
            raise ValueError(f"Unsupported search type: {search_type}")
        

        db_client.disconnect()

        # formatted_results = [
        #     {
        #         "chunk_content": "test",
        #         "path": "test",
        #         "score": 0,
        #         "id": "test",
        #         "metadata": {},
        #     }
        # ]

        return formatted_results


if __name__ == "__main__":
    sandbox = PythonSandbox()

    sandbox.add_tool_reference(
        name="searchlocaldocuments",
        module_path="baiss_sdk.tools",
        class_name="allTools",
        method_name="searchlocaldocuments",
        init_kwargs={"url_embedding": "http://127.0.0.1:8081"}
    )

    result = sandbox.execute("""
import asyncio

async def main():
    result = await searchlocaldocuments(query="pokemon")
    print(json.dumps(result, indent=2))

asyncio.run(main())
""", timeout=30)

    print(json.dumps(result, indent=2))
