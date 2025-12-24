import os
import sys
import baisstools
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from contextlib import asynccontextmanager
from typing import Dict
from baiss_agents.app.api.v1.router import api_router
import logging
import sys

# Windows console encoding fix
if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding='utf-8')
        sys.stderr.reconfigure(encoding='utf-8')
    except AttributeError:
        # Python < 3.7 doesn't support reconfigure
        pass

# Configure logging to output to STDOUT for CloudWatch
logging.basicConfig(
    # filename='logs/app.log',
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    stream=sys.stdout,
    force=True
)

logger = logging.getLogger(__name__)

@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan events"""
    # Startup
    logger.info("Starting up application...")
    try:

        logger.info("Application startup complete.")
    except Exception as e:
        logger.error(f"Error during application startup: {str(e)}")
        raise
    
    yield
    
    # Shutdown
    logger.info("Shutting down application...")

# Create FastAPI app with default values
app = FastAPI(
    title="Baiss API",
    description="AI-powered API using Baiss",
    version="0.1.0",
    lifespan=lifespan,
    root_path="/ai"
)


# Configure CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Configure this appropriately in production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Include API router - will be configured with proper prefix during startup
app.include_router(api_router, prefix="/api/v1")

@app.get("/")
async def root() -> Dict[str, str]:
    """Root endpoint - redirects to docs"""
    try:
        return {
            "message": f"Welcome to Baiss",
            "docs_url": "/docs"
        }
    except RuntimeError:
        return {
            "message": "Welcome to Baiss API",
            "version": "0.1.0",
            "docs_url": "/docs"
        }

@app.get("/health")
async def health_check() -> Dict[str, str]:
    """Health check endpoint"""
    return {"status": "healthy"}

