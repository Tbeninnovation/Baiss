# Troubleshooting Guide - Baiss Linux Build

## Common Issues and Solutions

### Build-Related Issues

#### Issue 1: ".NET SDK not found"

**Error Message:**
```
Error: .NET SDK not found!
Please install .NET 8.0 SDK or later
```

**Causes:**
- .NET not installed
- .NET not in PATH
- Wrong .NET version

**Solutions:**

1. Check installation:
```bash
dotnet --version
which dotnet
```

2. Install or update .NET:
```bash
# Ubuntu/Debian
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0

# Then reload shell
source ~/.bashrc
```

3. Verify installation:
```bash
dotnet --list-sdks
```

---

#### Issue 2: Build fails with "pythonnet" errors

**Error Messages:**
```
Package: pythonnet Version: 3.0.5
error: Unable to build pythonnet
error: Python development headers not found
```

**Causes:**
- Python development packages not installed
- Wrong Python version
- Python headers not in expected location

**Solutions:**

1. Install Python development packages:
```bash
sudo apt-get install -y \
    python3 \
    python3-dev \
    python3-pip \
    libpython3-dev
```

2. Verify Python installation:
```bash
python3 --version
python3-config --includes
```

3. Set explicit Python path if needed:
```bash
export PYTHONPATH=/usr/include/python3.x:$PYTHONPATH
export Python_EXECUTABLE=/usr/bin/python3
dotnet build -c Release
```

4. Clean and rebuild:
```bash
dotnet clean
rm -rf bin obj
./build.sh Release linux-x64
```

---

#### Issue 3: Missing Avalonia dependencies

**Error Messages:**
```
error: Cannot find library libgconf-2.so.4
error: Cannot find library libappindicator.so.1
```

**Solutions:**

```bash
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
```

---

#### Issue 4: "Permission denied" on shell scripts

**Error:**
```
bash: ./build.sh: Permission denied
```

**Solution:**
```bash
chmod +x build.sh run.sh install-deps.sh configure-linux.sh
```

Or for all scripts:
```bash
find . -name "*.sh" -exec chmod +x {} \;
```

---

#### Issue 5: "Could not restore" NuGet packages

**Error:**
```
error: Unable to resolve 'System.XXX' due to no matching dependency
```

**Causes:**
- NuGet source issues
- Network connectivity
- Corrupted package cache

**Solutions:**

1. Clear NuGet cache:
```bash
rm -rf ~/.nuget/packages
dotnet restore
```

2. Check NuGet sources:
```bash
dotnet nuget list source
```

3. Restore with logging:
```bash
dotnet restore --verbosity diagnostic
```

4. Use official NuGet source:
```bash
dotnet nuget add source https://api.nuget.org/v3/index.json --name nuget.org
```

---

### Runtime Issues

#### Issue 6: Application won't start - "Unable to load native library"

**Error:**
```
Unable to load native library '/path/to/library.so'
```

**Causes:**
- Missing system library
- Library path issues
- 32/64-bit mismatch

**Solutions:**

1. Check library dependencies:
```bash
ldd ./bin/linux-x64/Release/Baiss.UI | grep "not found"
```

2. Install missing libraries:
```bash
# Example for missing library
sudo apt-get install libXXX-dev
```

3. Run with library path:
```bash
export LD_LIBRARY_PATH=/usr/lib/x86_64-linux-gnu:$LD_LIBRARY_PATH
./bin/linux-x64/Release/Baiss.UI
```

---

#### Issue 7: Application crashes immediately

**Error:**
```
Segmentation fault (core dumped)
or
An unhandled exception occurred
```

**Debug with strace:**
```bash
strace -e trace=file ./bin/linux-x64/Release/Baiss.UI 2>&1 | head -50
```

**Debug with gdb:**
```bash
sudo apt-get install gdb
gdb --args ./bin/linux-x64/Release/Baiss.UI
(gdb) run
# When it crashes:
(gdb) bt
```

**Check logs:**
```bash
cat ~/.local/share/baiss/logs/*.log
```

---

#### Issue 8: "No protocol specified" - Display connection issue

**Error:**
```
No protocol specified
Cannot connect to display
```

**Causes:**
- Running GUI app without display
- X11 forwarding issues

**Solutions:**

1. For local desktop:
```bash
export DISPLAY=:0
./bin/linux-x64/Release/Baiss.UI
```

2. For SSH X11 forwarding:
```bash
ssh -X user@host
# Then run app
```

3. For headless systems, use Xvfb:
```bash
sudo apt-get install xvfb
Xvfb :99 -screen 0 1024x768x24 &
export DISPLAY=:99
./bin/linux-x64/Release/Baiss.UI
```

---

### System-Level Issues

#### Issue 9: Insufficient disk space

