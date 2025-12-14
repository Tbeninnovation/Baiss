import json
import sys
import os
import asyncio
from langchain_ollama import OllamaEmbeddings
from ranx import Qrels, Run, evaluate, compare
import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])
from baiss_sdk.db.duck_db import DuckDb
from baiss_sdk.files.embeddings import Embeddings

try:
    with open("test_dataset.json", "r") as f:
        exam_data = json.load(f)
except FileNotFoundError:
    print("Golden dataset not found. Run generate_dataset.py first.")
    sys.exit()

from baiss_sdk import get_baiss_project_path

async def main():
    db_path = get_baiss_project_path("local-data", "duckdb", "baiss.duckdb")
    db = DuckDb(db_path)
    db.connect()

    embeddings_model = OllamaEmbeddings(model="nomic-embed-text")

    results_bm25 = {}
    results_cosine = {}
    results_hybrid = {}
    qrels_dict = {} 

    print("Running retrieval evaluation on 3 methods...")

    for i, row in enumerate(exam_data):
        q_id = f"q_{i}"
        query = row['question']
        
        # Ragas stores the source info in 'metadata'. We need the 'chunk_id' we saved earlier.
        qrels_dict[q_id] = {}
        #Metadata is usually a list of dicts in Ragas datasets
        metadata_list = row.get('metadata', [])
        if isinstance(metadata_list, list):
            for meta in metadata_list:
                if 'chunk_id' in meta:
                    #We mark this chunk_id as relevant (score 1) for this question
                    qrels_dict[q_id][str(meta['chunk_id'])] = 1
        
        print(f"Processing {q_id}...", end="\r")
        url_embedding = "http://localhost:8080"
        try:
            query_vec = await Embeddings(url = url_embedding).embed(query)
            
            # --- 1. BM25 Search ---
            bm25_res = db.similarity_search_bm25(query, top_k=10)
            # Map: {doc_id: score}
            results_bm25[q_id] = {str(r[3]): r[2] for r in bm25_res}

            # --- 2. Cosine Search ---
            cosine_res = db.similarity_search_cosine(query_vec, top_k=10)
            results_cosine[q_id] = {str(r[3]): r[2] for r in cosine_res}

            # --- 3. Hybrid Search ---
            hybrid_res = db.hybrid_similarity_search(query, query_vec, top_k=10)
            results_hybrid[q_id] = {str(r[3]): r[2] for r in hybrid_res}
            
        except Exception as e:
            print(f"\nError searching for q_{i}: {e}")
            results_bm25[q_id] = {}
            results_cosine[q_id] = {}
            results_hybrid[q_id] = {}

    print("\nSearch complete. Calculating metrics...")

    #Create Ranx Objects
    qrels = Qrels(qrels_dict)
    run_bm25 = Run(results_bm25, name="BM25")
    run_cosine = Run(results_cosine, name="Cosine")
    run_hybrid = Run(results_hybrid, name="Hybrid")

    # NDCG@10: Did the best document appear at the top?
    # Recall@10: Did the correct document appear anywhere in the top 10?
    report = compare(
        qrels=qrels,
        runs=[run_bm25, run_cosine, run_hybrid],
        metrics=["ndcg@10", "recall@10", "mrr@10"],
        max_p=0.05
    )

    print("\n" + "="*50)
    print("RETRIEVAL LEADERBOARD")
    print("="*50)
    print(report)

    # Save detailed results
    with open("ranx_report.txt", "w", encoding="utf-8") as f:
        f.write(str(report))

    # Save raw runs
    with open("run_bm25.json", "w") as f: json.dump(results_bm25, f)
    with open("run_cosine.json", "w") as f: json.dump(results_cosine, f)
    with open("run_hybrid.json", "w") as f: json.dump(results_hybrid, f)
    
    
if __name__ == "__main__":
    asyncio.run(main())    