# VisioForge SDK MCP Server

A [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) server that provides AI assistants with comprehensive knowledge of the VisioForge .NET Media SDK API.

Compatible with **Claude Code**, **GitHub Copilot (VS Code)**, **Cursor**, **Windsurf**, and any MCP-compliant client.

## What It Does

The MCP server gives AI tools accurate, searchable access to:

- **377+ Media Blocks** — Sources, Sinks, Encoders, Decoders, Processing blocks with full API docs
- **Core SDK Classes** — VideoCaptureCoreX, VideoEditCoreX, MediaPlayerCoreX, etc.
- **Full API Surface** — Properties, methods, events, constructors, parameters, return types
- **Code Examples** — Working C# snippets for 12 common scenarios
- **Pipeline Templates** — Pre-built media block pipeline graphs for 8 use cases
- **Full-text Search** — Find any class, method, or concept by keyword
- **Namespace Browser** — Navigate the 224 SDK namespaces hierarchically

## Connect Your AI Tool

### Claude Code
```bash
claude mcp add --transport http visioforge-sdk https://mcp.visioforge.com/mcp
```

### VS Code / GitHub Copilot
Add to `.vscode/mcp.json`:
```json
{
  "servers": {
    "visioforge-sdk": {
      "type": "http",
      "url": "https://mcp.visioforge.com/mcp"
    }
  }
}
```

### Project-Level (any MCP client)
Drop `.mcp.json` at your repo root:
```json
{
  "mcpServers": {
    "visioforge-sdk": {
      "type": "http",
      "url": "https://mcp.visioforge.com/mcp"
    }
  }
}
```

## Available Tools

| Tool | Description |
|------|-------------|
| `list_media_blocks` | Browse media blocks by category (Sources, Sinks, Encoders, etc.) |
| `get_media_block_info` | Full details for a specific block (props, methods, events, pads) |
| `list_sdk_classes` | Core SDK entry-point classes |
| `get_class_info` | Full API surface for any class |
| `search_api` | Full-text search across all API names and documentation |
| `get_enum_values` | Enum values with descriptions |
| `get_code_example` | Working code snippets for common scenarios |
| `get_pipeline_template` | Media block pipeline graphs for use cases |
| `list_namespaces` | Browse namespaces hierarchically |
| `get_method_signature` | Detailed method signature and docs |
| `list_deployment_guides` | List deployment guides filtered by platform / project type / SDK / tag |
| `get_deployment_guide` | Full deployment guide content for a given guide ID |
| `get_nuget_packages_snippet` | `<ItemGroup>` snippet with the right VisioForge NuGet packages for a scenario |
| `get_platform_specific_config` | Platform-specific MSBuild targets / Info.plist / manifest snippets |

## Architecture

```
AI Client ──HTTPS──→ Nginx (SSL) ──HTTP──→ ASP.NET Core MCP Server
                                              │
                                              ├── Tools (14 endpoints)
                                              ├── Resources (5 guides)
                                              ├── Prompts (3 interactive)
                                              └── API Index (in-memory, from JSON)
```

## Projects

| Project | Purpose |
|---------|---------|
| `VisioForge.MCP.Shared` | Shared model classes (ApiIndex, ClassInfo, etc.) |
| `VisioForge.MCP.Server` | ASP.NET Core MCP server |
| `VisioForge.MCP.IndexBuilder` | Offline tool that parses XML docs + source into JSON index |
| `VisioForge.MCP.Tests` | Unit tests |
| `deploy/` | Deployment script, systemd unit, nginx configuration, Dockerfile |

## Refreshing the index

The index is built from the VisioForge SDK source tree: `VisioForge.Core.xml` from a Release
build of VisioForge.Core, the source itself, and `_SOURCE/VisioForge.PackageVersions.props`.
The SDK source is not public, so `Data/api-index.json` ships committed and every deploy
serves that file. The build script looks for the SDK checkout in `VF_SDK_ROOT`, or in a
sibling `../MediaFrameworkDotnet` directory:

```bash
pwsh "$VF_SDK_ROOT/_SOURCE/switch-to-docs.ps1"   # the docs build profile that emits the XML docs
bash build-mcp.sh                                # builds Core, regenerates api-index.json, runs the tests
git add VisioForge.MCP.Server/Data/api-index.json && git commit
```

## Deployment

`deploy/deploy-from-workstation.sh` builds the server locally, backs up the live install,
ships the payload over SSH, restarts the service, checks `/health` and rolls back if the new
build does not come up. The host and the key are passed in, never stored here:

```bash
bash deploy/deploy-from-workstation.sh --host <server> --key <ssh-key> [--dry-run]
```

`deploy/visioforge-mcp.service` and `deploy/nginx.conf` are the systemd unit and the reverse
proxy configuration the server runs behind.

## Development

```bash
# Run the server locally
cd VisioForge.MCP.Server
dotnet run

# Run tests (the deployment-guide tests read the package catalog from the SDK checkout)
cd ../VisioForge.MCP.Tests
dotnet test
```

## Docker (Alternative)
```bash
docker build -t visioforge-mcp -f deploy/Dockerfile .
docker run -p 5000:5000 -v /path/to/api-index.json:/app/Data/api-index.json visioforge-mcp
```

## License

MIT — see [LICENSE](LICENSE). The VisioForge SDK itself is commercial and licensed separately.
