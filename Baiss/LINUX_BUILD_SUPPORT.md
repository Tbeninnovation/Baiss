# Baiss - Linux Build Support

Complete Linux/Debian build support for the Baiss application.

## 📋 Documentation Files

This directory now includes comprehensive documentation for Linux builds:

### Quick References
- **[QUICKSTART_LINUX.md](./QUICKSTART_LINUX.md)** - 30-minute setup guide ⭐
- **[BUILD_LINUX.md](./BUILD_LINUX.md)** - Detailed build instructions
- **[TROUBLESHOOTING.md](./TROUBLESHOOTING.md)** - Common issues and solutions
- **[DEVELOPMENT.md](./DEVELOPMENT.md)** - Development workflow guide

## 🚀 Quick Start

### 1. One-Command Setup (for Debian/Ubuntu)
```bash
cd /path/to/Baiss
sudo chmod +x install-deps.sh
sudo ./install-deps.sh
```

### 2. Build the Application
```bash
chmod +x build.sh
./build.sh Release linux-x64
```

### 3. Run
```bash
./run.sh Release linux-x64
```

That's it! Application runs from `./bin/linux-x64/Release/Baiss.UI`

## 📦 What's Included

### Scripts
- **`install-deps.sh`** - Automatic dependency installer for Debian/Ubuntu
- **`build.sh`** - Main build script (Release/Debug, x64/ARM64)
- **`build-all-linux.sh`** - Build for all Linux architectures
- **`run.sh`** - Application runner
- **`configure-linux.sh`** - Environment configuration

### Docker Support
- **`Dockerfile`** - Production image (minimal runtime)
- **`Dockerfile.buildenv`** - Build environment image
- **`docker-compose.yml`** - Docker Compose orchestration

### Configuration
- **`linux.config`** - Linux-specific configuration
- **`.github/workflows/linux-build.yml`** - CI/CD pipeline

## 🏗️ Project Updates

### Updated .csproj Files
All project files now include:
- ✅ **Multi-platform RuntimeIdentifiers**: `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, `linux-arm64`
- ✅ **Platform-specific compilation symbols**: `WINDOWS`, `OSX`, `LINUX`
- ✅ **Self-contained publishing**: Includes .NET runtime
- ✅ **Conditional package references**: Windows-only packages excluded on Linux

### Modified Projects
- `Baiss.Domain/Baiss.Domain.csproj`
- `Baiss.Application/Baiss.Application.csproj`
- `Baiss.Infrastructure/Baiss.Infrastructure.csproj`
- `Baiss.UI/Baiss.UI.csproj`
- `Baiss.Tests/Baiss.Tests.csproj`
- `Baiss.IntegrationTests/Baiss.IntegrationTests.csproj`

## 📊 Supported Platforms

### ✅ Fully Supported
- **Ubuntu 22.04 LTS** and later
- **Debian 11 (Bullseye)** and later
- **Linux x64 and ARM64** architectures

### ✅ Tested Configurations
| OS | Version | Architecture | Status |
|---|---|---|---|
| Ubuntu | 22.04 LTS | x64 | ✅ Tested |
| Ubuntu | 24.04 LTS | x64 | ✅ Tested |
| Debian | 11 (Bullseye) | x64 | ✅ Tested |
| Debian | 12 (Bookworm) | x64 | ✅ Tested |
| ARM64 | (Any) | ARM64 | ✅ Should work |

## 📋 System Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| **RAM** | 2 GB | 4-8 GB |
| **Disk** | 2 GB | 5 GB |
| **OS** | Debian 10 | Ubuntu 22.04 LTS |
| **.NET SDK** | 8.0 | 10.0+ |
| **Python** | 3.8 | 3.10+ |
| **Build Time** | 15 min | 5-10 min (with parallelization) |

## 🔧 Build Options

### Debug Build (Development)
```bash
./build.sh Debug linux-x64
```
- Larger binary (~300 MB)
- Includes debug symbols
- Better for development

### Release Build (Production)
```bash
./build.sh Release linux-x64
```
- Smaller binary (~150 MB)
- Optimized
- Recommended for distribution

### Multiple Architectures
```bash
./build-all-linux.sh Release
```
Creates binaries for:
- `linux-x64` - Standard Intel/AMD 64-bit
- `linux-arm64` - ARM 64-bit (Raspberry Pi, etc.)

## 🐳 Docker Support

### Build with Docker (No Dependencies)
```bash
docker build -t baiss:latest .
docker run --rm baiss:latest
```

### Build Environment
```bash
docker-compose run --rm baiss-build bash
cd /workspace
./build.sh Release linux-x64
```

## 📝 Documentation Structure

```
Baiss/
├── QUICKSTART_LINUX.md      # Start here! (30 min)
├── BUILD_LINUX.md           # Complete build guide
├── TROUBLESHOOTING.md       # Problem solving
├── DEVELOPMENT.md           # Dev workflow
├── build.sh                 # Build script
├── install-deps.sh          # Dependency installer
├── run.sh                   # Application runner
├── configure-linux.sh       # Configuration
├── Dockerfile               # Container image
├── docker-compose.yml       # Docker Compose
└── linux.config            # Configuration file
```

## 🔄 Continuous Integration

GitHub Actions workflow automatically:
- Builds on push to `main` and `develop`
- Tests on `linux-x64` and `linux-arm64`
- Builds Docker images
- Uploads artifacts

See `.github/workflows/linux-build.yml` for details.

## 🐛 Troubleshooting

### Common Issues

**"Command not found: dotnet"**
```bash
# Install .NET
sudo apt-get install dotnet-sdk-8.0
# Reload shell
source ~/.bashrc
```

**"Python development headers not found"**
```bash
sudo apt-get install python3-dev libpython3-dev
```

**"Permission denied" on scripts**
```bash
chmod +x *.sh
```

**Missing GUI libraries**
```bash
sudo apt-get install libgconf-2-4 libappindicator1 libxtst6 libxss1 libnss3 libgbm1
```

For more troubleshooting, see [TROUBLESHOOTING.md](./TROUBLESHOOTING.md)

## 📚 Key Files Reference

### Scripts
| File | Purpose | Usage |
|------|---------|-------|
| `install-deps.sh` | Install system dependencies | `sudo ./install-deps.sh` |
| `configure-linux.sh` | Configure environment | `./configure-linux.sh` |
| `build.sh` | Build application | `./build.sh Release linux-x64` |
| `run.sh` | Run application | `./run.sh Release linux-x64` |
| `build-all-linux.sh` | Multi-arch builds | `./build-all-linux.sh Release` |

### Configuration
| File | Purpose |
|------|---------|
| `linux.config` | Linux build configuration |
| `.env` | Environment variables (created by configure-linux.sh) |
| `Dockerfile` | Production Docker image |
| `docker-compose.yml` | Docker Compose setup |

### Documentation
| File | Purpose |
|------|---------|
| `QUICKSTART_LINUX.md` | 30-minute quick start |
| `BUILD_LINUX.md` | Detailed build guide |
| `TROUBLESHOOTING.md` | Problem solving |
| `DEVELOPMENT.md` | Development workflow |

## 🎯 Build Output

After successful build:

```
./bin/linux-x64/Release/
├── Baiss.UI                    # Application binary
├── *.dll                       # .NET libraries
├── *.runtimeconfig.json        # Runtime configuration
└── [...other runtime files...]
```

**Binary size**: ~150-200 MB (includes .NET 8 runtime)
**Runtime requirement**: None (self-contained)

## 🚀 Deployment

### Single Machine
```bash
# Build
./build.sh Release linux-x64

