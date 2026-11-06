---
id: linux-ubuntu
title: Linux/Ubuntu Deployment Guide
category: platform
platforms:
  - Linux
projectTypes:
  - Console
  - Avalonia
sdkTypes:
  - MediaBlocks
  - VideoCapture
tags:
  - linux
  - ubuntu
  - server
difficulty: intermediate
summary: Deploy VisioForge SDK on Linux/Ubuntu for headless processing or with Avalonia UI.
---

# Linux/Ubuntu Deployment Guide

## Overview

VisioForge SDK supports Linux (Ubuntu 20.04+, Debian 11+) for both headless server applications and desktop apps with Avalonia UI.

## Prerequisites

- **Ubuntu 20.04** or later (or Debian 11+)
- **.NET 10.0 SDK**: `wget https://dot.net/v1/dotnet-install.sh && bash dotnet-install.sh --channel 10.0`
- **GStreamer**: `sudo apt-get install gstreamer1.0-tools gstreamer1.0-plugins-base gstreamer1.0-plugins-good gstreamer1.0-plugins-bad gstreamer1.0-plugins-ugly gstreamer1.0-libav`

## NuGet Packages

> **Pick the wrapper package for the SDK you licensed.** The snippets below show
> `VisioForge.DotNet.MediaBlocks`, but this guide applies to all four: use
> `VisioForge.DotNet.VideoCapture` for camera, screen and IP-camera recording,
> `VisioForge.DotNet.MediaPlayer` for playback, `VisioForge.DotNet.VideoEdit` for
> timeline editing. Media Blocks is the low-level pipeline layer. The native
> redist packages are the same whichever you choose.

```xml
<ItemGroup>
  <PackageReference Include="VisioForge.DotNet.MediaBlocks" Version="2026.11.6" />
  <PackageReference Include="VisioForge.CrossPlatform.Core.Linux.x64" Version="2026.10.26" />
</ItemGroup>
```

## Project Configuration

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
</PropertyGroup>
```

## Deployment

### Self-Contained Deployment

```bash
dotnet publish -c Release -r linux-x64 --self-contained true
```

Copy `bin/Release/net10.0/linux-x64/publish/` to target server.

### Make Executable

```bash
chmod +x MyApp
./MyApp
```

## Code Example

```csharp
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;
using VisioForge.Core.MediaBlocks.Sinks;

// Headless video transcoding
var pipeline = new MediaBlocksPipeline();
var source = new UniversalSourceBlock("input.mp4");
var sink = new MP4SinkBlock("output.mp4");

pipeline.Connect(source.VideoOutput, sink.VideoInput);
pipeline.Connect(source.AudioOutput, sink.AudioInput);

await pipeline.StartAsync();
await pipeline.WaitForCompletionAsync();
```

## Troubleshooting

- **libgstreamer missing**: Install GStreamer packages
- **Permission denied**: Use `chmod +x` on executable
- **Display not found**: For headless servers, set `DISPLAY=:0` or run without GUI

## Next Steps

- [Docker deployment](linux-docker.md)
- [Avalonia UI for Linux desktop](../project-types/avalonia-cross.md)
