import os
import sys
import uuid
import threading
import time
import asyncio
from typing import Dict, Any, Optional, List
from dataclasses import dataclass
from enum import Enum
from concurrent.futures import ThreadPoolExecutor
import httpx
import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])
import multiprocessing
from baiss_agents.app.core.config import change_global_token
import logging
from fastapi               import APIRouter
from fastapi.responses     import JSONResponse
from starlette.websockets import WebSocket, WebSocketDisconnect
from baiss_sdk.files import file_reader
from baiss_sdk.parsers.arguments import ArgList
from baiss_sdk.files.structures.scan import generate_full_tree_structures, TreeStructureScanner
from baiss_agents.app.core.config import init_global_token, init_embedding_url
# from baiss_agents.app.models.files import (
#     MetadataValidationRequest
# )
from dotenv import load_dotenv
from datetime import datetime
router = APIRouter()
logger = logging.getLogger(__name__)

load_dotenv()

from pydantic import BaseModel

class GetChunksByPathsRequest(BaseModel):
    paths: List[str]

class FillInMissingEmbeddingsRequest(BaseModel):
    values: List[Dict[str, Any]]


class MetadataValidationRequest(BaseModel):
    sources: List[str]

class DeleteTreeStructureRequestPaths(BaseModel):
    paths: List[str]

class DeleteTreeStructureRequestExtensions(BaseModel):
    extensions: List[str]

class UpdateTreeStructureRequest(BaseModel):
    paths: List[str]
    extensions: List[str]

class AddEmbeddingsRequest(BaseModel):
    embedding_service_url: str = "http://localhost:8081"


# manager = multiprocessing.Manager()
# Dictionary to store all job information
jobs = dict()
# Dictionary to store active processes
active_processes = {}

# Additional Pydantic models for threading operations
class StartTreeStructureRequest(BaseModel):
    paths: List[str]
    extensions: List[str]
    url: str

class TreeStructureProgressRequest(BaseModel):
    process_id: str

class StopTreeStructureRequest(BaseModel):
    process_id: str

@router.websocket("/tree-structure/start")
async def start_tree_structure_operation(websocket: WebSocket):
    """Start a tree structure generation operation StartTreeStructureRequest """
    await websocket.accept()
    try:
        data = await websocket.receive_json()
        paths = data.get("paths")
        extensions = data.get("extensions")
        url = data.get("url")
        token = False
        result = await start_tree_structure_operation_impl(paths, extensions, url, token=token)
        if result.status_code == 200:
            await websocket.send_json({
                "status": 200,
                "success": True,
                "response": "Tree structure operation completed successfully.",
                "error": None
            })
        else:
            await websocket.send_json({
                "status": result.status_code,
                "success": False,
                "response": None,
                "error": result.content.get("error")
            })

    except WebSocketDisconnect:
        logger.info("WebSocket disconnected by client.")
        change_global_token(new_token=True)

    except Exception as e:
        logger.error(f"WebSocket error during tree structure operation: {e}")
        await websocket.send_json({
            "status": 500,
            "success": False,
            "response": None,
            "error": str(e)
        })
    finally:
        await websocket.close()

async def start_tree_structure_operation_impl(paths, extensions, url: str, token = None):
    """Implementation function that can be called directly from C# bridge"""
    try:
        paths = list(set(paths))
        extensions = list(set(extensions))
        # logger.info(f"token received: {token}")
        if url is None or url.strip() == "":
            raise Exception("Embedding URL must be provided and cannot be empty.")
        # logger.info(f"655656646546546546546554654654654654654url received: {url}")
        # exit(0)
        # init_stop()
        init_global_token(token)
        init_embedding_url(url)

        # Validate inputs
        if not paths or not isinstance(paths, list):
            logger.error(f"Paths validation failed: paths={paths}, is_list={isinstance(paths, list)}, bool(paths)={bool(paths)}")
            return JSONResponse(
                status_code=400,
                content={
                    "status": 400,
                    "success": False,
                    "response": None,
                    "error": "paths must be a non-empty list"
                }
            )

        if not extensions or not isinstance(extensions, list):
            logger.error(f"Extensions validation failed: extensions={extensions}, is_list={isinstance(extensions, list)}, bool(extensions)={bool(extensions)}")
            return JSONResponse(
                status_code=400,
                content={
                    "status": 400,
                    "success": False,
                    "response": None,
                    "error": "extensions must be a non-empty list"
                }
            )

        # Check for cancellation using Python's threading mechanisms
        await generate_full_tree_structures(paths, extensions)

        return JSONResponse(
            status_code=200,
            content={
                "status": 200,
                "success": True,
                "response": {
                    "paths": paths,
                    "extensions": extensions,
                    "message": "Tree structure operation is successfully completed."
                },
                "error": None
            }
        )

    except Exception as e:
        logger.error(f"Error starting tree structure operation: {e}")
        return JSONResponse(
            status_code=500,
            content={
                "status": 500,
                "success": False,
                "response": None,
                "error": str(e)
            }
        )






