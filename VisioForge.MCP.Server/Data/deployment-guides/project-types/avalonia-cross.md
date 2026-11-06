---
id: avalonia-cross
title: Avalonia UI Cross-Platform Deployment
category: project-type
platforms:
  - Windows
  - Linux
  - macOS
  - Android
  - iOS
projectTypes:
  - Avalonia
sdkTypes:
  - MediaBlocks
  - VideoCapture
  - MediaPlayer
tags:
  - cross-platform
  - desktop
  - mobile
  - avalonia
difficulty: intermediate
summary: Deploy VisioForge SDK with Avalonia UI for true cross-platform desktop and mobile applications.
---

# Avalonia UI Cross-Platform Deployment

## Overview

Avalonia UI enables cross-platform apps with a single codebase. VisioForge SDK integrates seamlessly with Avalonia for Windows, Linux, macOS, Android, and iOS.

## Project Approaches

Avalonia supports **two project patterns**:

1. **Single Multi-Target Project** - One project targeting multiple platforms (recommended)
2. **Separate Platform Projects** - Individual projects per platform (Xamarin style)

This guide covers the **multi-target approach**.

## Prerequisites

- **.NET 10.0 SDK** or later
- **Avalonia templates**: `dotnet new install Avalonia.Templates`

## NuGet Packages (Multi-Target Project)

> **Pick the wrapper package for the SDK you licensed.** The snippets below show
> `VisioForge.DotNet.MediaBlocks`, but this guide applies to all four: use
> `VisioForge.DotNet.VideoCapture` for camera, screen and IP-camera recording,
> `VisioForge.DotNet.MediaPlayer` for playback, `VisioForge.DotNet.VideoEdit` for
> timeline editing. Media Blocks is the low-level pipeline layer. The native
> redist packages are the same whichever you choose.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>

  <!-- Conditional TargetFrameworks based on build OS -->
  <PropertyGroup Condition="$([MSBuild]::IsOsPlatform('Windows'))">
    <TargetFrameworks>net10.0-windows;net10.0-android;net10.0-ios</TargetFrameworks>
  </PropertyGroup>
  <PropertyGroup Condition="$([MSBuild]::IsOsPlatform('OSX'))">
    <TargetFrameworks>net10.0-ios;net10.0-android;net10.0-macos</TargetFrameworks>
  </PropertyGroup>
  <PropertyGroup Condition="$([MSBuild]::IsOsPlatform('Linux'))">
    <TargetFrameworks>net10.0;net10.0-android</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="12.0.5" />
    <PackageReference Include="VisioForge.DotNet.MediaBlocks" Version="2026.11.6" />
    <PackageReference Include="VisioForge.DotNet.Core.UI.Avalonia" Version="2026.11.6" />
  </ItemGroup>

  <!-- Windows-specific -->
  <ItemGroup Condition="$(TargetFramework.Contains('-windows'))">
    <PackageReference Include="VisioForge.CrossPlatform.Core.Windows.x64" Version="2026.11.3" />
    <PackageReference Include="VisioForge.CrossPlatform.Libav.Windows.x64.UPX" Version="2026.11.3" />
  </ItemGroup>

  <!-- macOS-specific -->
  <ItemGroup Condition="$(TargetFramework.Contains('-macos'))">
    <PackageReference Include="VisioForge.CrossPlatform.Core.macOS" Version="2026.11.5" />
  </ItemGroup>

  <!-- Android-specific -->
  <ItemGroup Condition="$(TargetFramework.Contains('-android'))">
    <PackageReference Include="Avalonia.Android" Version="12.0.5" />
    <PackageReference Include="VisioForge.CrossPlatform.Core.Android" Version="2026.11.5" />
    <ProjectReference Include="..\AndroidDependency\VisioForge.Core.Android.X10.csproj" />
  </ItemGroup>

  <!-- iOS-specific -->
  <ItemGroup Condition="$(TargetFramework.Contains('-ios'))">
    <PackageReference Include="Avalonia.iOS" Version="12.0.5" />
    <PackageReference Include="VisioForge.CrossPlatform.Core.iOS" Version="2026.11.5" />
  </ItemGroup>
