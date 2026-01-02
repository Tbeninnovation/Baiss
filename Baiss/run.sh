#!/bin/bash

# Baiss Run Script for Linux/Debian

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

BUILD_CONFIG="${1:-Release}"
RUNTIME_ID="${2:-linux-x64}"
APP_DIR="./bin/${RUNTIME_ID}/${BUILD_CONFIG}"
APP_BINARY="$APP_DIR/Baiss.UI"

echo -e "${YELLOW}================================================${NC}"
echo -e "${YELLOW}Baiss Application Runner${NC}"
echo -e "${YELLOW}================================================${NC}"
echo ""

# Check if application binary exists
if [ ! -f "$APP_BINARY" ]; then
    echo -e "${RED}Error: Application binary not found at $APP_BINARY${NC}"
    echo ""
    echo -e "${YELLOW}Please build the application first:${NC}"
    echo "  ./build.sh $BUILD_CONFIG $RUNTIME_ID"
    exit 1
fi

# Check if binary is executable
if [ ! -x "$APP_BINARY" ]; then
    echo -e "${YELLOW}Making binary executable...${NC}"
    chmod +x "$APP_BINARY"
fi

echo -e "${GREEN}Starting Baiss application...${NC}"
echo ""

# Set environment variables if needed
export DOTNET_CLI_HOME="$HOME/.dotnet"
export DOTNET_ROOT="$HOME/.dotnet"

# Change to app directory and run
cd "$APP_DIR"
./"$(basename "$APP_BINARY")"