@router.post("/delete_from_tree_structure_with_paths")
async def delete_from_tree_structure_with_paths(request: DeleteTreeStructureRequestPaths):
    paths = request.paths
    try:
        # logger.info(f"Deleting paths from tree structures: {list(paths)}")
        loop = asyncio.get_event_loop()
        await loop.run_in_executor(None, TreeStructureScanner.delete_path_file_or_folder, paths)
        return JSONResponse(
            status_code = 200,
            content = {
                "status"  : 200,
                "success" : True,
                "response": "Paths deleted successfully.",
                "error"   : None,
            }
        )
    except Exception as e:
        logger.error(f"Error deleting paths from tree structure: {e}")
        return JSONResponse(
            status_code = 500,
            content = {
                "status"  : 500,
                "success" : False,
                "response": None,
                "error"   : str(e),
            }
        )


@router.post("/delete_from_tree_structure_with_extensions")
async def delete_from_tree_structure_with_extensions(request: DeleteTreeStructureRequestExtensions):
    extensions = request.extensions
    try:
        # logger.info(f"Deleting paths from tree structures: {list(paths)}")
        loop = asyncio.get_event_loop()
        await loop.run_in_executor(None, TreeStructureScanner.delete_path_extension, extensions)
        return JSONResponse(
            status_code = 200,
            content = {
                "status"  : 200,
                "success" : True,
                "response": "Extensions deleted successfully.",
                "error"   : None,
            }
        )
    except Exception as e:
        logger.error(f"Error deleting extensions from tree structure: {e}")
        return JSONResponse(
            status_code = 500,
            content = {
                "status"  : 500,
                "success" : False,
                "response": None,
                "error"   : str(e),
            }
        )


@router.post("/stop_tree_structure_operation")
async def stop_tree_structure_operation():
    try:
        change_global_token(new_token=True)
        return JSONResponse(
            status_code = 200,
            content = {
                "status"  : 200,
                "success" : True,
                "response": "Tree structure operation stopped successfully.",
                "error"   : None,
            }
        )
    except Exception as e:
        logger.error(f"Error stopping tree structure operation: {e}")
        return JSONResponse(
            status_code = 500,
            content = {
                "status"  : 500,
                "success" : False,
                "response": None,
                "error"   : str(e),
            }
        )


@router.get("/get_all_paths_wo_embeddings")
async def get_all_paths_wo_embeddings():
    try:
        loop = asyncio.get_event_loop()
        paths = await loop.run_in_executor(None, TreeStructureScanner.get_all_paths_wo_embeddings)
        return JSONResponse(
            status_code = 200,
            content = {
                "status"  : 200,
                "success" : True,
                "response": paths,
                "error"   : None,
            }
        )
    except Exception as e:
        logger.error(f"Error retrieving paths without embeddings: {e}")
        return JSONResponse(
            status_code = 500,
            content = {
                "status"  : 500,
                "success" : False,
                "response": None,
                "error"   : str(e),
            }
        )

@router.post("/get_chunks_by_paths")
async def get_chunks_by_paths(request: GetChunksByPathsRequest):
    paths = request.paths
    try:
        # chunks = TreeStructureScanner.get_chunks_by_paths(paths)
        # Run the synchronous method in a thread pool to avoid blocking the event loop
        loop = asyncio.get_event_loop()
        chunks = await loop.run_in_executor(None, TreeStructureScanner.get_chunks_by_paths, paths)
        return JSONResponse(
            status_code = 200,
            content = {
                "status"  : 200,
                "success" : True,
                "response": chunks,
                "error"   : None,
            }
        )
    except Exception as e:
        logger.error(f"Error retrieving chunks by paths: {e}")
        return JSONResponse(
            status_code = 500,
            content = {
                "status"  : 500,
                "success" : False,
                "response": None,
                "error"   : str(e),
            }
        )





