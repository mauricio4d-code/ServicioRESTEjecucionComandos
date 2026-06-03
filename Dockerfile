# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY ServicioRESTEjecucionComandos.sln .
COPY ServicioRESTEjecucionComandos.csproj .

# Restore dependencies (cached if csproj unchanged)
RUN dotnet restore ServicioRESTEjecucionComandos.csproj

# Copy the rest of the source code
COPY . .

# Publish the application as Release
RUN dotnet publish ServicioRESTEjecucionComandos.csproj \
    -c Release \
    -o /app/publish \
    --self-contained false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for HEALTHCHECK
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published output from build stage
COPY --from=build /app/publish .

# Set ownership of application files
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 5000

# Docker-level health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=20s --retries=3 \
    CMD curl -f http://localhost:5000/api/health || exit 1

# Set default environment
ENV ASPNETCORE_URLS=http://0.0.0.0:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Start the application
ENTRYPOINT ["dotnet", "ServicioRESTEjecucionComandos.dll"]
