import json
from typing import Optional, List
import time
import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])

from fastapi import APIRouter
from fastapi.responses import JSONResponse
from typing import Dict, Any, Optional, List

from dotenv import load_dotenv
import logging
import json
import httpx
from pydantic import BaseModel
from baiss_sdk.parsers.json_extractor import JsonExtractor
from baiss_sdk.parsers.python_extractor import PythonExtractor
from starlette.websockets import WebSocket, WebSocketDisconnect
from baiss_agents.app.api.v1.endpoints.files import start_tree_structure_operation_impl
#from baiss_agents.app.core.config import ai_client
from baiss_sdk.db import DbProxyClient
from baiss_sdk import get_baiss_project_path
from baiss_sdk.files.embeddings import Embeddings
from baiss_sdk.search.pipeline import SearchPipeline
import time
import datetime
from pathlib import Path
from baiss_sdk.sandbox.python_sandbox import PythonSandbox
router = APIRouter()
logger = logging.getLogger(__name__)


load_dotenv()



class SimilaritySearchRequest(BaseModel):
    query: str
    search_type: str = "cosine"  # "cosine", "bm25", or "hybrid"
    top_k: int = 8
    k: int = 2 
    score_threshold: float = 0.0
    cosine_weight: float = 0.7
    bm25_weight: float = 0.3
    url_embedding: str
    model_path: Optional[str] = None



def now() -> str:
    return datetime.datetime.now(datetime.timezone.utc).isoformat().replace("+00:00", "Z")

