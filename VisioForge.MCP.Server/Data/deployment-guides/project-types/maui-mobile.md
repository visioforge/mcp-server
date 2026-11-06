---
id: maui-mobile
title: .NET MAUI Mobile Cross-Platform Deployment
category: project-type
platforms:
  - Android
  - iOS
  - macOS
  - Windows
projectTypes:
  - MAUI
sdkTypes:
  - MediaBlocks
  - VideoCapture
  - MediaPlayer
tags:
  - mobile
  - cross-platform
  - maui
difficulty: intermediate
summary: Deploy VisioForge SDK with .NET MAUI for cross-platform mobile and desktop applications from a single codebase.
---

# .NET MAUI Mobile Cross-Platform Deployment

## Overview

.NET MAUI enables building native cross-platform apps with a single codebase. VisioForge SDK supports MAUI for Android, iOS, Windows, and Mac Catalyst.

## Prerequisites

- **.NET 10.0 SDK** or later
- **MAUI workload**: `dotnet workload install maui`
- **Platform-specific tools**:
  - Windows: Visual Studio 2022 with MAUI workload
  - macOS: Xcode 14+ for iOS/Mac Catalyst
  - Android: Android SDK API 21+

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
    <TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
    <TargetFrameworks Condition="$([MSBuild]::IsOsPlatform('windows'))">$(TargetFrameworks);net10.0-windows10.0.19041.0</TargetFrameworks>

    <OutputType>Exe</OutputType>
    <RootNamespace>MyApp</RootNamespace>
    <UseMaui>true</UseMaui>
    <SingleProject>true</SingleProject>

    <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetFrameworkIdentifier('$(TargetFramework)')) == 'ios'">13.0</SupportedOSPlatformVersion>
    <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetFrameworkIdentifier('$(TargetFramework)')) == 'maccatalyst'">14.0</SupportedOSPlatformVersion>
    <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetFrameworkIdentifier('$(TargetFramework)')) == 'android'">21.0</SupportedOSPlatformVersion>
    <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetFrameworkIdentifier('$(TargetFramework)')) == 'windows'">10.0.17763.0</SupportedOSPlatformVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Maui.Controls" Version="$(MauiVersion)" />
    <PackageReference Include="VisioForge.DotNet.MediaBlocks" Version="2026.11.6" />
  </ItemGroup>

  <!-- Windows-specific packages -->
  <ItemGroup Condition="$(TargetFramework.Contains('-windows'))">
    <PackageReference Include="VisioForge.CrossPlatform.Core.Windows.x64" Version="2026.11.3" />
    <PackageReference Include="VisioForge.CrossPlatform.Libav.Windows.x64.UPX" Version="2026.11.3" />
  </ItemGroup>

  <!-- Android-specific packages -->
  <ItemGroup Condition="$(TargetFramework.Contains('-android'))">
    <PackageReference Include="VisioForge.CrossPlatform.Core.Android" Version="2026.11.5" />
    <ProjectReference Include="..\AndroidDependency\VisioForge.Core.Android.X10.csproj" />
  </ItemGroup>

  <!-- iOS-specific packages -->
  <ItemGroup Condition="$(TargetFramework.Contains('-ios'))">
    <PackageReference Include="VisioForge.CrossPlatform.Core.iOS" Version="2026.11.5" />
  </ItemGroup>

  <!-- Mac Catalyst-specific packages -->
  <ItemGroup Condition="$(TargetFramework.Contains('-maccatalyst'))">
    <PackageReference Include="VisioForge.CrossPlatform.Core.macCatalyst" Version="2026.11.5" />
  </ItemGroup>
</Project>
```

## Critical: Mac Catalyst Native Library Copying

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
  <ItemGroup>
    <_NativeLibraries Include="@(None)" Condition="'%(Extension)' == '.dylib' Or '%(Extension)' == '.so'" />
  </ItemGroup>
  <Copy SourceFiles="@(_NativeLibraries->'%(FullPath)')" DestinationFolder="$(MonoBundleDir)">
    <Output TaskParameter="CopiedFiles" ItemName="CopiedNativeFiles" />
  </Copy>
  <Message Text="Copied @(_NativeLibraries->Count()) native libraries" Importance="High" />
</Target>
```

## Permissions

### Android (Platforms/Android/AndroidManifest.xml)
```xml
<uses-permission android:name="android.permission.CAMERA" />
<uses-permission android:name="android.permission.RECORD_AUDIO" />
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
```

### iOS/Mac Catalyst (Platforms/iOS/Info.plist)
```xml
<key>NSCameraUsageDescription</key>
<string>This app requires camera access for video recording</string>

<key>NSMicrophoneUsageDescription</key>
<string>This app requires microphone access for audio recording</string>
```

## XAML Integration

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="MyApp.MainPage">
    <Grid>
        <Image x:Name="VideoImage" />
    </Grid>
</ContentPage>
```

## Code Example

```csharp
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;
using VisioForge.Core.MediaBlocks.VideoRendering;

public partial class MainPage : ContentPage
{
    private MediaBlocksPipeline? _pipeline;

    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        _pipeline = new MediaBlocksPipeline();
        var camera = new VideoCaptureDeviceSourceBlock(0);
        var renderer = new VideoRendererBlock(_pipeline, VideoImage);

        _pipeline.Connect(camera.VideoOutput, renderer.Input);
        await _pipeline.StartAsync();
    }
}
```

## Deployment

### Android
```bash
dotnet build -c Release -f net10.0-android
dotnet publish -c Release -f net10.0-android -p:AndroidPackageFormat=aab
```

### iOS
```bash
dotnet build -c Release -f net10.0-ios
```

### Mac Catalyst
```bash
dotnet build -c Release -f net10.0-maccatalyst
```

### Windows
```bash
dotnet build -c Release -f net10.0-windows10.0.19041.0
```

## Platform-Specific Notes

- **Android**: Requires AndroidDependency project reference for Java bindings
- **iOS**: Requires provisioning profile and code signing certificate
- **Mac Catalyst**: **CRITICAL** - Must use CopyNativeLibrariesToMonoBundle target
- **Windows**: Works like WPF/WinForms, no special requirements

## Troubleshooting

- **Mac Catalyst libs not found**: Verify CopyNativeLibrariesToMonoBundle target executed
- **Android build failed**: Ensure AndroidDependency project is included
- **iOS provisioning**: Check Apple Developer account and certificates
- **MAUI workload missing**: Run `dotnet workload install maui`

## Next Steps

- [Android deployment](../platforms/android-maui.md)
- [iOS deployment](../platforms/ios-maui.md)
- [Mac Catalyst deployment](../platforms/maccatalyst-maui.md)
- [Windows deployment](../platforms/windows-desktop.md)