@router.post("/add_embeddings")
async def add_embeddings(request: AddEmbeddingsRequest):
    """
    Finds all document chunks without embeddings, generates embeddings for them
    using the specified model, and updates the database.
    """
    embedding_service_url = "http://localhost:8080"
    embeddings_endpoint = f"{embedding_service_url}/embedding"

    try:
        # Get all file paths that are missing embeddings
        paths_wo_embeddings = TreeStructureScanner.get_all_paths_wo_embeddings()
        if not paths_wo_embeddings:
            return JSONResponse(
                status_code=200,
                content={
                    "status": 200,
                    "success": True,
                    "response": "No files are missing embeddings.",
                    "error": None,
                }
            )

        logger.info(f"Found {len(paths_wo_embeddings)} files missing embeddings. Starting generation...")

        async with httpx.AsyncClient(timeout=60.0) as client:
            for path in paths_wo_embeddings:
                chunks = TreeStructureScanner.get_chunks_by_paths([path])

                for chunk in chunks:
                    chunk_id = chunk["id"]
                    content = chunk["chunk_content"]

                    payload = {
                        "content": content
                    }

                    try:
                        response = await client.post(embeddings_endpoint, json=payload)

                        logger.info(f"response of :{response.json()}")
                        response.raise_for_status()

                        embedding_data = response.json()
                        embedding = embedding_data[0]["embedding"][0]
                        TreeStructureScanner.fill_in_missing_embeddings([{"id": chunk_id, "embedding": embedding}])
                        logger.info(f"Successfully generated and stored embedding for chunk {chunk_id} in path {path}")

                    except httpx.RequestError as e:
                        logger.error(f"HTTP request to embedding service failed for chunk {chunk_id}: {e}")
                        continue
                    except (KeyError, IndexError) as e:
                        logger.error(f"Failed to parse embedding from response for chunk {chunk_id}: {e}")
                        continue

        return JSONResponse(
            status_code=200,
            content={
                "status": 200,
                "success": True,
                "response": f"Successfully processed {len(paths_wo_embeddings)} files.",
                "error": None,
            }
        )
    except Exception as e:
        logger.error(f"An error occurred during embedding generation: {e}")
        return JSONResponse(
            status_code=500,
            content={
                "status": 500,
                "success": False,
                "response": None,
                "error": str(e),
            }
        )


@router.post("/metadata-validation")
async def metadata_validation(request: MetadataValidationRequest):
    sources = request.sources
    if not isinstance(sources, list):
        return JSONResponse(
            status_code = 400,
            content = {
                "status"  : 400,
                "success" : False,
                "response": None,
                "error"   : "Invalid input: 'sources' must be a list.",
            }
        )
    sources = list(set(sources)) # Remove duplicates
    if len(sources) < 2:
        return JSONResponse(
            status_code = 400,
            content = {
                "status"  : 400,
                "success" : False,
                "response": None,
                "error"   : "Invalid input: 'sources' must contain at least two different file paths.",
            }
        )
    try:
        dataframe = file_reader.FileReader(sources[0]).read_dataframe()
        reference = ArgList(dataframe.columns.tolist())
    except Exception as e:
        logger.error(f"Error processing file {sources[0]}: {e}")
        return JSONResponse(
            status_code = 500,
            content = {
                "status"  : 500,
                "success" : False,
                "response": None,
                "error"   : str(e),
            }
        )
    for filename in sources[1:]:
        try:
            fp = file_reader.FileReader(filename)
            dataframe = fp.dataframe
            cuurnames = ArgList(dataframe.columns.tolist())
            if not reference.compare(cuurnames):
                logger.info(f"File {filename} does not match the reference columns: {reference}")
                return JSONResponse(
                    status_code = 200,
                    content = {
                        "status"  : 200,
                        "success" : True,
                        "response": False,
                        "error"   : None,
                    }
                )
        except Exception as e:
            logger.error(f"Error processing file {filename}: {e}")
            return JSONResponse(
                status_code = 500,
                content = {
                    "status"  : 500,
                    "success" : False,
                    "response": None,
                    "error"   : str(e),
                }
            )
    return JSONResponse(
        status_code = 200,
        content = {
            "status"  : 200,
            "success" : True,
            "response": True,
            "error"   : None,
        }
    )



if __name__ == "__main__":
    pass
