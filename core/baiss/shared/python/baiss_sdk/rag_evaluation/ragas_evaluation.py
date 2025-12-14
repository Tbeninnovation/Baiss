import json
import sys
import os
import pandas as pd
from datasets import Dataset
from ragas import evaluate
from ragas.metrics import (
    faithfulness,
    answer_relevancy,
    context_precision,
    context_recall,
)
from langchain_ollama import ChatOllama, OllamaEmbeddings
from ragas.llms import LangchainLLMWrapper
from ragas.embeddings import LangchainEmbeddingsWrapper

import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])
from baiss_sdk.db.duck_db import DuckDb

from baiss_sdk import get_baiss_project_path

db_path = get_baiss_project_path("local-data", "duckdb", "baiss.duckdb")
db = DuckDb(db_path)
db.connect()

llm = ChatOllama(model="llama3.2")
embeddings_model = OllamaEmbeddings(model="nomic-embed-text")

evaluator_llm = LangchainLLMWrapper(llm)
evaluator_embeddings = LangchainEmbeddingsWrapper(embeddings_model)

try:
    with open("test_dataset.json", "r") as f:
        exam_data = json.load(f)
except FileNotFoundError:
    print("Golden dataset not found.")
    sys.exit()

questions = []
ground_truths = []
answers = []
contexts = []

print("Running RAG pipeline generation...")

for i, row in enumerate(exam_data):
    query = row['question']
    print(f"Answering question {i+1}/{len(exam_data)}...", end="\r")
    
    questions.append(query)
    ground_truths.append(row['ground_truth'])
    
    query_vec = embeddings_model.embed_query(query)
    
    # hybrid_similarity_search returns: 
    # (content, path, hybrid_score, chunk_id, metadata, cosine_score, bm25_score)
    results = db.hybrid_similarity_search(query, query_vec, top_k=5)
    
    #Extract text content (index 0)
    retrieved_contexts = [r[0] for r in results]
    contexts.append(retrieved_contexts)
    
    context_block = "\n".join(retrieved_contexts)
    prompt = f"Answer the question based on the context.\nContext:\n{context_block}\n\nQuestion: {query}"
    
    response = llm.invoke(prompt)
    answers.append(response.content)

print("\nGeneration complete. Calculating metrics...")

data = {
    "question": questions,
    "answer": answers,
    "contexts": contexts,
    "ground_truth": ground_truths
}
dataset = Dataset.from_dict(data)

#Evaluate
results = evaluate(
    dataset=dataset,
    metrics=[
        context_precision,
        context_recall,
        faithfulness,
        answer_relevancy,
    ],
    llm=evaluator_llm,
    embeddings=evaluator_embeddings
)

df_results = results.to_pandas()
df_results.to_csv("ragas_results.csv", index=False)
print("Evaluation complete, Results saved to ragas_results.csv")
print(results)