@router.post("/similarity_search")
async def api_v1_llmbox_similarity(request: SimilaritySearchRequest):
    """
    Enhanced similarity search endpoint using DuckDB with VSS and FTS extensions.
    Supports cosine similarity, BM25, and hybrid search.
    """
    try:
        query = request.query
        search_type = request.search_type
        top_k = request.top_k
        score_threshold = request.score_threshold
        cosine_weight = request.cosine_weight
        bm25_weight = request.bm25_weight
        url_embedding = request.url_embedding
        if not query:
            raise ValueError("Query must be provided.")


        # Initialize database client
        db_client = DbProxyClient()
        db_client.connect()
        
        # Setup extensions
        db_client.setup_extensions()
        
        # Only create HNSW index for cosine/hybrid searches 
        #if search_type in ["cosine", "hybrid"]:
        #    db_client.create_hnsw_index()
        
        # Always create FTS index
        db_client.create_fts_index()
        logging.info(f"Database client connected and indexes created.")
        results = []

        if search_type == "cosine":
            # Generate query embedding
            logging.info(f"Generated query embedding for cosine search.")
            query_embedding = await Embeddings(url = url_embedding).embed(query)
            if query_embedding is None:
                raise ValueError("Failed to generate embedding for the query.")
            if not isinstance(query_embedding, list) or len(query_embedding) == 0:
                raise ValueError(f"Invalid embedding returned: {type(query_embedding)}, length: {len(query_embedding) if isinstance(query_embedding, list) else 'N/A'}")
            # Perform cosine similarity search
            results = db_client.similarity_search_cosine(
                query_embedding=query_embedding,
                top_k=top_k,
                score_threshold=score_threshold
            )
            
            # Format results
            formatted_results = [
                {
                    "chunk_content": result[0],
                    "path": result[1] if not result[1].startswith("file://") else result[1][7:],
                    "score": result[2],
                    "id": result[3],
                    "metadata": result[4],
                    # "search_type": "cosine"
                }
                for result in results
            ]

        elif search_type == "bm25":
            # Perform BM25 search (no model needed)
            logging.info(f"Performing BM25 search for query: {query}")
            results = db_client.similarity_search_bm25(
                query=query,
                top_k=top_k,
                score_threshold=score_threshold
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

        elif search_type == "hybrid":
            # Generate query embedding for hybrid search
            
            query_embedding = await Embeddings(url = url_embedding).embed(query)

            search_pipeline = SearchPipeline(db_client)
            
            # Perform hybrid search
            results = search_pipeline.search(
                query_text=query,
                query_embedding=query_embedding,
                final_top_k=top_k
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

        else:
            raise ValueError(f"Invalid search_type: {search_type}. Must be 'cosine', 'bm25', or 'hybrid'.")

        db_client.disconnect()

        return JSONResponse(
            content={
                "status": 200,
                "success": True,
                "message": f"Similarity search completed using {search_type} method.",
                "error": None,
                "data": {
                    "results": formatted_results,
                    "total_results": len(formatted_results),
                    "search_type": search_type,
                    "parameters": {
                        "top_k": top_k,
                        "score_threshold": score_threshold,
                        "cosine_weight": cosine_weight if search_type == "hybrid" else None,
                        "bm25_weight": bm25_weight if search_type == "hybrid" else None
                    }
                },
                "timestamp": now()
            },
            status_code=200
        )

    except Exception as e:
        logger.error(f"Similarity search error: {e}")
        return JSONResponse(
            content={
                "status": 500,
                "success": False,
                "message": str(e),
                "error": str(e),
                "data": None,
                "timestamp": now()
            },
            status_code=500
        )


def convert_stream_chunks(chunk: dict, cache: dict = None) -> dict:
        """
        Converts a chunk from the Llama model to the standard response format.
        Args:
            chunk (dict): Chunk from the Llama model.
            cache (dict): Cache to store intermediate values like role.
        Returns:
            dict: Converted chunk in the standard response format.
        Raises:
            ValueError: If the chunk format is invalid.
        """
        result = {
            "success" : True,
            "error"   : None,
            "response": {
                "choices": []
            }
        }
        if ( not isinstance(chunk, dict) ) or ( chunk.get("object") != "chat.completion.chunk" ):
            raise ValueError("Invalid chunk format from Llama model.")
        if not ("choices" in chunk and isinstance(chunk["choices"], list) and len(chunk["choices"]) > 0):
            raise ValueError("Invalid chunk format from Llama model: missing choices.")
        for choice in chunk["choices"]:
            role : str  = None
            delta: dict = choice["delta"]
            if "role" in delta:
                role = delta["role"]
                if not ( cache is None ):
                    cache["role"] = role
            if (role is None) and cache and ("role" in cache):
                role = cache["role"]
            if role is None:
                role = "assistant"
            content = delta.get("content", None)
            if content is None:
                continue
            result["response"]["choices"].append(
                 {
                    "messages": [
                        {
                            "role"   : role,
                            "content": [
                                {
                                    "type": "text",
                                    "text": content
                                }
                            ]
                        }
                    ]
                }
            )
        return result

def convert_paths_response(paths: List[Dict[str, str]]):
    """
    Converts a list of file paths to a list of dictionaries with 'path' keys.
    Args:
        paths (List[str]): List of file paths.
    Returns:
        List[Dict[str, str]]: List of dictionaries with 'path' keys.
    """
    result = {
            "success" : True,
            "error"   : None,
            "response": {
                "choices": []
            }
        }
    result_paths = []
    seen_paths = []
    for path in paths:
        path_value = path.get("path")
        if path_value and path_value.startswith("file://"):
            path_value = path_value[7:]  # Remove 'file://' prefix
        if path_value not in seen_paths:
            seen_paths.append(path_value)
            result_paths.append({"path": path_value, "score": path.get("score")})
    result["response"]["choices"].append({"paths": result_paths, "messages": []})
    return result

@router.websocket("/pre_chat")
async def get_pre_chat(websocket: WebSocket):
    await websocket.accept()
    retrieval_start = time.time()
    try:
        data = await websocket.receive_json()
        url = data.get("url")
        logging.info(f"Received URL: {url}")
        url_embedding = data.get("embedding_url")
        logging.info(f"Received Embedding URL: {url_embedding}")
        if not url_embedding:
            raise ValueError("Embedding URL must be provided in the request.")
        if not url:
            raise ValueError("URL must be provided in the request.")
        elif not url.endswith("/v1/chat/completions"):
            url = url + "/v1/chat/completions"
        paths = data.get("paths", [])
        messages = data.get("messages")
        
        
        if not messages:
            raise ValueError("Messages must be provided in the request.")

        if len(paths) > 0:
            # check if paths are in db
            logging.info(f"Received paths: {paths}")
            db_client = DbProxyClient()
            db_client.connect()

            existing_paths = db_client.check_if_paths_exists(paths)
            logging.info(f"Existing paths in DB: {existing_paths}")
            unprocessed_paths = [path for path, exists in existing_paths.items() if not exists]
            if len(unprocessed_paths) > 0:
                logging.info(f"Starting tree structure operation for unprocessed paths: {unprocessed_paths}")
                try:
                    for unprocessed_path in unprocessed_paths:
                        logging.info(f"Unprocessed path: {unprocessed_path}")
                        await websocket.send_json({
                            "success" : True,
                            "error"   : None,
                            "response": {
                                "choices": [{"unprocessed_path": unprocessed_path, "messages": []}]
                            }
                            })
                        tree_structure_result = await start_tree_structure_operation_impl(unprocessed_paths, extensions=["csv", "pdf", "xlsx", "xls", "txt", "docx", "md"], url=url_embedding)
                        if tree_structure_result.status_code != 200:
                            await websocket.send_json({
                                "status": 400,
                                "success": False,
                                "message": str(e),
                                "error": str(e),
                                "timestamp": now()
                            })
                        else:
                            await websocket.send_json({
                                "status": 200,
                                "success" : True,
                                "error"   : None,
                                "response": {
                                    "choices": [{"processed_path": unprocessed_path, "messages": []}]
                                }
                            })
                except Exception as e:
                    logging.error(f"Error during tree structure operation: {e}", exc_info=True)
                    await websocket.send_json({
                        "status": 500,
                        "success": False,
                        "message": "Error processing unprocessed paths",
                        "error": str(e),
                        "timestamp": now()
                    })
            db_client.disconnect()

        #TODO: system prompt should be cached and launched with the API 
        system_prompt_path_str = get_baiss_project_path("core","baiss","shared","python","baiss_agents","app","system_prompt","brain","brain.md")
        
        system_prompt_path = Path(system_prompt_path_str)

        
        
        if not system_prompt_path.exists():
            raise FileNotFoundError(f"System prompt file not found at {system_prompt_path}")
        
        with open(system_prompt_path, "r", encoding="utf-8") as f:
            system_prompt_content = f.read()




        system_prompt_message = {
            "role": "system",
            "content": system_prompt_content
        }
        
        all_messages = [system_prompt_message] + messages

        # Prepare the payload for llama server
        payload = {
            "stream": True,
            "reasoning_format": "auto",
            "temperature": 0.3,
            "max_tokens": -1,
            "dynatemp_range": 0,
            "dynatemp_exponent": 1,
            "top_k": 40,
            "top_p": 0.95,
            "min_p": 0.05,
            "xtc_probability": 0,
            "xtc_threshold": 0.1,
            "typ_p": 1,
            "repeat_last_n": 64,
            "repeat_penalty": 1.1,
            "presence_penalty": 0,
            "frequency_penalty": 0,
            "dry_multiplier": 0.8,
            "dry_base": 1.75,
            "dry_allowed_length": 2,
            "dry_penalty_last_n": 4096,
            "samplers": [
                "penalties", "dry", "top_n_sigma", "top_k", "typ_p",
                "top_p", "min_p", "xtc", "temperature"
            ],
            "timings_per_token": True
        }
        MAX_ATTEMPTS = 3
        i = 0
        results = []
        should_continue = True  # Flag to control loop continuation
        
        while i < MAX_ATTEMPTS and should_continue:
            payload["messages"] = all_messages
            content_buffer = ""
            should_continue = False  # Reset - only continue if we have more work

            # Stream the response from llama server to websocket
            async with httpx.AsyncClient(timeout=300.0) as client:
                async with client.stream("POST", url, json=payload) as response:
                    response.raise_for_status()

                    async for line in response.aiter_lines():
                        if line.strip():
                            # Remove "data: " prefix if present
                            if line.startswith("data: "):
                                line = line[6:]

                            # Skip [DONE] message
                            if line.strip() == "[DONE]":
                                await websocket.send_json({
                                    "status": 200,
                                    "success": True,
                                    "done": True,
                                    "timestamp": now()
                                })
                                break
                            
                            try:
                                # Parse and forward the JSON chunk
                                chunk_data = json.loads(line)

                                if "choices" in chunk_data and isinstance(chunk_data["choices"], list):
                                    for choice in chunk_data["choices"]:
                                        delta = choice.get("delta", {})
                                        content = delta.get("content", "")
                                        if content:
                                            content_buffer += content

                                data = convert_stream_chunks(chunk_data)
                                
                                # Filter out <code_execution> tags from the response
                                should_send = True
                                if data["response"]["choices"]:
                                    for choice in data["response"]["choices"]:
                                        for msg in choice.get("messages", []):
                                            for content_item in msg.get("content", []):
                                                if content_item.get("type") == "text":
                                                    text = content_item.get("text", "")
                                                    # Skip if text contains code_execution tags
                                                    if "<code_execution>" in text or "</code_execution>" in text:
                                                        should_send = False
                                                    # Also filter out the tags from partial matches
                                                    elif text.strip() in ["<code", "<code_", "<code_e", "<code_ex", 
                                                                          "<code_exe", "<code_exec", "<code_execu", 
                                                                          "<code_execut", "<code_executi", "<code_executio",
                                                                          "<code_execution>", "</code", "</code_", "</code_e", "</code_ex",
                                                                          "</code_exe", "</code_exec", "</code_execu",
                                                                          "</code_execut", "</code_executi", "</code_execution>"]:
                                                        should_send = False
                                
                                if data["response"]["choices"] and should_send:
                                    logging.info(f"Streaming chunk: {data}")
                                    await websocket.send_json(data)
                            except json.JSONDecodeError:
                                logger.warning(f"Failed to parse JSON chunk: {line}")
                                continue
            # to check wash had l3iba khas tkon flkhr ola ndiroha lwst
            extract_tools = JsonExtractor.extract_objects(content_buffer)
            extract_python = PythonExtractor(content_buffer).functions

            if extract_python:
                # logging.info(f"Extracted Python functions/classes/methods: {extract_python}")

                for attempts in range(3):
                    try:
                        sandbox = PythonSandbox()
                        sandbox.add_tool_reference(
                            name="searchlocaldocuments",
                            module_path="baiss_sdk.tools",
                            class_name="allTools",
                            method_name="searchlocaldocuments",
                            init_kwargs={"url_embedding": url_embedding}
                        )
                        code_to_execute = str(extract_python[0]["body"]).rstrip() + "\n\nasyncio.run(main())\n"
                        logger.info(f"extracted code {code_to_execute}")
                        exec_result = sandbox.execute(code_to_execute, timeout=30)
                        # logging.info(f"Sandbox execution result: {exec_result}")
                        if exec_result.get("success"):
                            exec_data = exec_result.get("stdout")
                            if exec_data:
                                all_messages.append({
                                    "role": "user",
                                    "content": f"<code_execution_result>{str(exec_data)}</code_execution_result>"
                                })
                            else: 
                                all_messages.append({
                                    "role": "user",
                                    "content": f"The code executed successfully with no result."
                                })

                            await websocket.send_json({
                                    "success" : True,
                                    "error"   : None,
                                    "response": {
                                        "code_execution_status": True,
                                        "error": None
                                    }
                                })
                            should_continue = True
                            break
                        else:
                            exec_error = exec_result.get("error")
                            # logging.error(f"Sandbox execution error: {exec_error}")
                            all_messages.append({
                                "role": "user",
                                "content": f"<code_execution_result> {exec_error} </code_execution_result>"
                            })


                            await websocket.send_json({
                                    "success" : True,
                                    "error"   : None,
                                    "response": {
                                        "code_execution_status": False,
                                        "error": exec_error
                                    }
                                })
                            should_continue = True
                            time.sleep(1)
                            break
                    except Exception as e:
                        logging.error(f"Sandbox execution error on attempt {attempts + 1}: {e}", exc_info=True)
                        time.sleep(1)


            if len(extract_tools) > 0:
                try:
                    logging.info(f"Extracted tools: {extract_tools}")
                    for tool in extract_tools:
                        if tool.get("tool") == "search" and tool.get("query"):
                            search_query = tool["query"]
                            logger.info(f"Performing additional search for query: {search_query}")
                            search_params = SimilaritySearchRequest(query=search_query, search_type="hybrid", url_embedding=url_embedding, top_k=5)
                            search_result = await api_v1_llmbox_similarity(search_params)
                            # logging.info(f"Additional similarity search completed with status: {search_result.body}")
                            result_content = json.loads(search_result.body.decode('utf-8'))
                            status = result_content.get('status')
                            if result_content.get("data"):
                                data = result_content.get('data', {})
                                results = data.get('results', [])
                                logging.info(f"Additional search results: {results}")
                                all_messages.append({
                                    "role": "user",
                                    "content": f"<search_results>{str(results)}</search_results>"
                                })
                                should_continue = True
                            else:
                                all_messages.append({
                                    "role": "user",
                                    "content": "<search_results>[]</search_results>"
                                })
                                should_continue = True
                        else:
                            logging.info("No search tool found in extracted tools.")
                            # No valid tool, don't continue
                except Exception as tool_error:
                    logging.error(f"Error processing tools: {tool_error}")
                    all_messages.append({
                        "role": "user",
                        "content": "<search_results>[]</search_results>"
                    })
                    should_continue = True
            # else: no tools/code found, should_continue stays False, loop ends naturally
            
            i += 1
        retrieval_end = time.time()
        retrieval_time = retrieval_end - retrieval_start
        if len(results) > 0:
            last_paths = convert_paths_response(results)
            await websocket.send_json(last_paths)
            logging.info(f"Sent final paths: {last_paths}")
    

    except ValueError as e:
        logger.error(f"Validation error: {e}", exc_info=True)
        await websocket.send_json({
            "status": 400,
            "success": False,
            "message": str(e),
            "error": str(e),
            "timestamp": now()
        })
    except httpx.HTTPError as e:
        logger.error(f"HTTP error calling llama server: {e}", exc_info=True)
        await websocket.send_json({
            "status": 500,
            "success": False,
            "message": "Failed to connect to llama server",
            "error": str(e),
            "timestamp": now()
        })
    except Exception as e:
        logger.error(f"Error in pre_chat websocket: {e}", exc_info=True)
        await websocket.send_json({
            "status": 500,
            "success": False,
            "message": "Internal server error",
            "error": str(e),
            "timestamp": now()
        })
    finally:
        await websocket.close()



def main():
    """
    Main function to test similarity search methods directly via the POST endpoint.
    """
    import asyncio
    import httpx
    
    async def test_similarity_search():
        url = "http://localhost:8000/api/v1/chatv2/similarity_search"
        
        test_queries = [
            "MCTS algorithm implementation",
            "machine learning techniques",
            "data processing methods"
        ]
        
        search_types = ["cosine", "bm25", "hybrid"]
        
        async with httpx.AsyncClient(timeout=30.0) as client:
            for query in test_queries:
                print(f"\n{'='*60}")
                print(f"Testing Query: '{query}'")
                print('='*60)
                
                for search_type in search_types:
                    payload = {
                        "query": query,
                        "search_type": search_type,
                        "top_k": 5,
                        "score_threshold": 0.0,
                        "cosine_weight": 0.7,
                        "bm25_weight": 0.3,
                        "url_embedding": "http://localhost:8080"
                    }
                    
                    print(f"\n--- Search Type: {search_type.upper()} ---")
                    
                    try:
                        response = await client.post(url, json=payload)
                        result = response.json()
                        
                        if result.get("success"):
                            data = result.get("data", {})
                            results = data.get("results", [])
                            print(f"Found {len(results)} results:")
                            
                            for i, res in enumerate(results, 1):
                                score = res.get("score", 0)
                                path = res.get("path", res.get("name", "N/A"))
                                content_preview = res.get("chunk_content", res.get("text", ""))[:100]
                                
                                print(f"\n  {i}. Score: {score:.4f}")
                                print(f"     Path: {path}")
                                print(f"     Preview: {content_preview}...")
                                
                                if search_type == "hybrid":
                                    print(f"     Cosine: {res.get('cosine_score', 'N/A'):.4f}, "
                                          f"BM25: {res.get('bm25_score', 'N/A'):.4f}")
                        else:
                            print(f"Error: {result.get('message')}")
                            
                    except Exception as e:
                        print(f"Request failed: {e}")
    
    try:
        asyncio.run(test_similarity_search())
    except Exception as e:
        print(f"Test execution failed: {e}")

if __name__ == "__main__":
    main()