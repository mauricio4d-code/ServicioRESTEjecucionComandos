# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0-windowsservercore-ltsc2022 AS build
WORKDIR /src

# Copy project file first for better layer caching
COPY ServicioRESTEjecucionComandos.csproj .

# Restore dependencies (cached if csproj unchanged)
RUN dotnet restore ServicioRESTEjecucionComandos.csproj

# Copy the rest of the source code
COPY . .

# Publish the application as Release
RUN dotnet publish ServicioRESTEjecucionComandos.csproj -c Release -o /app/publish --self-contained false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-windowsservercore-ltsc2022 AS final
WORKDIR /app

# Copy published output from build stage
COPY --from=build /app/publish .

# Expose port
EXPOSE 5000

# Docker-level health check (PowerShell instead of curl)
HEALTHCHECK --interval=30s --timeout=10s --start-period=20s --retries=3 CMD powershell -Command "(Invoke-WebRequest -Uri http://localhost:5000/api/health -UseBasicParsing).StatusCode -eq 200"

# Set default environment
ENV ASPNETCORE_URLS=http://0.0.0.0:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Start the application
ENTRYPOINT ["dotnet", "ServicioRESTEjecucionComandos.dll"]
