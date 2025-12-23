#!/usr/bin/env python3
"""
Local development server for Baiss API
Run this script to test the FastAPI application locally
"""

import uvicorn
import logging
from pathlib import Path
import os
import sys
import platform
import signal
import subprocess
import argparse

# Add the parent directories to Python path to allow imports
current_dir = Path(__file__).parent.absolute()
shared_python_dir = current_dir.parent
core_dir = shared_python_dir.parent
dev_dir = core_dir.parent

# Add these to sys.path so baiss_agents can be found
sys.path.insert(0, str(shared_python_dir))
sys.path.insert(0, str(current_dir))

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)

logger = logging.getLogger(__name__)


def kill_process_using_port(port):
    """
    Kill any process using the specified port.
    Cross-platform compatible (Windows, macOS, Linux).
    """
    system = platform.system().lower()

    try:
        if system == "windows":
            # Windows approach using netstat and taskkill
            result = subprocess.run(
                ['netstat', '-ano'],
                capture_output=True,
                text=True,
                check=False
            )

            if result.returncode == 0:
                lines = result.stdout.split('\n')
                for line in lines:
                    if f':{port}' in line and 'LISTENING' in line:
                        parts = line.split()
                        if len(parts) >= 5:
                            pid = parts[-1].strip()
                            try:
                                print(f"Killing process {pid} using port {port} on Windows")
                                subprocess.run(['taskkill', '/F', '/PID', pid], check=False)
                            except Exception as e:
                                print(f"Could not kill process {pid}: {e}")
                                continue
                print(f"Port {port} cleanup completed on Windows")

        else:
            # Unix-like systems (macOS, Linux) using lsof
            result = subprocess.run(
                ['lsof', '-t', f'-i:{port}'],
                capture_output=True,
                text=True,
                check=False
            )

            if result.returncode == 0 and result.stdout.strip():
                pids = result.stdout.strip().split('\n')
                for pid in pids:
                    if pid.strip():
                        try:
                            print(f"Killing process {pid} using port {port}")
                            os.kill(int(pid), signal.SIGTERM)
                            # Wait a bit and force kill if still running
                            import time
                            time.sleep(1)
                            try:
                                os.kill(int(pid), signal.SIGKILL)
                            except ProcessLookupError:
                                pass  # Process already dead
                        except (ValueError, ProcessLookupError):
                            continue
                print(f"Port {port} is now free")
            else:
                print(f"No process found using port {port}")

    except FileNotFoundError as e:
        print(f"Command not found: {e}")
        # Fallback: try using psutil if available
        try:
            import psutil
            for proc in psutil.process_iter(['pid', 'name', 'connections']):
                try:
                    for conn in proc.info['connections']:
                        if conn.laddr.port == port and conn.status == psutil.CONN_LISTEN:
                            print(f"Killing process {proc.info['pid']} ({proc.info['name']}) using port {port}")
                            if system == "windows":
                                subprocess.run(['taskkill', '/F', '/PID', str(proc.info['pid'])], check=False)
                            else:
                                os.kill(proc.info['pid'], signal.SIGTERM)
                except (psutil.NoSuchProcess, psutil.AccessDenied, psutil.ZombieProcess):
                    pass
        except ImportError:
            print("psutil not available. Please install it: pip install psutil")
    except Exception as e:
        print(f"Could not kill process using port {port}: {e}")


def main():
    parser = argparse.ArgumentParser(description="Run the Baiss API server locally")
    parser.add_argument("--port", type=int, default=8000, help="Port to run the server on")
    args = parser.parse_args()

    # Set the current working directory to the script's directory
    os.chdir(current_dir)

    port = args.port
    # Kill any process using the port
    kill_process_using_port(port)
    logging.info(f"Starting FastAPI server on port {port}...")

    # Run the FastAPI application
    uvicorn.run(
        "app.main:app",
        host="0.0.0.0",
        port=port,
        reload=True
    )

if __name__ == "__main__":
    main()
