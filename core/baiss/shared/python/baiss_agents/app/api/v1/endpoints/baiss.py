import logging
import os
import signal
import time
import sys
import baisstools
from fastapi import APIRouter, BackgroundTasks
baisstools.insert_syspath(__file__, matcher = [r"^baiss_.*$"])
from baiss_updater import BaissUpdater

router = APIRouter()
logger = logging.getLogger(__name__)

@router.post("/update")
def update_baiss_app():
    """
    Endpoint to start the Baiss tree structure update process.
    """
    try:
        updater = BaissUpdater()
        updater.download_all()
        updater.extract_all()
        updater.replace_all()
        updater.configure_permissions()
        return {"message": "Baiss tree structure update started successfully."}
    except Exception as e:
        logger.error(f"Error starting Baiss tree structure update: {e}")
    return {"error": "Failed to start Baiss tree structure update."}


# add helth check endpoint
@router.get("/health")
def health_check():
    """
    Health check endpoint to verify the API is running.
    """
    return {"status": "Baiss API is running."}

@router.post("/stop_server")
def stop_server(background_tasks: BackgroundTasks):
    """
    Endpoint to stop the Baiss server.
    """
    def shutdown():
        time.sleep(1)
        logger.info("Shutting down server...")
        try:
            # Try to kill parent process (uvicorn reloader) if it exists
            os.kill(os.getppid(), signal.SIGTERM)
        except:
            pass
        os.kill(os.getpid(), signal.SIGTERM)
        os._exit(0)

    background_tasks.add_task(shutdown)
    return {"message": "Baiss server is stopping..."}
