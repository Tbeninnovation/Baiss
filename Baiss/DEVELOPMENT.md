# Development Workflow - Building Baiss on Linux

## Development Environment Setup

### Initial Setup (One-time)

```bash
# 1. Install dependencies
sudo chmod +x install-deps.sh
sudo ./install-deps.sh

# 2. Configure environment
chmod +x configure-linux.sh
./configure-linux.sh

# 3. Verify setup
dotnet --version
python3 --version
```

### Project Structure for Development

```
Baiss/
├── Baiss.Application/     # Business logic - Update here for new features
├── Baiss.Domain/          # Domain models - Define data structures
├── Baiss.Infrastructure/  # Services & database - Implement external integrations
├── Baiss.UI/              # Avalonia UI - UI components and views
├── Baiss.Tests/           # Unit tests - Test business logic
├── Baiss.IntegrationTests/# Integration tests - Test full features
└── build.sh               # Use for production builds
```

## Daily Development Workflow

### 1. Start Your Day

```bash
cd ~/projects/Baiss

# Make sure you have latest code
git pull origin main

# Verify your environment
dotnet restore
```

### 2. Make Changes

Edit files in your editor (VS Code recommended):

**Example: Adding a new service**
1. Create interface in `Baiss.Application/Interfaces/IMyService.cs`
2. Implement in `Baiss.Infrastructure/Services/MyService.cs`
3. Register in dependency container
4. Add tests in `Baiss.Tests/Infrastructure/MyServiceTests.cs`

### 3. Build During Development

```bash
# Quick debug build (faster)
dotnet build -c Debug

# Run specific project
dotnet build Baiss.UI/Baiss.UI.csproj -c Debug

# Build with warnings as errors
dotnet build /p:TreatWarningsAsErrors=true
```

### 4. Run and Test

```bash
# Run unit tests
dotnet test

# Run specific test project
dotnet test Baiss.Tests/Baiss.Tests.csproj

# Run with verbose output
dotnet test -v normal

# Run app directly
dotnet run --project Baiss.UI/Baiss.UI.csproj -c Debug

# Or use helper script
chmod +x run.sh
./run.sh Debug linux-x64
```

### 5. Debugging

#### Using Visual Studio Code

Install extensions:
- C# (powered by OmniSharp)
- .NET Install Tool
- Debugger for C#

Debug configuration in `.vscode/launch.json`:

```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": ".NET Baiss.UI",
            "type": "coreclr",
            "request": "launch",
            "program": "${workspaceFolder}/bin/linux-x64/Debug/Baiss.UI",
            "args": [],
            "cwd": "${workspaceFolder}",
            "stopAtEntry": false,
            "console": "integratedTerminal",
            "env": {
                "ASPNETCORE_ENVIRONMENT": "Development"
            }
        }
    ]
}
```

#### From Command Line

```bash
# Run with debugging
dotnet run --project Baiss.UI/Baiss.UI.csproj -c Debug

# Use strace to see system calls
strace dotnet run --project Baiss.UI/Baiss.UI.csproj

# Check logs
tail -f ~/.local/share/baiss/logs/*.log
```

### 6. Code Quality

#### Analyze code

```bash
# Run code analysis
dotnet build /p:EnforceCodeStyleInBuild=true

# Run security analysis
dotnet publish -c Release --no-self-contained /p:PublishReadyToRun=true
```

#### Format code

```bash
# Install formatter
dotnet tool install -g dotnet-format

# Format solution
dotnet format

# Format specific project
dotnet format Baiss.UI/
```

#### Static analysis

```bash
# Install analyzer
dotnet tool install -g roslyn-analyzers

# Run analysis
dotnet build /p:EnforceAnalyzersOnBuild=true
```

### 7. Commit and Push

```bash
# Stage changes
git add .

# Commit with meaningful message
git commit -m "feat: add new feature description

- Detail about change 1
- Detail about change 2
"

# Push to develop branch
git push origin develop
```

## Building for Release

### Local Release Build

```bash
# Build for your architecture
./build.sh Release linux-x64

# Or for ARM64
./build.sh Release linux-arm64

# Or build all
./build-all-linux.sh Release
```

### Testing Release Build

```bash
# Run release build
./run.sh Release linux-x64

# Verify binary
ls -lh ./bin/linux-x64/Release/Baiss.UI
file ./bin/linux-x64/Release/Baiss.UI
ldd ./bin/linux-x64/Release/Baiss.UI
```

## Debugging Common Development Issues

### Issue: Changes not reflected in build

```bash
# Clean build
dotnet clean
dotnet build -c Debug

# Or use script
rm -rf bin obj
dotnet build
```

### Issue: NuGet packages out of sync

