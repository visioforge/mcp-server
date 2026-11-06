# VisioForge MCP Server - Docker deployment (alternative to bare metal)
# Build: docker build -t visioforge-mcp -f deploy/Dockerfile .
# Run:   docker run -p 5000:5000 visioforge-mcp

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for better caching
COPY VisioForge.MCP.Shared/VisioForge.MCP.Shared.csproj VisioForge.MCP.Shared/
COPY VisioForge.MCP.IndexBuilder/VisioForge.MCP.IndexBuilder.csproj VisioForge.MCP.IndexBuilder/
COPY VisioForge.MCP.Server/VisioForge.MCP.Server.csproj VisioForge.MCP.Server/
RUN dotnet restore VisioForge.MCP.Server/VisioForge.MCP.Server.csproj

# Copy all source
COPY . .

# Build the server
WORKDIR /src/VisioForge.MCP.Server
RUN dotnet publish -c Release -o /app --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The api-index.json should be mounted or copied in
# docker run -v /path/to/api-index.json:/app/Data/api-index.json visioforge-mcp

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000
ENTRYPOINT ["dotnet", "VisioForge.MCP.Server.dll"]