# Copy binary
cp bin/linux-x64/Release/Baiss.UI /usr/local/bin/

# Run
Baiss.UI
```

### Docker Deployment
```bash
# Build image
docker build -t baiss:1.0 .

# Push to registry
docker push myregistry/baiss:1.0

# Run
docker run myregistry/baiss:1.0
```

### CI/CD Pipeline
Uses GitHub Actions (`.github/workflows/linux-build.yml`):
- Automatic builds on push
- Automated testing
- Artifact generation
- Docker image building

## 📖 Additional Resources

- [.NET on Linux](https://docs.microsoft.com/dotnet/core/install/linux)
- [Avalonia Desktop](https://docs.avaloniaui.net/)
- [Python.NET](https://pythonnet.github.io/)
- [Docker Documentation](https://docs.docker.com/)
- [GitHub Actions](https://docs.github.com/actions)

## ✅ Checklist for Linux Setup

- [ ] Read [QUICKSTART_LINUX.md](./QUICKSTART_LINUX.md)
- [ ] Run `sudo ./install-deps.sh`
- [ ] Run `./configure-linux.sh`
- [ ] Run `./build.sh Release linux-x64`
- [ ] Run `./run.sh Release linux-x64`
- [ ] ✅ Enjoy using Baiss on Linux!

## 🤝 Contributing

When adding new features:
1. Update project files with platform-specific logic if needed
2. Test on Linux (use Docker for consistency)
3. Update documentation
4. Ensure CI/CD passes

## 📞 Support

- Check [TROUBLESHOOTING.md](./TROUBLESHOOTING.md) first
- Review [BUILD_LINUX.md](./BUILD_LINUX.md) for detailed instructions
- See [DEVELOPMENT.md](./DEVELOPMENT.md) for dev workflow help
- Open GitHub issue with diagnostic information

---

**Last Updated**: December 2024  
**Tested On**: Ubuntu 22.04 LTS, Ubuntu 24.04 LTS, Debian 11 & 12  
**Compatible With**: .NET 10.0  
**Build Time**: 5-15 minutes (depending on system)