**Error:**
```
No space left on device
```

**Check disk:**
```bash
df -h
du -sh ./*
```

**Clean up:**
```bash
# Remove build artifacts
dotnet clean
rm -rf bin obj

# Clean NuGet cache
rm -rf ~/.nuget/packages

# Clean apt cache (if using system .NET)
sudo apt-get clean
```

---

#### Issue 10: Memory issues during build

**Error:**
```
fatal: unable to allocate memory
```

**Causes:**
- Build system requiring too much RAM
- System swap too small

**Solutions:**

1. Increase swap:
```bash
sudo fallocate -l 4G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
```

2. Build with limited parallelism:
```bash
dotnet build -c Release -m:1
```

3. Run tests separately:
```bash
dotnet build -c Release
dotnet test -c Release --no-build
```

---

### Docker-Specific Issues

#### Issue 11: Docker build fails

**Error:**
```
failed to build: docker build failed
```

**Solutions:**

1. Check Docker installation:
```bash
docker --version
sudo usermod -aG docker $USER
# Log out and back in
```

2. Clean Docker cache:
```bash
docker system prune -a
docker build -t baiss:latest . --no-cache
```

3. Build with verbose output:
```bash
docker build -t baiss:latest . --progress=plain
```

---

#### Issue 12: Docker container won't run

**Error:**
```
docker: Error response from daemon
```

**Solutions:**

1. Check Docker daemon:
```bash
sudo systemctl status docker
sudo systemctl start docker
```

2. Check container logs:
```bash
docker logs <container-id>
```

3. Run with verbose output:
```bash
docker run --rm -it baiss:latest bash
```

---

### Python.NET Specific Issues

#### Issue 13: Python.NET runtime errors

**Error:**
```
BadImageFormatException: Could not load file or assembly 'Python.Runtime'
```

**Solutions:**

1. Verify Python installation:
```bash
python3 --version
python3-config --libs
```

2. Check Python library path:
```bash
find /usr -name "libpython*.so" 2>/dev/null
```

3. Set Python environment:
```bash
export PYTHONHOME=/usr
export PYTHONPATH=/usr/lib/python3/dist-packages
dotnet run
```

4. Rebuild with Python explicitly:
```bash
export Python_EXECUTABLE=/usr/bin/python3
dotnet clean
dotnet build -c Release
```

---

### Database Issues

#### Issue 14: SQLite database errors

**Error:**
```
unable to open database file
permission denied
```

**Solutions:**

1. Check database directory:
```bash
mkdir -p ~/.local/share/baiss
chmod 755 ~/.local/share/baiss
```

2. Check database file:
```bash
sqlite3 ~/.local/share/baiss/baiss.db ".tables"
```

3. Reset database:
```bash
rm ~/.local/share/baiss/baiss.db
# App will recreate on next run
```

---

## Diagnostic Commands

### Gather System Information

```bash
#!/bin/bash
echo "=== System Information ==="
lsb_release -a
uname -a
free -h
df -h

echo "=== Software Versions ==="
dotnet --version
python3 --version
gcc --version

echo "=== Installed Packages ==="
dpkg -l | grep -E "python|dotnet|sqlite|libgconf|libappindicator"
```

### Check All Dependencies

```bash
#!/bin/bash
DEPS=(
    "python3"
    "python3-dev"
    "libpython3-dev"
    "libgconf-2-4"
    "libappindicator1"
    "libxtst6"
    "libxss1"
    "libnss3"
    "libgbm1"
    "libatspi2.0-0"
    "sqlite3"
    "libsqlite3-dev"
)

for dep in "${DEPS[@]}"; do
    if dpkg -l | grep -q "^ii.*$dep"; then
        echo "✓ $dep"
    else
        echo "✗ $dep (missing)"
    fi
done
```

---

## Getting Help

If you still can't resolve the issue:

1. **Gather diagnostics:**
```bash
cat > diagnostics.txt << 'EOF'
System Info:
$(lsb_release -a)
$(uname -a)

.NET Info:
$(dotnet --version)
$(dotnet --list-sdks)

Python Info:
$(python3 --version)
$(python3-config --includes)

Installed Packages:
$(dpkg -l | grep -E "python|dotnet|sqlite" | wc -l) relevant packages installed

Build Output:
$(dotnet build 2>&1 | tail -20)
EOF
```

2. **Create GitHub issue with:**
   - `diagnostics.txt` content
   - Exact error messages
   - Steps to reproduce
   - Output of failed commands

3. **Contact maintainers** with:
   - Your Linux distribution
   - Build configuration used
   - Previous troubleshooting attempts

---

**Version**: 1.0  
**Last Updated**: December 2024  
**Compatible With**: Baiss .NET 10.0 builds on Linux

