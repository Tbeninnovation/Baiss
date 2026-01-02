#!/usr/bin/env bash
set -euo pipefail

# Run the Python FastAPI backend in a reusable venv under core/baiss
# Usage: ./scripts/linux/run_backend.sh [port]

ROOT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../" && pwd)
BAISS_CORE="$ROOT_DIR/core/baiss"
PORT=${1:-8000}

cd "$BAISS_CORE"
if [ ! -d .venv ]; then
  echo "Creating virtualenv in $BAISS_CORE/.venv"
  python3 -m venv .venv
fi
. .venv/bin/activate
pip install --upgrade pip
if [ -f requirements.txt ]; then
  pip install -r requirements.txt
fi

echo "Starting Python backend (port $PORT)"
# Attempt to run known entry point
if [ -f shared/python/baiss_agents/run_local.py ]; then
  python3 shared/python/baiss_agents/run_local.py --port "$PORT"
else
  echo "Could not find run_local.py; please run your backend entrypoint manually."
  exit 2
fi
