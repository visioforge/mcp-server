---
id: maccatalyst-maui
title: Mac Catalyst MAUI Deployment Guide
category: platform
platforms:
  - macOS
projectTypes:
  - MAUI
sdkTypes:
  - MediaBlocks
tags:
  - macos
  - catalyst
  - maui
difficulty: advanced
summary: Deploy VisioForge SDK to Mac Catalyst using .NET MAUI for iOS apps on macOS.
---

# Mac Catalyst MAUI Deployment Guide

## Overview

Mac Catalyst allows iOS apps to run natively on macOS. VisioForge SDK supports Mac Catalyst for bringing your iOS video apps to the Mac.

## Prerequisites

- **macOS 11.0** or later
- **Xcode 14** or later
- **.NET 10.0 SDK** or later
- **MAUI workload**: `dotnet workload install maui`

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

<ItemGroup Condition="$(TargetFramework.Contains('-maccatalyst'))">
  <PackageReference Include="VisioForge.CrossPlatform.Core.macCatalyst" Version="2026.11.5" />
</ItemGroup>
```

## Project Configuration

```xml
<PropertyGroup>
  <TargetFrameworks>net10.0-maccatalyst</TargetFrameworks>
  <SupportedOSPlatformVersion Condition="$(TargetFramework.Contains('-maccatalyst'))">14.0</SupportedOSPlatformVersion>
</PropertyGroup>
```

## Critical: Native Library Copying

**Mac Catalyst requires manual copying of .dylib and .so files to MonoBundle directory.**

Add this MSBuild target to your .csproj:

```xml
<Target Name="CopyNativeLibrariesToMonoBundle" AfterTargets="Build" Condition="$(TargetFramework.Contains('-maccatalyst'))">
  <Message Text="Copying native libraries for Mac Catalyst..." Importance="High" />
  <PropertyGroup>
    <AppBundleDir>$(OutputPath)$(AssemblyName).app</AppBundleDir>
    <MonoBundleDir>$(AppBundleDir)/Contents/MonoBundle</MonoBundleDir>
  </PropertyGroup>
  <MakeDir Directories="$(MonoBundleDir)" Condition="!Exists('$(MonoBundleDir)')" />
  <Copy SourceFiles="@(None->'%(FullPath)')" DestinationFolder="$(MonoBundleDir)"
        Condition="'%(Extension)' == '.dylib' Or '%(Extension)' == '.so'">
    <Output TaskParameter="CopiedFiles" ItemName="CopiedNativeFiles" />
  </Copy>
  <Message Text="Native libraries copied successfully" Importance="High" />
</Target>
```

## Permissions

Add to Info.plist (same as iOS):

```xml
<key>NSCameraUsageDescription</key>
<string>This app requires camera access for video recording</string>

<key>NSMicrophoneUsageDescription</key>
<string>This app requires microphone access for audio recording</string>
```

## Deployment

```bash
dotnet build -c Release -f net10.0-maccatalyst
```

## Troubleshooting

- **Native libs not found**: Verify `CopyNativeLibrariesToMonoBundle` target executed
- **Code signing**: Mac Catalyst requires proper signing for distribution
- **Entitlements**: Add camera/microphone entitlements

## Next Steps

- [iOS deployment](ios-maui.md)
- [macOS native](macos-desktop.md)
