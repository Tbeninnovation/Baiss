import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])

import logging
import os
from typing import List, Dict, Any, Tuple
from baiss_sdk import get_baiss_project_path


class BaissReranker:
    def __init__(self, model_name: str = "ms-marco-MiniLM-L-12-v2"):
        """
        Initializes FlashRank.
        """
        try:
            from flashrank import Ranker, RerankRequest
            self.RerankRequest = RerankRequest
            
            cache_dir = get_baiss_project_path("local-data", "models")

            logging.info(f"Loading FlashRank model: {model_name}...")
            self.ranker = Ranker(model_name=model_name, cache_dir=cache_dir)
            logging.info("FlashRank model loaded successfully.")
        except ImportError:
            logging.error("FlashRank not found. Please install: pip install flashrank")
            raise

    def rerank(self, query: str, initial_results: List[Tuple], top_k: int = 5) -> List[Dict[str, Any]]:
        if not initial_results:
            return []

        passages = []
        for i, res in enumerate(initial_results):
            passages.append({
                "id": str(res[3]),
                "text": res[0],
                "meta": {"original_tuple": res} 
            })

        rerank_request = self.RerankRequest(query=query, passages=passages)
        ranked_results = self.ranker.rerank(rerank_request)

        final_results = []
        for res in ranked_results:
            original = res['meta']['original_tuple']
            new_score = res['score']
            
            final_results.append({
                "chunk_content": original[0],
                "path": original[1],
                "score": float(new_score),
                "reranked_score": float(new_score),
                "old_hybrid_score": original[2],
                "id": original[3],
                "metadata": original[4]
            })

        return final_results[:top_k]