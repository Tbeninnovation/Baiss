#!/bin/bash

# Build Baiss for all Linux architectures

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

ARCHITECTURES=("linux-x64" "linux-arm64")
BUILD_CONFIG="${1:-Release}"

echo -e "${YELLOW}Building Baiss for all Linux architectures...${NC}"
echo ""

for ARCH in "${ARCHITECTURES[@]}"; do
    echo -e "${GREEN}Building for $ARCH...${NC}"
    ./build.sh "$BUILD_CONFIG" "$ARCH"
    echo ""
done

echo -e "${GREEN}All architectures built successfully!${NC}"
