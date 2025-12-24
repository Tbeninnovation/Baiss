import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])

import logging
from typing import List, Dict, Any
from baiss_sdk.db.duck_db import DuckDb
from baiss_sdk.reranking.rerank import BaissReranker

class SearchPipeline:
    def __init__(self, db: DuckDb):
        self.db = db
        self._reranker = None

    @property
    def reranker(self):
        """Lazy load to speed up app startup"""
        if self._reranker is None:
            self._reranker = BaissReranker()
        return self._reranker

    def search(self, query_text: str, query_embedding: List[float], final_top_k: int = 5) -> List[Dict[str, Any]]:
        retrieval_k = 50 
        logging.info(f"Stage 1: Retrieving top {retrieval_k} candidates via Hybrid Search...")
        
        initial_results = self.db.hybrid_similarity_search(
            query_text=query_text,
            query_embedding=query_embedding,
            top_k=retrieval_k,
            cosine_weight=0.3, 
            score_threshold=0.0
        )

        if not initial_results:
            return []

        logging.info(f"Stage 2: Reranking {len(initial_results)} candidates via FlashRank...")
        
        return self.reranker.rerank(
            query=query_text,
            initial_results=initial_results,
            top_k=final_top_k
        )
        
        
if __name__ == "__main__":
    import random
    import os
    from baiss_sdk import get_baiss_project_path
    
    logging.basicConfig(level=logging.INFO)

    # --- CONFIGURATION ---

    TEST_DB_PATH = get_baiss_project_path("local-data","duckdb", "baiss.duckdb") 
    
    EMBEDDING_DIM = 384 

    print(f"--- Starting Pipeline Test ---")
    
    if not os.path.exists(TEST_DB_PATH):
        logging.warning(f"Database file not found at {TEST_DB_PATH}. Creating a new one or check path.")
    
    db = DuckDb(TEST_DB_PATH)
    db.connect()

    try:
        pipeline = SearchPipeline(db)

        query = "monte carlo tree search"
        print(f"Query: '{query}'")


        test_embedding = [random.random() for _ in range(EMBEDDING_DIM)]

        results = pipeline.search(
            query_text=query, 
            query_embedding=test_embedding, 
            final_top_k=3
        )

        print(f"\n--- Final Results ({len(results)}) ---")
        for i, res in enumerate(results):
            print(f"\n[Result {i+1}]")
            print(f"  Score (Cross-Encoder): {res.get('score', 0):.4f}")
            print(f"  Path: {res.get('path')}")
            print(f"  Content Preview: {res.get('content', '')[:100]}...")

    except Exception as e:
        logging.error(f"An error occurred during testing: {e}")
        import traceback
        traceback.print_exc()
    finally:
        db.disconnect()
        print(f"--- Pipeline Test Completed ---")        