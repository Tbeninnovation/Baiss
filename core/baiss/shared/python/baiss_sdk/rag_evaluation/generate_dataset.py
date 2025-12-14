import sys
import os
import pandas as pd
from langchain_core.documents import Document
from langchain_ollama import ChatOllama, OllamaEmbeddings
from ragas.testset import TestsetGenerator

import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])

from baiss_sdk.db.duck_db import DuckDb

langchain_llm = ChatOllama(
    model="llama3.2", 
    temperature=0)
langchain_embeddings = OllamaEmbeddings(model="nomic-embed-text")

from baiss_sdk import get_baiss_project_path

db_path = get_baiss_project_path("local-data", "duckdb", "baiss.duckdb")
db = DuckDb(db_path)
db.connect()

print("Fetching documents from DuckDB BaissChunks...")
try:
    rows = db.execute_query("SELECT chunk_content, path, id FROM BaissChunks LIMIT 3")
except Exception as e:
    print(f"Error fetching data: {e}")
    sys.exit()

documents = [
    Document(
        page_content=row[0], 
        metadata={"filename": row[1], "chunk_id": row[2]}
    ) 
    for row in rows if row[0] 
]

if not documents:
    print("No documents found in BaissChunks. Please ingest data first.")
    sys.exit()

#Generate the Exam
generator = TestsetGenerator.from_langchain(
    llm=langchain_llm,
    embedding_model=langchain_embeddings
)

print(f"Generating testset from {len(documents)} chunks...")
testset = generator.generate_with_langchain_docs(documents, testset_size=2)

testset.to_pandas().to_json("test_dataset.json", orient="records")
print("Exam created and saved to test_dataset.json!")