</Project>
```

## Desktop-Only Project Configuration

For desktop-only apps (simpler):

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework Condition="$([MSBuild]::IsOsPlatform('Windows'))">net10.0-windows</TargetFramework>
  <TargetFramework Condition="$([MSBuild]::IsOsPlatform('OSX'))">net10.0-macos</TargetFramework>
  <TargetFramework Condition="$([MSBuild]::IsOsPlatform('Linux'))">net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
  <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
</PropertyGroup>
```

## XAML Integration

Use VisioForge Avalonia video view control:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:vf="clr-namespace:VisioForge.Core.UI.Avalonia;assembly=VisioForge.Core.UI.Avalonia">
    <Grid>
        <vf:VideoView x:Name="VideoView" />
    </Grid>
</Window>
```

## Code Example

```csharp
using Avalonia.Controls;
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;
using VisioForge.Core.MediaBlocks.VideoRendering;

public partial class MainWindow : Window
{
    private MediaBlocksPipeline? _pipeline;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        _pipeline = new MediaBlocksPipeline();
        var source = new UniversalSourceBlock("video.mp4");
        var renderer = new VideoRendererBlock(_pipeline, VideoView);

        _pipeline.Connect(source.VideoOutput, renderer.Input);
        await _pipeline.StartAsync();
    }
}
```

## Platform-Specific Deployment

### 1. Windows Desktop

**Build:**
```bash
dotnet build -c Release -f net10.0-windows
```

**Publish:**
```bash
dotnet publish -c Release -f net10.0-windows -r win-x64 --self-contained
```

**Requirements:**
- Windows 10 or later
- x64 architecture

**Notes:**
- No file copying required
- Native libraries via NuGet

---

### 2. Linux Desktop

**Build:**
```bash
dotnet build -c Release -f net10.0
```

**Publish:**
```bash
dotnet publish -c Release -f net10.0 -r linux-x64 --self-contained
```

**Requirements:**
- GStreamer 1.0 installed: `sudo apt install gstreamer1.0-plugins-base gstreamer1.0-plugins-good gstreamer1.0-libav`

**Notes:**
- Uses system GStreamer
- No native library packages needed

---

### 3. macOS Desktop

**Build:**
```bash
dotnet build -c Release -f net10.0-macos
```

**Publish (Intel):**
```bash
dotnet publish -c Release -f net10.0-macos -r osx-x64 --self-contained
```

**Publish (Apple Silicon):**
```bash
dotnet publish -c Release -f net10.0-macos -r osx-arm64 --self-contained
```

**Requirements:**
- macOS 13.0+ (Ventura)

**Notes:**
- No file copying required
- Native libraries via NuGet

---

### 4. Android

**Build APK:**
```bash
dotnet build -c Release -f net10.0-android
```

**Build AAB:**
```bash
dotnet publish -c Release -f net10.0-android -p:AndroidPackageFormat=aab
```

**Requirements:**
- Android SDK API 21+
- AndroidDependency project reference

**Permissions (AndroidManifest.xml):**
```xml
<uses-permission android:name="android.permission.CAMERA" />
<uses-permission android:name="android.permission.RECORD_AUDIO" />
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
```

**Notes:**
- **CRITICAL**: Must include AndroidDependency ProjectReference
- Runtime permissions required (API 23+)

---

### 5. iOS

**Build:**
```bash
dotnet build -c Release -f net10.0-ios
```

**Publish for Device:**
```bash
dotnet publish -c Release -f net10.0-ios \
  /p:ArchiveOnBuild=true \
  /p:CodesignKey="Apple Development"
```

**Requirements:**
- macOS with Xcode 14+
- Apple Developer account
- Provisioning profile

**Permissions (Info.plist):**
```xml
<key>NSCameraUsageDescription</key>
<string>This app requires camera access for video recording</string>

<key>NSMicrophoneUsageDescription</key>
<string>This app requires microphone access for audio recording</string>
```

**Notes:**
- Minimum iOS 13.0
- Code signing required

## Troubleshooting

- **Video not rendering**: Check VideoView is properly bound
- **Platform libs missing**: Ensure platform-specific packages are included
- **Linux rendering issues**: Install required GStreamer plugins
- **Android build failed**: Verify AndroidDependency project reference
- **iOS signing errors**: Check provisioning profile and certificate

## Next Steps

- [Windows deployment](../platforms/windows-desktop.md)
- [Linux deployment](../platforms/linux-ubuntu.md)
- [macOS deployment](../platforms/macos-desktop.md)
