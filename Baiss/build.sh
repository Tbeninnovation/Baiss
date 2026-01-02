#!/bin/bash

# Baiss Build Script for Linux/Debian
# This script builds the Baiss application for Linux platforms

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
BUILD_CONFIG="${1:-Release}"
RUNTIME_ID="${2:-linux-x64}"
OUTPUT_DIR="./bin/${RUNTIME_ID}/${BUILD_CONFIG}"

echo -e "${YELLOW}================================================${NC}"
echo -e "${YELLOW}Baiss Build Script for Linux${NC}"
echo -e "${YELLOW}================================================${NC}"
echo ""
echo -e "${GREEN}Build Configuration:${NC}"
echo "  Configuration: $BUILD_CONFIG"
echo "  Runtime ID: $RUNTIME_ID"
echo "  Output Directory: $OUTPUT_DIR"
echo ""

# Check for .NET SDK
echo -e "${GREEN}Checking .NET SDK...${NC}"
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}Error: .NET SDK not found!${NC}"
    echo "Please install .NET 8.0 SDK or later"
    echo "Visit: https://dotnet.microsoft.com/download"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo -e "${GREEN}✓ .NET SDK version: $DOTNET_VERSION${NC}"
echo ""

# Restore dependencies
echo -e "${GREEN}Restoring NuGet packages...${NC}"
dotnet restore
echo -e "${GREEN}✓ Restore completed${NC}"
echo ""

# Build the solution
echo -e "${GREEN}Building Baiss solution...${NC}"
dotnet build -c "$BUILD_CONFIG" --no-restore

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Build completed successfully${NC}"
else
    echo -e "${RED}✗ Build failed${NC}"
    exit 1
fi
echo ""

# Publish the UI application
echo -e "${GREEN}Publishing Baiss.UI application...${NC}"
dotnet publish Baiss.UI/Baiss.UI.csproj \
    -c "$BUILD_CONFIG" \
    -r "$RUNTIME_ID" \
    -o "$OUTPUT_DIR" \
    --self-contained \
    --no-build

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Publish completed successfully${NC}"
else
    echo -e "${RED}✗ Publish failed${NC}"
    exit 1
fi
echo ""

# Make the executable
if [ -f "$OUTPUT_DIR/Baiss.UI" ]; then
    chmod +x "$OUTPUT_DIR/Baiss.UI"
    echo -e "${GREEN}✓ Made executable: $OUTPUT_DIR/Baiss.UI${NC}"
fi
echo ""

# Run unit tests if requested
if [ "$3" == "test" ]; then
    echo -e "${GREEN}Running unit tests...${NC}"
    dotnet test -c "$BUILD_CONFIG" --no-build --verbosity normal
    echo -e "${GREEN}✓ Tests completed${NC}"
    echo ""
fi

echo -e "${GREEN}================================================${NC}"
echo -e "${GREEN}Build completed successfully!${NC}"
echo -e "${GREEN}================================================${NC}"
echo ""
echo -e "${YELLOW}Application Location:${NC}"
echo "  $OUTPUT_DIR/Baiss.UI"
echo ""
echo -e "${YELLOW}To run the application:${NC}"
echo "  $OUTPUT_DIR/Baiss.UI"
echo ""
