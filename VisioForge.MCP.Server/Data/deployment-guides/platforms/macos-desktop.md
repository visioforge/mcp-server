---
id: macos-desktop
title: macOS Desktop Deployment Guide
category: platform
platforms:
  - macOS
projectTypes:
  - Console
  - Avalonia
  - MAUI
sdkTypes:
  - MediaBlocks
tags:
  - macos
  - apple
  - desktop
difficulty: intermediate
summary: Deploy VisioForge SDK on macOS for desktop applications with Avalonia or MAUI.
---

# macOS Desktop Deployment Guide

## Overview

VisioForge SDK supports macOS 11+ (Big Sur and later) for both Apple Silicon (ARM64) and Intel (x64) Macs.

## Prerequisites

- **macOS 11.0** or later
- **.NET 10.0 SDK** or later
- **Xcode Command Line Tools**: `xcode-select --install`

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
</ItemGroup>

<ItemGroup Condition="$(TargetFramework.Contains('-macos'))">
  <PackageReference Include="VisioForge.CrossPlatform.Core.macOS" Version="2026.11.5" />
</ItemGroup>
```

## Project Configuration

```xml
<PropertyGroup>
  <TargetFramework>net10.0-macos</TargetFramework>
  <RuntimeIdentifiers>osx-arm64;osx-x64</RuntimeIdentifiers>
  <SupportedOSPlatformVersion>11.0</SupportedOSPlatformVersion>
</PropertyGroup>
```

## Deployment

### Build for macOS

```bash
# Apple Silicon
dotnet publish -c Release -r osx-arm64

# Intel
dotnet publish -c Release -r osx-x64
```

### Create App Bundle

For proper macOS app:

```xml
<PropertyGroup>
  <CFBundleName>MyVideoApp</CFBundleName>
  <CFBundleDisplayName>My Video App</CFBundleDisplayName>
  <CFBundleIdentifier>com.company.myvideoapp</CFBundleIdentifier>
  <CFBundleVersion>1.0</CFBundleVersion>
</PropertyGroup>
```

## Permissions

Add to `Info.plist`:

```xml
<key>NSCameraUsageDescription</key>
<string>This app requires camera access for video recording</string>

<key>NSMicrophoneUsageDescription</key>
<string>This app requires microphone access for audio recording</string>
```

## Code Example

```csharp
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;

var pipeline = new MediaBlocksPipeline();
var source = new VideoCaptureDeviceSourceBlock(0); // Camera index
var sink = new MP4SinkBlock("recording.mp4");

pipeline.Connect(source.VideoOutput, sink.VideoInput);
await pipeline.StartAsync();
```

## Troubleshooting

- **Framework not found**: Install .NET SDK for macOS
- **Camera permission denied**: Check System Preferences → Security & Privacy
- **Code signing required**: For distribution, sign with Apple Developer certificate

## Next Steps

- [Mac Catalyst deployment](maccatalyst-maui.md)
- [Avalonia cross-platform UI](../project-types/avalonia-cross.md)
