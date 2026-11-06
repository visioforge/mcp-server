---
id: android-maui
title: Android MAUI Deployment Guide
category: platform
platforms:
  - Android
projectTypes:
  - MAUI
sdkTypes:
  - MediaBlocks
tags:
  - mobile
  - android
  - maui
difficulty: intermediate
summary: Deploy VisioForge SDK to Android devices using .NET MAUI framework.
---

# Android MAUI Deployment Guide

## Overview

VisioForge SDK supports Android 5.0 (API 21) and later for mobile video applications.

## Prerequisites

- **Visual Studio 2022** with MAUI workload
- **Android SDK API 34** or later
- **Android device or emulator** (API 21+)

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

<ItemGroup Condition="$(TargetFramework.Contains('-android'))">
  <PackageReference Include="VisioForge.CrossPlatform.Core.Android" Version="2026.11.5" />
  <ProjectReference Include="..\AndroidDependency\VisioForge.Core.Android.X10.csproj" />
</ItemGroup>
```

**Important**: Android requires Java bindings via `AndroidDependency` project reference.

## Project Configuration

```xml
<PropertyGroup>
  <TargetFramework>net10.0-android</TargetFramework>
  <SupportedOSPlatformVersion>21</SupportedOSPlatformVersion>
  <ApplicationId>com.yourcompany.appname</ApplicationId>
</PropertyGroup>
```

## Permissions

Add to `AndroidManifest.xml`:

```xml
<uses-permission android:name="android.permission.CAMERA" />
<uses-permission android:name="android.permission.RECORD_AUDIO" />
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
<uses-permission android:name="android.permission.INTERNET" />
```

### Runtime Permissions (API 23+)

```csharp
using Microsoft.Maui.ApplicationModel;

if (OperatingSystem.IsAndroidVersionAtLeast(23))
{
    await Permissions.RequestAsync<Permissions.Camera>();
    await Permissions.RequestAsync<Permissions.Microphone>();
    await Permissions.RequestAsync<Permissions.StorageWrite>();
}
```

## Deployment

### Build APK

```bash
dotnet build -c Release -f net10.0-android
```

### Build AAB (Google Play)

```bash
dotnet publish -c Release -f net10.0-android -p:AndroidPackageFormat=aab
```

## Code Example

```csharp
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;

public async Task StartCameraAsync()
{
    var pipeline = new MediaBlocksPipeline();
    var camera = new VideoCaptureDeviceSourceBlock(0);
    var renderer = new VideoRendererBlock(pipeline, videoView);

    pipeline.Connect(camera.VideoOutput, renderer.Input);
    await pipeline.StartAsync();
}
```

## Troubleshooting

- **Camera not found**: Check permissions and device capabilities
- **Build failed**: Ensure AndroidDependency project is included
- **App crashes on launch**: Check API level compatibility (min API 21)

## Next Steps

- [MAUI mobile cross-platform](../project-types/maui-mobile.md)
- [iOS deployment](ios-maui.md)