```bash
# Restore packages
dotnet restore --no-cache

# Update packages
dotnet package search <package-name>
dotnet add package <package-name>
```

### Issue: Type not found after adding new file

```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build

# Or reload VS Code
Ctrl+Shift+P -> Reload Window
```

### Issue: Tests not running

```bash
# Run with verbose output
dotnet test -v d

# Check for missing dependencies
dotnet test --no-build

# Rebuild test project
dotnet rebuild Baiss.Tests/
```

## Performance Profiling

### Memory Profiling

```bash
# Run with profiler
dotnet run --project Baiss.UI/Baiss.UI.csproj -c Debug

# Monitor memory
watch -n 1 'ps aux | grep Baiss.UI'
```

### Build Time Analysis

```bash
# Measure build time
time dotnet build -c Debug

# Analyze build performance
dotnet build -c Debug /p:TimeLog=build.log
cat build.log
```

## Continuous Integration / GitHub Actions

The repository includes GitHub Actions workflow for automated builds:

### View workflow status

```bash
git log --oneline | head -5
# Check GitHub Actions tab for build status
```

### Local CI simulation

```bash
# Install act (GitHub Actions locally)
# https://github.com/nektos/act

act push
```

## Tips and Tricks

### 1. Faster builds with parallelization

```bash
# Use more threads
dotnet build -m:4

# Balance: Use half your CPU cores
```

### 2. Incremental rebuilds

```bash
# Only rebuild changed projects
dotnet build --no-restore
```

### 3. Skip unnecessary tasks

```bash
# Skip tests during development
dotnet build --configuration Debug --no-restore

# Skip restore
dotnet build --no-restore --no-dependencies
```

### 4. Create development shortcuts

```bash
# Add to ~/.bashrc or ~/.zshrc
alias baiss-build='cd ~/projects/Baiss && ./build.sh Release linux-x64'
alias baiss-run='cd ~/projects/Baiss && ./run.sh Release linux-x64'
alias baiss-test='cd ~/projects/Baiss && dotnet test'
alias baiss-clean='cd ~/projects/Baiss && dotnet clean && rm -rf bin obj'

# Then use: baiss-build, baiss-run, etc.
```

### 5. Watch mode for development

```bash
# Install dotnet-watch
dotnet tool install -g dotnet-watch

# Run with auto-reload on changes
dotnet watch --project Baiss.UI/ run

# Build with auto-rebuild
dotnet watch build
```

### 6. Working with Docker for development

```bash
# Build in isolated environment
docker-compose run --rm baiss-build bash
cd /workspace
./build.sh Release linux-x64

# Or mount your code
docker run -it -v $(pwd):/workspace baiss-build bash
```

## Project Dependencies

### NuGet Packages by Project

**Baiss.Domain**: Core business logic
- No external dependencies

**Baiss.Application**: Use cases and services
- Quartz.NET (scheduling)
- Microsoft.Extensions.* (dependency injection, logging)

**Baiss.Infrastructure**: Data access and integrations
- Dapper (database access)
- pythonnet (Python integration)
- Microsoft.SemanticKernel (AI integration)
- Entity Framework or Dapper (ORM)

**Baiss.UI**: User interface
- Avalonia (desktop framework)
- MVVM Community Toolkit (MVVM pattern)
- Serilog (logging)
- Various UI utilities

**Baiss.Tests**: Unit testing
- xunit (test framework)
- Moq (mocking)
- FluentAssertions (assertions)

## Useful Commands Reference

```bash
# Build and test
dotnet build                                    # Build debug
dotnet build -c Release                         # Build release
dotnet test                                     # Run all tests
dotnet test -f net8.0                          # Run for specific framework
dotnet test --filter ClassName                 # Run specific tests

# Run application
dotnet run --project Baiss.UI                  # Run UI
dotnet run --project Baiss.UI -c Release       # Run release UI

# Clean and maintain
dotnet clean                                   # Remove build artifacts
dotnet restore                                 # Restore NuGet packages
dotnet build -m:1                              # Single-threaded build

# Code quality
dotnet format                                  # Format code
dotnet build /p:EnforceCodeStyleInBuild=true  # Check style
```

## Resources

- [.NET Documentation](https://docs.microsoft.com/dotnet/)
- [Avalonia Docs](https://docs.avaloniaui.net/)
- [Visual Studio Code C# Setup](https://code.visualstudio.com/docs/languages/csharp)
- [Git Workflow](https://git-scm.com/docs)
- [Semantic Kernel Docs](https://github.com/microsoft/semantic-kernel)

---

**Happy Coding!**

For questions or issues, refer to [TROUBLESHOOTING.md](./TROUBLESHOOTING.md)

