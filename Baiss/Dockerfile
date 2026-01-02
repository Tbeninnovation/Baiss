FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

WORKDIR /src

# Copy solution and project files
COPY Baiss.sln ./
COPY Baiss.Application/ ./Baiss.Application/
COPY Baiss.Domain/ ./Baiss.Domain/
COPY Baiss.Infrastructure/ ./Baiss.Infrastructure/
COPY Baiss.UI/ ./Baiss.UI/
COPY Baiss.Tests/ ./Baiss.Tests/
COPY Baiss.IntegrationTests/ ./Baiss.IntegrationTests/
COPY NuGet.config ./

# Restore and build
RUN dotnet restore
RUN dotnet build -c Release --no-restore

# Publish
FROM builder AS publisher
RUN dotnet publish Baiss.UI/Baiss.UI.csproj \
    -c Release \
    -r linux-x64 \
    -o /app/publish \
    --self-contained

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:10.0

# Install runtime dependencies for Avalonia and Python.NET
RUN apt-get update && apt-get install -y \
    python3 \
    python3-dev \
    libpython3-dev \
    libgconf-2-4 \
    libappindicator1 \
    libxtst6 \
    libxss1 \
    libnss3 \
    libgbm1 \
    libatspi2.0-0 \
    fonts-liberation \
    xdg-utils \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=publisher /app/publish .

# Create a non-root user
RUN useradd -m -s /bin/bash baiss

# Change ownership
RUN chown -R baiss:baiss /app

USER baiss

# Make the binary executable
RUN chmod +x Baiss.UI

ENTRYPOINT ["./Baiss.UI"]

