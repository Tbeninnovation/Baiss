# Quick Start Guide - Building Baiss on Linux

## 30-Minute Quick Setup

### Step 1: Install Dependencies (10 minutes)

On a fresh Debian/Ubuntu system:

```bash
cd /path/to/Baiss
sudo chmod +x install-deps.sh
sudo ./install-deps.sh
```

This will automatically install:
- .NET 8.0 SDK
- Python 3 development packages
- Avalonia desktop dependencies
- SQLite support
- Build tools

### Step 2: Configure Environment (2 minutes)

```bash
chmod +x configure-linux.sh
./configure-linux.sh
```

This creates necessary directories and configuration files.

### Step 3: Build Application (10 minutes)

For x64 (most common):
```bash
chmod +x build.sh
./build.sh Release linux-x64
```

For ARM64 (Raspberry Pi, etc.):
```bash
./build.sh Release linux-arm64
```

### Step 4: Run Application (5 minutes)

```bash
chmod +x run.sh
./run.sh Release linux-x64
```

Or directly:
```bash
./bin/linux-x64/Release/Baiss.UI
```

## Troubleshooting

### Issue: "sudo: install-deps.sh: command not found"

**Fix**: Use absolute or relative path:
```bash
sudo bash ./install-deps.sh
# or
sudo /path/to/install-deps.sh
```

### Issue: ".NET not found after installation"

**Fix**: Reload shell environment:
```bash
source ~/.bashrc
# or log out and back in
```

### Issue: "Permission denied" on scripts

**Fix**:
```bash
chmod +x build.sh run.sh install-deps.sh
```

## Docker Alternative (No Dependencies)

Build and run entirely in Docker:

```bash
# Build Docker image
docker build -t baiss:latest .

# Run application
docker run --rm baiss:latest

# Interactive build environment
docker-compose run --rm baiss-build bash
cd /workspace
./build.sh Release linux-x64
```

## What Gets Built

After successful build:
- **Application**: `./bin/linux-x64/Release/Baiss.UI` (~150 MB)
- **Self-contained**: Includes .NET 8 runtime
- **Standalone**: Can run on any Debian/Ubuntu without .NET installed

## File Locations

- **Application**: `~/.local/share/baiss/`
- **Logs**: `~/.local/share/baiss/logs/`
- **Cache**: `~/.cache/baiss/`
- **Config**: `~/.config/baiss/`

## Next Steps

1. Read [BUILD_LINUX.md](./BUILD_LINUX.md) for detailed instructions
2. Check [TROUBLESHOOTING.md](./TROUBLESHOOTING.md) if issues arise
3. Review [DEVELOPMENT.md](./DEVELOPMENT.md) for development workflow

## System Requirements Summary

| Requirement | Minimum | Recommended |
|---|---|---|
| RAM | 2 GB | 4-8 GB |
| Disk | 2 GB | 5 GB |
| OS | Debian 10 | Ubuntu 22.04 LTS |
| .NET | 10.0 | 10.0+ |
| Python | 3.8 | 3.10+ |

## Supported Linux Distributions

✅ **Tested and Supported:**
- Ubuntu 22.04 LTS
- Ubuntu 24.04 LTS
- Debian 11 (Bullseye)
- Debian 12 (Bookworm)

✅ **Should Work:**
- Any Debian-based distribution
- Linux Mint
- Pop!_OS
- Elementary OS

⚠️ **May Require Adjustments:**
- Fedora / Red Hat
- Arch Linux
- Alpine Linux

---

**For comprehensive documentation, see [BUILD_LINUX.md](./BUILD_LINUX.md)**

