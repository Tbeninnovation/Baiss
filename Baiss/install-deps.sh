#!/bin/bash

# Baiss Install Dependencies Script for Linux/Debian

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${YELLOW}================================================${NC}"
echo -e "${YELLOW}Baiss Dependencies Installation for Debian${NC}"
echo -e "${YELLOW}================================================${NC}"
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}This script requires root privileges.${NC}"
    echo "Please run with sudo: sudo ./install-deps.sh"
    exit 1
fi

# Update package list
echo -e "${GREEN}Updating package list...${NC}"
apt-get update
echo -e "${GREEN}✓ Package list updated${NC}"
echo ""

# Install system dependencies
echo -e "${GREEN}Installing system dependencies...${NC}"

# Build essentials
echo "Installing build-essential..."
apt-get install -y build-essential

# .NET SDK dependencies
echo "Installing .NET SDK dependencies..."
if apt-get install -y \
    dotnet-sdk-10.0 \
    dotnet-runtime-10.0; then
    echo -e "\n${GREEN}✓ .NET packages installed via APT${NC}\n"
else
    echo -e "\n${YELLOW}APT could not find dotnet 10 packages — falling back to dotnet-install script${NC}\n"
    wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
    chmod +x /tmp/dotnet-install.sh
    # detect architecture for dotnet-install
    ARCH=$(dpkg --print-architecture || echo "amd64")
    if [ "$ARCH" = "arm64" ] || [ "$ARCH" = "armhf" ]; then
        DOTNET_ARCH=arm64
    else
        DOTNET_ARCH=x64
    fi
    # Install .NET 10 to system location and symlink
    /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet --architecture $DOTNET_ARCH
    ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet || true
    echo -e "${GREEN}✓ .NET 10 installed via dotnet-install script${NC}\n"
fi

# Python development (required for Python.NET)
echo "Installing Python development packages..."
apt-get install -y \
    python3 \
    python3-dev \
    python3-pip \
    libpython3-dev

# Required libraries for Avalonia (desktop framework)
echo "Installing desktop framework dependencies..."
apt-get install -y \
    libgconf-2-4 \
    libappindicator1 \
    libxtst6 \
    libxss1 \
    libnss3 \
    libgbm1 \
    libatspi2.0-0 \
    fonts-liberation \
    xdg-utils

# SQLite development
echo "Installing SQLite development packages..."
apt-get install -y \
    sqlite3 \
    libsqlite3-dev

# Additional utilities
echo "Installing additional utilities..."
apt-get install -y \
    git \
    curl \
    wget \
    ca-certificates

echo -e "${GREEN}✓ All system dependencies installed${NC}"
echo ""

# Verify .NET installation
echo -e "${GREEN}Verifying .NET installation...${NC}"
dotnet --version
if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ .NET SDK is properly installed${NC}"
else
    echo -e "${RED}✗ .NET SDK verification failed${NC}"
    exit 1
fi
echo ""

echo -e "${GREEN}================================================${NC}"
echo -e "${GREEN}Dependencies installation completed!${NC}"
echo -e "${GREEN}================================================${NC}"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo "1. Navigate to the Baiss directory"
echo "2. Run: ./build.sh Release linux-x64"
echo ""
