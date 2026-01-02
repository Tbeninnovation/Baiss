#!/usr/bin/env bash
set -euo pipefail

echo "This script installs system dependencies for Baiss on Debian-based systems."
echo "Run with sudo: sudo ./scripts/install_debian.sh"

apt update
apt install -y ca-certificates curl gnupg lsb-release wget software-properties-common build-essential \
  python3 python3-venv python3-pip git libsqlite3-dev libsndfile1 libgomp1 \
  libgtk-3-0 libgdk-pixbuf2.0-0 libxcursor1 libxrandr2 libxss1 libxinerama1 libatk1.0-0 libatk-bridge2.0-0

echo "Installing Microsoft packages for .NET"
TEMP_DEB=$(mktemp)
wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O "$TEMP_DEB"
dpkg -i "$TEMP_DEB"
rm -f "$TEMP_DEB"
apt update
apt install -y dotnet-sdk-8.0

echo "Done. Next steps:
 1) From repo root, prepare Python backend: cd core/baiss && python3 -m venv .venv && . .venv/bin/activate && pip install -r requirements.txt
 2) Run the backend: python3 shared/python/baiss_agents/run_local.py
 3) Build and run UI: cd Baiss && dotnet build Baiss.sln -c Release && cd Baiss.UI && dotnet run --configuration Release"
