#!/bin/bash

# Pre-build configuration script for Linux
# This script sets up environment variables and checks prerequisites

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${YELLOW}Baiss Linux Pre-build Configuration${NC}"
echo ""

# 1. Check .NET SDK
echo -e "${GREEN}Checking .NET SDK...${NC}"
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}✗ .NET SDK not found${NC}"
    exit 1
fi
DOTNET_VERSION=$(dotnet --version)
echo -e "${GREEN}✓ .NET SDK: $DOTNET_VERSION${NC}"

# 2. Check Python
echo -e "${GREEN}Checking Python...${NC}"
if ! command -v python3 &> /dev/null; then
    echo -e "${RED}✗ Python 3 not found${NC}"
    exit 1
fi
PYTHON_VERSION=$(python3 --version)
echo -e "${GREEN}✓ Python: $PYTHON_VERSION${NC}"

# 3. Check Git
echo -e "${GREEN}Checking Git...${NC}"
if ! command -v git &> /dev/null; then
    echo -e "${YELLOW}⚠ Git not found (optional)${NC}"
else
    GIT_VERSION=$(git --version)
    echo -e "${GREEN}✓ $GIT_VERSION${NC}"
fi

echo ""

# 4. Set up environment variables
echo -e "${GREEN}Setting up environment variables...${NC}"

# Create .env file if it doesn't exist
if [ ! -f ".env" ]; then
    cat > .env << 'EOF'
# Baiss Application Configuration

# Environment
ASPNETCORE_ENVIRONMENT=Production
DOTNET_ENVIRONMENT=Production

# Logging
ASPNETCORE_LOGGING__LOGLEVEL__DEFAULT=Information

# Data directories
BAISS_DATA_PATH=~/.local/share/baiss
BAISS_LOG_PATH=~/.local/share/baiss/logs
BAISS_CACHE_PATH=~/.cache/baiss

# Python configuration (for Python.NET)
PYTHONHOME=/usr
EOF
    echo -e "${GREEN}✓ Created .env file${NC}"
else
    echo -e "${GREEN}✓ .env file already exists${NC}"
fi

# 5. Create necessary directories
echo -e "${GREEN}Creating application directories...${NC}"
mkdir -p ~/.local/share/baiss/logs
mkdir -p ~/.cache/baiss
mkdir -p ~/.config/baiss
echo -e "${GREEN}✓ Directories created${NC}"

echo ""
echo -e "${GREEN}Pre-build configuration complete!${NC}"
echo -e "${YELLOW}You can now run: ./build.sh Release linux-x64${NC}"

