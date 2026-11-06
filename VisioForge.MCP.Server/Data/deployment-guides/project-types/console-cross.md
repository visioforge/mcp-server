---
id: console-cross
title: Console Cross-Platform Deployment
category: project-type
platforms:
  - Windows
  - Linux
  - macOS
projectTypes:
  - Console
sdkTypes:
  - MediaBlocks
tags:
  - console
  - cross-platform
  - headless
  - server
difficulty: beginner
summary: Deploy VisioForge SDK in cross-platform console applications for servers, automation, and headless video processing.
---

# Console Cross-Platform Deployment

## Overview

VisioForge SDK supports headless console applications for Windows, Linux, and macOS. Perfect for servers, automation, batch processing, and cloud deployments.

## Prerequisites

- **.NET 10.0 SDK** or later
- Platform-specific requirements:
  - **Linux**: GStreamer runtime installed
  - **macOS**: Xcode command-line tools

## NuGet Packages

> **Pick the wrapper package for the SDK you licensed.** The snippets below show
> `VisioForge.DotNet.MediaBlocks`, but this guide applies to all four: use
> `VisioForge.DotNet.VideoCapture` for camera, screen and IP-camera recording,
> `VisioForge.DotNet.MediaPlayer` for playback, `VisioForge.DotNet.VideoEdit` for
> timeline editing. Media Blocks is the low-level pipeline layer. The native
> redist packages are the same whichever you choose.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <!-- Conditional TargetFramework based on build OS -->
    <TargetFramework Condition="$([MSBuild]::IsOsPlatform('MacOS'))">net10.0-macos</TargetFramework>
    <TargetFrameworks Condition="$([MSBuild]::IsOsPlatform('Windows'))">net10.0-windows</TargetFrameworks>
    <TargetFramework Condition="$([MSBuild]::IsOsPlatform('Linux'))">net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="VisioForge.DotNet.MediaBlocks" Version="2026.11.6" />
  </ItemGroup>

  <!-- Windows-specific packages -->
  <ItemGroup Condition="$([MSBuild]::IsOsPlatform('Windows'))">
    <PackageReference Include="VisioForge.CrossPlatform.Core.Windows.x64" Version="2026.11.3" />
    <PackageReference Include="VisioForge.CrossPlatform.Libav.Windows.x64.UPX" Version="2026.11.3" />
  </ItemGroup>

  <!-- macOS-specific packages -->
  <ItemGroup Condition="$(TargetFramework.Contains('-macos'))">
    <PackageReference Include="VisioForge.CrossPlatform.Core.macOS" Version="2026.11.5" />
  </ItemGroup>

  <!-- Linux uses system GStreamer - no additional packages needed -->
</Project>
```

## Code Example

```csharp
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;
using VisioForge.Core.MediaBlocks.Sinks;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting video conversion...");

        var pipeline = new MediaBlocksPipeline();

        var source = new UniversalSourceBlock("input.mp4");
        var sink = new MP4SinkBlock("output.mp4", new H264VideoEncoderSettings());

        pipeline.Connect(source.VideoOutput, sink.VideoInput);
        pipeline.Connect(source.AudioOutput, sink.AudioInput);

        await pipeline.StartAsync();

        // Wait for completion
        await Task.Delay(-1);
    }
}
```

## Deployment

### Windows
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

### Linux (Ubuntu/Debian)
```bash
dotnet publish -c Release -r linux-x64 --self-contained
```

### macOS
```bash
dotnet publish -c Release -r osx-x64 --self-contained
dotnet publish -c Release -r osx-arm64 --self-contained
```

## Docker Deployment

### Dockerfile Example

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble

# Install GStreamer
RUN apt-get update && apt-get install -y \
    gstreamer1.0-tools \
    gstreamer1.0-plugins-base \
    gstreamer1.0-plugins-good \
    gstreamer1.0-plugins-bad \
    gstreamer1.0-libav \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY bin/Release/net10.0/linux-x64/publish/ .

ENTRYPOINT ["dotnet", "MyVideoApp.dll"]
```

## Platform-Specific Notes

- **Windows**: Includes native FFmpeg libraries via NuGet packages
- **Linux**: Uses system-installed GStreamer (install via apt/yum)
- **macOS**: Includes native libraries via NuGet package
- **No UI**: Console apps are headless - use file-based sinks or streaming outputs

## Use Cases

- **Batch transcoding**: Convert large video libraries
- **RTSP recording**: Capture IP camera streams to disk
- **HLS streaming**: Generate live HLS streams for CDN
- **Cloud processing**: AWS Lambda, Azure Functions, Google Cloud Run
- **CI/CD pipelines**: Automated video processing in build systems

## Troubleshooting

- **Linux GStreamer errors**: Install all required GStreamer plugin packages
- **Permission denied**: Ensure user has read/write access to input/output paths
- **Out of memory**: Limit concurrent pipelines or increase container memory
- **Codec not found**: Install additional GStreamer plugins (gstreamer1.0-plugins-ugly for some formats)

## Next Steps

- [Linux deployment](../platforms/linux-ubuntu.md)
- [macOS deployment](../platforms/macos-desktop.md)
- [Windows deployment](../platforms/windows-desktop.md)
- [Docker scenarios](../scenarios/)
