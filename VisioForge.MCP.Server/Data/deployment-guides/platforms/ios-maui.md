---
id: ios-maui
title: iOS MAUI Deployment Guide
category: platform
platforms:
  - iOS
projectTypes:
  - MAUI
sdkTypes:
  - MediaBlocks
tags:
  - mobile
  - ios
  - apple
  - maui
difficulty: intermediate
summary: Deploy VisioForge SDK to iOS devices using .NET MAUI framework.
---

# iOS MAUI Deployment Guide

## Overview

VisioForge SDK supports iOS 13.0 and later for iPhone and iPad video applications.

## Prerequisites

- **macOS** with Xcode 14 or later
- **Visual Studio 2022 for Mac** or **VS Code** with MAUI extension
- **.NET 10.0 SDK** or later
- **Apple Developer account** (for device deployment)

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

<ItemGroup Condition="$(TargetFramework.Contains('-ios'))">
  <PackageReference Include="VisioForge.CrossPlatform.Core.iOS" Version="2026.11.5" />
</ItemGroup>
```

## Project Configuration

```xml
<PropertyGroup>
  <TargetFramework>net10.0-ios</TargetFramework>
  <SupportedOSPlatformVersion>13.0</SupportedOSPlatformVersion>
  <CodesignKey>iPhone Developer</CodesignKey>
</PropertyGroup>
```

## Permissions

Add to `Info.plist`:

```xml
<key>NSCameraUsageDescription</key>
<string>This app requires camera access for video recording</string>

<key>NSMicrophoneUsageDescription</key>
<string>This app requires microphone access for audio recording</string>

<key>NSPhotoLibraryUsageDescription</key>
<string>This app requires photo library access to save videos</string>

<key>NSPhotoLibraryAddUsageDescription</key>
<string>This app needs to save captured videos to your photo library</string>
```

## Deployment

### Build for iOS Simulator

```bash
dotnet build -c Debug -f net10.0-ios /p:_DeviceName=:v2:udid=<simulator-uuid>
```

### Build for Physical Device

```bash
dotnet publish -c Release -f net10.0-ios /p:ArchiveOnBuild=true /p:CodesignKey="iPhone Developer" /p:CodesignProvision="YourProvisioningProfile"
```

### Deploy to TestFlight

1. Build IPA with Release configuration
2. Upload to App Store Connect
3. Submit for TestFlight review

## Code Example

```csharp
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;
using VisioForge.Core.MediaBlocks.Sinks;

public async Task StartRecordingAsync()
{
    var pipeline = new MediaBlocksPipeline();

    // Front camera
    var camera = new VideoCaptureDeviceSourceBlock(1);

    // Save to MP4
    var sink = new MP4SinkBlock(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "recording.mp4"));

    pipeline.Connect(camera.VideoOutput, sink.VideoInput);
    pipeline.Connect(camera.AudioOutput, sink.AudioInput);

    await pipeline.StartAsync();
}
```

## Troubleshooting

- **Code signing failed**: Verify provisioning profile and certificate
- **Camera not available**: Check Info.plist permissions
- **App rejected**: Ensure all privacy descriptions are clear and specific
- **Crash on device**: Check minimum iOS version (13.0+)

## Distribution Checklist

- [ ] Valid provisioning profile
- [ ] Code signing certificate installed
- [ ] Info.plist permissions descriptions
- [ ] App icons (all sizes)
- [ ] Launch screen
- [ ] Test on physical device
- [ ] TestFlight beta testing
- [ ] App Store review guidelines compliance

## Next Steps

- [MAUI mobile cross-platform](../project-types/maui-mobile.md)
- [Android deployment](android-maui.md)
- [Mac Catalyst](maccatalyst-maui.md)
