#!/usr/bin/env bash
set -euo pipefail

# Convenience script: start backend in background, then run UI. Kills backend when UI exits.

ROOT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../" && pwd)
BAISS_CORE="$ROOT_DIR/core/baiss"
BAISS_UI="$ROOT_DIR/Baiss"

echo "Starting backend..."
pushd "$BAISS_CORE" >/dev/null
. .venv/bin/activate 2>/dev/null || true
if [ ! -d .venv ]; then
  echo "Creating virtualenv and installing requirements"
  python3 -m venv .venv
  . .venv/bin/activate
  pip install --upgrade pip
  pip install -r requirements.txt
else
  . .venv/bin/activate
fi

# start backend in background
if [ -f shared/python/baiss_agents/run_local.py ]; then
  python3 shared/python/baiss_agents/run_local.py &
  BACKEND_PID=$!
  echo "Backend PID: $BACKEND_PID"
else
  echo "No backend entrypoint found. Aborting."
  popd >/dev/null
  exit 2
fi
popd >/dev/null

trap 'echo "Shutting down..."; kill $BACKEND_PID 2>/dev/null || true; wait $BACKEND_PID 2>/dev/null || true; exit' EXIT INT TERM

echo "Building UI..."
pushd "$BAISS_UI" >/dev/null
dotnet build Baiss.sln -c Release
cd Baiss.UI
dotnet run --configuration Release
popd >/dev/null
