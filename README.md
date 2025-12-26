<div align="center">

```text
                              ██████╗  █████╗ ██╗███████╗███████╗
                              ██╔══██╗██╔══██╗██║██╔════╝██╔════╝
                              ██████╔╝███████║██║███████╗███████╗
                              ██╔══██╗██╔══██║██║╚════██║╚════██║
                              ██████╔╝██║  ██║██║███████║███████║
                              ╚═════╝ ╚═╝  ╚═╝╚═╝╚══════╝╚══════╝
```


### Your Private, Local-First AI Desktop Assistant.

[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Python](https://img.shields.io/badge/Python-3.10%2B-3776AB?logo=python&logoColor=white)](https://www.python.org/)
[![Avalonia](https://img.shields.io/badge/UI-Avalonia-B72834)](https://avaloniaui.net/)
[![Made with <3](https://img.shields.io/badge/Made%20with-%E2%9D%A4%EF%B8%8F-red.svg)](https://github.com/Tbeninnovation/Baiss)

</div>



https://github.com/user-attachments/assets/0b36d021-e2d8-4410-9cfd-67019821169d




---

## Why Baiss?

In an era where data privacy is paramount, **Baiss** brings the power of Large Language Models (LLMs) and Retrieval-Augmented Generation (RAG) directly to your desktop—**running entirely locally**.

No cloud subscriptions, no data leaks, just pure AI productivity. Whether you're a developer needing a coding assistant or a researcher organizing documents, Baiss provides a unified, cross-platform interface to interact with your data and models.

## Key Features

- **Privacy First**: Runs local LLMs (via `llama.cpp`) and vector search on your machine. Your data never leaves your device.
- **Advanced RAG**: Built-in Retrieval-Augmented Generation using **DuckDB** for high-performance vector storage and **FlashRank** for re-ranking.
- **Cross-Platform UI**: A beautiful, responsive interface built with **Avalonia UI**, running natively on macOS, Windows, and Linux.
- **Extensible Architecture**: Designed with **Clean Architecture** principles, making it easy for developers to add new AI providers, tools, or plugins.
- **Python Power**: Leverages a robust Python backend (FastAPI) for heavy AI lifting, seamlessly integrated with the .NET frontend.

## The Tech Stack

**Frontend & Core:**
- **C# / .NET 8**: The backbone of the application.
- **Avalonia UI**: For a pixel-perfect cross-platform user experience.
- **Clean Architecture**: Separation of concerns (Domain, Application, Infrastructure, UI).

**AI Backend:**
- **Python & FastAPI**: Handles AI logic and API endpoints.
- **DuckDB**: Embedded SQL OLAP database for efficient vector search.
- **Llama.cpp**: For running quantized LLMs locally with hardware acceleration.
- **HuggingFace & Transformers**: For embeddings and model management.

## Getting Started

### Prerequisites

Ensure you have the following installed:
- **.NET 8 SDK**: [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Python 3.10+**: [Download here](https://www.python.org/downloads/)

### Installation

1. **Clone the repository:**
   ```bash
   git clone https://github.com/Tbeninnovation/Baiss.git
   cd Baiss
   ```

2. **Set up the Python Environment:**
   Navigate to the core directory and install dependencies.
   ```bash
   cd core/baiss
   pip install -r requirements.txt
   ```

### Usage

To run the application locally:

```bash
# Navigate to the UI project
cd Baiss.UI

# Run the application
dotnet run
```

*Note: On the first run, Baiss may need to download default models or configure the local database. Please check the console output for status updates.*

## Project Structure

Here's a quick look at the codebase organization:

```text
Baiss/
├── Baiss.UI/              # Avalonia UI Frontend & Entry Point
├── Baiss.Application/     # Business Logic, Interfaces, Use Cases
├── Baiss.Domain/          # Core Entities & Value Objects
├── Baiss.Infrastructure/  # Services, DB Access, External APIs
└── core/
    └── baiss/             # Python Backend (FastAPI, Agents, RAG)
        ├── requirements.txt
        └── shared/python/baiss_agents/
```

## Contributing

We love contributions! Whether it's fixing a bug, improving the UI, or adding support for a new AI model, your help is welcome.

1. clone the repo and create a branch from `dev`
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request to `dev`
6. We will test in dev and merge to main when ready.

### Contributors

A huge thank you to the brilliant minds building Baiss:

- [@taharbmn](https://github.com/taharbmn) - Thought this was a 2-week project. That was 6 months ago.
- [@Abdelmathin](https://github.com/Abdelmathin) - The voice of reason we muted on Meetings.
- [@L0Abdellah](https://github.com/L0Abdellah) - The only one who knows why the search results actually work.
- [@AYoubZarda](https://github.com/AYoubZarda) - Burned a laptop developing this (RIP)
- [@DraGSsine](https://github.com/DraGSsine) - Laptop Survived, My Code Didn’t

---

<div align="center">

**Baiss** — Empowering your desktop with local AI.

</div>
