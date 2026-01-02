# Building Baiss on Linux/Debian

This guide provides comprehensive instructions for building and running the Baiss application on Linux, specifically optimized for Debian-based distributions.

## Table of Contents

- [System Requirements](#system-requirements)
- [Quick Start](#quick-start)
- [Detailed Setup](#detailed-setup)
- [Building the Application](#building-the-application)
- [Running the Application](#running-the-application)
- [Docker Build](#docker-build)
- [Troubleshooting](#troubleshooting)
- [Platform-Specific Notes](#platform-specific-notes)

## System Requirements

### Minimum Requirements

- **OS**: Debian 10+, Ubuntu 20.04 LTS+, or compatible Linux distribution
- **CPU**: x64 or ARM64 processor
- **RAM**: 4GB minimum (8GB recommended)
- **Disk**: 2GB free space for build artifacts

### Required Software

- **.NET 10.0 SDK** or later ([Download](https://dotnet.microsoft.com/download/dotnet/10.0))
- **Python 3.8** or later with development packages
- **Git** (for version control)
- Build tools and development headers

## Quick Start

### 1. Automatic Dependency Installation (Recommended)

For a fresh Debian/Ubuntu system:

```bash
cd /path/to/Baiss
sudo chmod +x install-deps.sh
sudo ./install-deps.sh
```

This will install all required system dependencies automatically.

### 2. Build the Application

```bash
chmod +x build.sh
./build.sh Release linux-x64
```

### 3. Run the Application

```bash
chmod +x run.sh
./run.sh Release linux-x64
```

## Detailed Setup

### Step 1: Install .NET SDK

#### Ubuntu/Debian (using Microsoft repositories):

```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Update package lists
sudo apt-get update

# Install .NET 10.0 SDK
sudo apt-get install -y dotnet-sdk-10.0
```

#### Verify installation:

```bash
dotnet --version
dotnet --list-sdks
```

### Step 2: Install System Dependencies

```bash
sudo apt-get update

# Build essentials
sudo apt-get install -y build-essential

# Python development (required for Python.NET integration)
sudo apt-get install -y \
    python3 \
    python3-dev \
    python3-pip \
    libpython3-dev

# Avalonia desktop framework dependencies
sudo apt-get install -y \
    libgconf-2-4 \
    libappindicator1 \
    libxtst6 \
    libxss1 \
    libnss3 \
    libgbm1 \
    libatspi2.0-0 \
    fonts-liberation \
    xdg-utils

# SQLite support
sudo apt-get install -y \
    sqlite3 \
    libsqlite3-dev

# Optional utilities
sudo apt-get install -y \
    git \
    curl \
    wget \
    ca-certificates
```

### Step 3: Clone or Navigate to Repository

```bash
# If you haven't cloned yet:
git clone <repository-url>
cd Baiss

# Or if you already have the code:
cd /path/to/Baiss
```

## Building the Application

### Standard Build (Release)

```bash
./build.sh Release linux-x64
```

**Output**: `./bin/linux-x64/Release/Baiss.UI`

### Debug Build

```bash
./build.sh Debug linux-x64
```

**Output**: `./bin/linux-x64/Debug/Baiss.UI`

### Build for ARM64 (e.g., Raspberry Pi)

```bash
./build.sh Release linux-arm64
```

**Output**: `./bin/linux-arm64/Release/Baiss.UI`

### Build All Linux Architectures

```bash
./build-all-linux.sh Release
```

This creates binaries for both `linux-x64` and `linux-arm64`.

### Build with Tests

```bash
./build.sh Release linux-x64 test
```

This builds the application and runs all unit tests.

### Manual Build Steps

If you prefer to use `dotnet` directly:

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build -c Release --no-restore

# Publish as self-contained application
dotnet publish Baiss.UI/Baiss.UI.csproj \
    -c Release \
    -r linux-x64 \
    -o ./bin/linux-x64/Release \
    --self-contained
```

## Running the Application

### Using the run script

```bash
./run.sh Release linux-x64
```

### Direct execution

```bash
./bin/linux-x64/Release/Baiss.UI
```

### With custom working directory

```bash
cd ./bin/linux-x64/Release
./Baiss.UI
```

### With environment variables

```bash
export DOTNET_CLI_HOME="$HOME/.dotnet"
export DOTNET_ROOT="$HOME/.dotnet"
./bin/linux-x64/Release/Baiss.UI
```

## Docker Build

### Building with Docker

#### Build the application image:

```bash
docker build -t baiss:latest .
```

#### Run in Docker container:

```bash
# Basic run
docker run --rm baiss:latest

# With X11 display forwarding (for GUI on Linux desktop)
docker run --rm \
    -e DISPLAY=$DISPLAY \
    -v /tmp/.X11-unix:/tmp/.X11-unix \
    baiss:latest
```

### Using Docker Compose

#### Build and run the application:

```bash
docker-compose up baiss-app
```

#### Interactive build environment:

```bash
docker-compose run --rm baiss-build bash
```

Inside the container, you can run:

```bash
./build.sh Release linux-x64
./run.sh Release linux-x64
```

#### Clean up:

```bash
docker-compose down
```

## Troubleshooting

### Issue: ".NET SDK not found"

**Solution**:
```bash
dotnet --version
# If not found, install .NET SDK as described in Step 1
which dotnet
```

### Issue: "Python development headers not found"

**Solution**:
```bash
sudo apt-get install python3-dev libpython3-dev
```

### Issue: Missing Avalonia dependencies

**Solution**:
```bash
sudo apt-get install libgconf-2-4 libappindicator1 libxtst6 libxss1 libnss3 libgbm1 libatspi2.0-0
```

### Issue: Build fails with "pythonnet" errors

**Solutions**:
1. Ensure Python development packages are installed:
   ```bash
   sudo apt-get install python3-dev libpython3-dev
   ```

2. Set Python path before building:
   ```bash
   export PYTHONPATH=/usr/include/python3.x:$PYTHONPATH
   dotnet build
   ```

3. Clean build cache:
   ```bash
   dotnet clean
   ./build.sh Release linux-x64
   ```

### Issue: "Permission denied" when running scripts

**Solution**:
```bash
chmod +x build.sh run.sh install-deps.sh build-all-linux.sh
```

### Issue: "Unable to load native library" at runtime

**Solutions**:
1. Ensure all dependencies are installed:
   ```bash
   sudo ./install-deps.sh
   ```

2. Check library paths:
   ```bash
   ldd ./bin/linux-x64/Release/Baiss.UI
   ```

3. Install missing libraries as indicated by `ldd` output.

### Issue: Application crashes on startup

**Debug steps**:
```bash
# Run with verbose output
./Baiss.UI --verbose

# Check logs
tail -f ~/.local/share/baiss/logs/*.log

# Run with strace to see system calls
strace ./Baiss.UI
```

## Platform-Specific Notes

### Debian/Ubuntu

- Uses APT package manager
- Follow the standard installation steps above
- May need to enable universe/multiverse repositories for some packages

### Raspberry Pi (ARM64)

```bash
# Install .NET for ARM64
sudo apt-get install dotnet-sdk-8.0

# Build for ARM64
./build.sh Release linux-arm64

# Expected output location
./bin/linux-arm64/Release/Baiss.UI
```

### Fedora/Red Hat (Not officially supported, but possible)

Would require using `dnf` instead of `apt-get`. Contact maintainers for support.

### Alpine Linux

Docker image supports Alpine. Use the provided `Dockerfile` with multi-stage builds.

## Project Structure

```
Baiss/
├── Baiss.Application/      # Business logic and use cases
├── Baiss.Domain/           # Domain entities and rules
├── Baiss.Infrastructure/   # Data access and external services
├── Baiss.UI/               # Avalonia UI application
├── Baiss.Tests/            # Unit tests
├── Baiss.IntegrationTests/ # Integration tests
├── build.sh                # Linux build script
├── build-all-linux.sh      # Multi-architecture build script
├── install-deps.sh         # Dependency installation script
├── run.sh                  # Application runner
├── Dockerfile              # Docker image definition
├── Dockerfile.buildenv     # Docker build environment
└── docker-compose.yml      # Docker Compose configuration
```

## Build Outputs

### Release Build (x64)

- **Binary**: `./bin/linux-x64/Release/Baiss.UI`
- **Size**: ~150-200 MB (including .NET runtime)
- **Dependencies**: Self-contained (includes .NET runtime)

### Debug Build (x64)

- **Binary**: `./bin/linux-x64/Debug/Baiss.UI`
- **Size**: ~250-300 MB
- **Use**: Development and debugging

## Development Workflow

### 1. Edit Code

Edit source files in your preferred editor (VS Code recommended).

### 2. Build

```bash
dotnet build -c Debug
```

### 3. Run Tests

```bash
dotnet test
```

### 4. Test Application

```bash
./build.sh Debug linux-x64
./run.sh Debug linux-x64
```

### 5. Release Build

```bash
./build.sh Release linux-x64
```

## Additional Resources

- [.NET Documentation](https://docs.microsoft.com/dotnet/)
- [Avalonia Documentation](https://docs.avaloniaui.net/)
- [Python.NET Documentation](https://pythonnet.github.io/)
- [Quartz.NET Documentation](https://www.quartz-scheduler.net/)

## Support

For issues or questions:

1. Check this troubleshooting guide
2. Review application logs in `~/.local/share/baiss/logs/`
3. Check GitHub issues
4. Create a new issue with:
   - Your Linux distribution and version
   - .NET SDK version (`dotnet --version`)
   - Error messages and logs
   - Steps to reproduce

---

**Last Updated**: December 2024
**Tested On**: Debian 11+, Ubuntu 22.04 LTS
**Maintainer**: Baiss Development Team

