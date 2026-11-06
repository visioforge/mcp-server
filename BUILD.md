# VisioForge MCP Server Build Instructions

## Quick Start

### Windows
```cmd
build-mcp-quick.cmd
```

### Linux/macOS/Git Bash
```bash
./build-mcp.sh
```

## Build Scripts

### 1. `build-mcp.cmd` / `build-mcp.sh` (Full Build)

**What it does:**
1. Builds VisioForge.Core (Debug) to generate XML documentation
2. Runs IndexBuilder to parse API docs and deployment guides
3. Builds MCP Server (Release)
4. Runs all tests (70 tests)
5. Generates updated `api-index.json` with deployment guides

**When to use:**
- After adding/modifying deployment guides in `VisioForge.MCP.Server/Data/deployment-guides/`
- After updating VisioForge.Core API
- First time building the project
- When you want a fresh api-index.json

**Usage (Windows):**
```cmd
build-mcp.cmd
```

**Usage (Linux/macOS/Git Bash):**
```bash
./build-mcp.sh
```

**Time:** ~30-60 seconds (includes building VisioForge.Core)

---

### 2. `build-mcp-quick.cmd` (Quick Build - Windows Only)

**What it does:**
1. Builds MCP Server (Release)
2. Runs all tests

**When to use:**
- Quick rebuilds when only MCP Server code changed
- When deployment guides haven't changed
- When api-index.json already exists and is up-to-date
- During active development for fast iteration

**Usage:**
```cmd
build-mcp-quick.cmd
```

**Time:** ~5-10 seconds

---

## Verifying the code examples

`get_code_example` and `get_pipeline_template` return C# as verbatim string literals
inside `VisioForge.MCP.Server/Tools/ExampleTools.cs`. **Nothing compiles those strings.**
`dotnet build` is clean whether the snippet is correct or names a type that has never
existed, and the first person to discover the difference is the developer who pasted it.
That is not hypothetical: four consecutive review rounds each found a different
non-compiling example in that file, two of them introduced while fixing the previous one.

```bash
python3 tools/verify-examples.py          # exit 0 = every snippet compiles
VF_VERSION=2026.9.1 python3 tools/verify-examples.py   # against another release
```

It extracts every ` ```csharp ` block, wraps each in a method, and builds the lot against
the published `VisioForge.DotNet.*` packages from nuget.org in a throwaway project. It is
deliberately not part of `dotnet test`: it restores from the network and takes tens of
seconds. **Run it whenever `ExampleTools.cs` changes.**

Two limits worth knowing. It builds for `net8.0-windows`, so only the `WINDOWS` arm of a
snippet's `#if` is compiled — the Android, iOS, macOS and Linux arms are checked by
reading, as before. And it declares `videoView` / `VideoView1` for the snippets, since
those are placeholders the reader supplies; every other unresolved name is a real defect.

## Running the MCP Server

After building, run the server:

**Windows:**
```cmd
cd VisioForge.MCP.Server
dotnet run
```

**Linux/macOS:**
```bash
cd VisioForge.MCP.Server
dotnet run
```

The server will start and listen for MCP protocol connections (typically via stdio).

---

## Testing Deployment Tools

To test the deployment guide tools manually:

```csharp
// List all deployment guides
await ListDeploymentGuides(platform: "Windows", projectType: "WPF");

// Get specific guide
await GetDeploymentGuide(guideId: "wpf-netcore");

// Get NuGet packages snippet
await GetNuGetPackagesSnippet(
    platform: "Windows",
    projectType: "WPF",
    sdkType: "MediaBlocks"
);

// Get platform-specific config
await GetPlatformSpecificConfig(platform: "maccatalyst");
```

---

## Build Output Locations

- **MCP Server DLL:** `VisioForge.MCP.Server/bin/Release/net10.0/VisioForge.MCP.Server.dll`
- **API Index JSON:** `VisioForge.MCP.Server/Data/api-index.json`
- **Deployment Guides:** `VisioForge.MCP.Server/Data/deployment-guides/`
- **Test Results:** Displayed in console after running tests

---

## Troubleshooting

### "XML documentation file not found"

**Problem:** VisioForge.Core.xml doesn't exist

**Solution:**
```cmd
cd %VF_SDK_ROOT%
dotnet build _SOURCE\VisioForge.Core\VisioForge.Core.csproj -c Debug
```

Ensure `VisioForge.Core.csproj` has:
```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

### "Tests failed"

**Problem:** One or more tests failing

**Solution:**
```cmd
cd VisioForge.MCP.Tests
dotnet test -c Release --logger "console;verbosity=detailed"
```

Review detailed test output to identify failing tests.

### "Deployment guides not found"

**Problem:** IndexBuilder can't find deployment guide directory

**Solution:** Verify directory exists:
```
VisioForge.MCP.Server\Data\deployment-guides\
  ├── platforms\
  └── project-types\
```

---

## Deployment Guide Development

### Adding a New Guide

1. Create markdown file in appropriate directory:
   - Platform guides: `Data/deployment-guides/platforms/`
   - Project type guides: `Data/deployment-guides/project-types/`

2. Add YAML front-matter:
```yaml
---
id: my-platform
title: My Platform Deployment
category: platform
platforms:
  - MyPlatform
projectTypes:
  - MyProjectType
sdkTypes:
  - MediaBlocks
tags:
  - tag1
  - tag2
difficulty: intermediate
summary: Brief description of this guide
---
```

3. Write guide content in Markdown

4. Run full build to regenerate api-index.json:
```cmd
build-mcp.cmd
```

5. Test the new guide:
```cmd
cd VisioForge.MCP.Server
dotnet run
# Use MCP client to call list_deployment_guides and get_deployment_guide
```

### Guide Structure Best Practices

- **Prerequisites:** List required tools/SDKs
- **NuGet Packages:** Provide complete `<ItemGroup>` XML
- **Project Configuration:** Show `.csproj` PropertyGroup settings
- **Code Example:** Working C# example
- **Deployment:** Platform-specific build/deploy commands
- **Troubleshooting:** Common issues and solutions

---

## CI/CD Integration

Example GitHub Actions workflow:

```yaml
name: Build MCP Server

on: [push, pull_request]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Build MCP
        run: |
          cd MCP
          ./build-mcp.cmd
```

---

## Version Information

- **.NET SDK:** 10.0
- **VisioForge SDK:** 2026.8.16
- **MCP projects target:** net10.0
- **Test Framework:** xUnit
- **MCP Protocol:** Model Context Protocol

---

## Additional Resources

- [MCP Server Tools Documentation](VisioForge.MCP.Server/Tools/)
- [Deployment Guides](VisioForge.MCP.Server/Data/deployment-guides/)
- [API Index Format](VisioForge.MCP.Shared/Models.cs)
