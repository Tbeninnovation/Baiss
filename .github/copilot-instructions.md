# Baiss Desktop AI Agent Instructions

## Project Overview
This is a cross-platform desktop application for AI chat and assistance, built with **Avalonia UI (C#)** and a local **Python AI backend**.
- **Frontend/Core**: .NET 9, Avalonia UI, Clean Architecture.
- **AI Backend**: Python (FastAPI), DuckDB (Vector Search), Llama.cpp (Local LLM).
- **Target Platforms**: macOS (x64, arm64), Windows.

## Architecture & Data Flow
The application follows a **Clean Architecture** pattern in C# and communicates with local subprocesses for AI capabilities.

1.  **C# Application (`Baiss/`)**:
    -   **UI Layer (`Baiss.UI`)**: Avalonia views/viewmodels. Manages lifecycle of subprocesses (`App.axaml.cs`).
    -   **Application Layer (`Baiss.Application`)**: Use cases, DTOs, Interfaces.
    -   **Infrastructure Layer (`Baiss.Infrastructure`)**: Implementations, DB access (Dapper), Service integrations.
    -   **Domain Layer (`Baiss.Domain`)**: Core entities and business rules.

2.  **Python Backend (`core/baiss/shared/python/baiss_agents`)**:
    -   Runs as a local FastAPI server (`run_local.py`).
    -   Handles chat logic, RAG (Retrieval Augmented Generation), and tool execution.
    -   Uses **DuckDB** for local vector storage and similarity search (`chatv2.py`).

3.  **Llama Server**:
    -   `llama-server` (llama.cpp) is launched as a subprocess to host local LLMs.
    -   Managed by `LaunchServerService.cs`.

## Key Files & Directories
-   **Entry Point & Process Management**: `Baiss/Baiss.UI/App.axaml.cs` (Starts Python & Llama servers).
-   **Python Entry Point**: `core/baiss/shared/python/baiss_agents/run_local.py`.
-   **Chat Logic**: `core/baiss/shared/python/baiss_agents/app/api/v1/endpoints/chatv2.py`.
-   **Llama Management**: `Baiss/Baiss.Infrastructure/Services/LaunchServerService.cs`.
-   **Infrastructure Services**: `Baiss/Baiss.Infrastructure/Services/`.

## Development Guidelines

### C# / .NET
-   **Dependency Injection**: All services are registered in `App.axaml.cs` -> `ConfigureServices`.
-   **Clean Architecture**:
    -   Define Interfaces in `Application`.
    -   Implement in `Infrastructure`.
    -   Use `UseCases` in `Application` to orchestrate logic.
-   **UI**: Use MVVM pattern. ViewModels in `Baiss.UI/ViewModels`.

### Python
-   **FastAPI**: Used for the local API.
-   **Pydantic**: Use for data validation and DTOs.
-   **DuckDB**: Use `DbProxyClient` for database interactions.
-   **Environment**: Python environment is managed locally or via Docker for builds.

### Build & Run
-   **Run Locally**: `cd Baiss/Baiss.UI && dotnet run`.
    -   Ensure Python environment is set up and `baiss_config.json` points to valid paths.
-   **Build**: Use Docker containers as described in `README.md` to ensure consistent environments for macOS/Windows builds.

## Common Patterns
-   **Subprocess Communication**: The C# app communicates with the Python backend via HTTP/WebSockets.
-   **Configuration**: `baiss_config.json` stores paths to Python, Models, etc.
-   **Vector Search**: Hybrid search (Cosine + BM25) implemented in `chatv2.py` using DuckDB.

## Important Constraints
-   **Local-First**: The app is designed to run models and logic locally.
-   **Cross-Platform**: Avoid OS-specific code unless guarded by `OperatingSystem.Is...` checks.
-   **Process Cleanup**: Ensure subprocesses (Python, Llama) are killed on app shutdown (`OnShutdownRequested` in `App.axaml.cs`).
