---
id: uno-platform
title: Uno Platform Cross-Platform Deployment
category: project-type
platforms:
  - Windows
  - Android
  - iOS
  - macOS
projectTypes:
  - Uno
sdkTypes:
  - MediaBlocks
  - VideoCapture
  - MediaPlayer
tags:
  - cross-platform
  - mobile
  - desktop
  - uno
difficulty: advanced
summary: Deploy VisioForge SDK with Uno Platform for truly cross-platform applications targeting Windows, Android, iOS, and Mac Catalyst.
---

# Uno Platform Cross-Platform Deployment

## Overview

Uno Platform enables single-codebase apps for Windows, Android, iOS, and Mac Catalyst. VisioForge SDK supports Uno for cross-platform video applications.

## Prerequisites

- **.NET 10.0 SDK** or later
- **Uno Platform templates**: `dotnet new install Uno.Templates`
- **Platform-specific tools**:
  - Windows: Visual Studio 2022
  - macOS: Xcode 14+
  - Android: Android SDK API 21+

## NuGet Packages

> **Pick the wrapper package for the SDK you licensed.** The snippets below show
> `VisioForge.DotNet.MediaBlocks`, but this guide applies to all four: use
> `VisioForge.DotNet.VideoCapture` for camera, screen and IP-camera recording,
> `VisioForge.DotNet.MediaPlayer` for playback, `VisioForge.DotNet.VideoEdit` for
> timeline editing. Media Blocks is the low-level pipeline layer. The native
> redist packages are the same whichever you choose.

```xml
<Project Sdk="Uno.Sdk">
  <PropertyGroup>
    <TargetFrameworks Condition="$([MSBuild]::IsOsPlatform('windows'))">
      net10.0-windows;net10.0-android;net10.0-ios;net10.0-maccatalyst
    </TargetFrameworks>
    <TargetFrameworks Condition="$([MSBuild]::IsOsPlatform('OSX'))">
      net10.0-ios;net10.0-maccatalyst
    </TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
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
  <Message Text="Copied @(_NativeLibraries->Count()) native libraries to $(MonoBundleDir)" Importance="High" />
</Target>
```

## Permissions

### Android (AndroidManifest.xml)
```xml
<uses-permission android:name="android.permission.CAMERA" />
<uses-permission android:name="android.permission.RECORD_AUDIO" />
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
```

### iOS/Mac Catalyst (Info.plist)
```xml
<key>NSCameraUsageDescription</key>
<string>This app requires camera access for video recording</string>

<key>NSMicrophoneUsageDescription</key>
<string>This app requires microphone access for audio recording</string>
```

## XAML Integration

```xml
<Page x:Class="MyApp.MainPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:vf="using:VisioForge.Core.UI.Uno">
    <Grid>
        <vf:VideoView x:Name="VideoView" />
    </Grid>
</Page>
```

## Code Example

```csharp
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;
using VisioForge.Core.MediaBlocks.VideoRendering;

public sealed partial class MainPage : Page
{
    private MediaBlocksPipeline? _pipeline;

    public MainPage()
    {
        this.InitializeComponent();
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        _pipeline = new MediaBlocksPipeline();
        var source = new UniversalSourceBlock("https://example.com/video.mp4");
        var renderer = new VideoRendererBlock(_pipeline, VideoView);

        _pipeline.Connect(source.VideoOutput, renderer.Input);
        await _pipeline.StartAsync();
    }
}
```

## Platform-Specific Deployment

### 1. Windows Desktop (WinUI 3)

Uno on Windows uses **WinUI 3**, not WPF/WinForms.

**Build:**
```bash
dotnet build -c Release -f net10.0-windows10.0.19041
```

**Publish (Self-Contained):**
```bash
dotnet publish -c Release -f net10.0-windows10.0.19041 --self-contained -r win-x64
```

**MSIX Package (Microsoft Store):**
```bash
dotnet publish -c Release -f net10.0-windows10.0.19041 -p:GenerateAppxPackageOnBuild=true
```

**Requirements:**
- Windows 10 version 1809 (build 17763) or later
- Windows App SDK via NuGet (Microsoft.WindowsAppSDK)
- x64 architecture

**Notes:**
- Output uses WinUI 3 controls, not WPF
- Native libraries included via NuGet packages
- No file copying required

---

### 2. Android

**Build APK:**
```bash
dotnet build -c Release -f net10.0-android
```

**Build AAB (Google Play):**
```bash
dotnet publish -c Release -f net10.0-android -p:AndroidPackageFormat=aab
```

**Install to Device:**
```bash
adb install -r bin/Release/net10.0-android/com.companyname.appname-Signed.apk
```

**Requirements:**
- Android SDK API 21+
- AndroidDependency project reference (for VisioForge Java bindings)
- Permissions in AndroidManifest.xml

**Notes:**
- **CRITICAL**: Must include AndroidDependency ProjectReference
- Runtime permissions required for camera/microphone (API 23+)
- No file copying required

---

### 3. iOS

**Build for Simulator:**
```bash
dotnet build -c Debug -f net10.0-ios
```

**Build for Physical Device:**
```bash
dotnet publish -c Release -f net10.0-ios \
  /p:ArchiveOnBuild=true \
  /p:CodesignKey="Apple Development" \
  /p:CodesignProvision="YourProvisioningProfile"
```

**TestFlight Distribution:**
1. Build with Release configuration
2. Archive with code signing
3. Upload to App Store Connect via Xcode or Transporter
4. Submit for TestFlight review
5. Distribute to testers

**Requirements:**
- macOS with Xcode 14+
- Apple Developer account ($99/year)
- Provisioning profile and code signing certificate
- Info.plist permissions (NSCameraUsageDescription, etc.)

**Notes:**
- Minimum iOS version: 13.0
- No file copying required
- Native libraries embedded in app bundle

---

### 4. Mac Catalyst

Mac Catalyst runs iOS apps natively on macOS with desktop-class features.

**Build:**
```bash
dotnet build -c Release -f net10.0-maccatalyst
```

**Publish:**
```bash
dotnet publish -c Release -f net10.0-maccatalyst --self-contained
```

**⚠️ CRITICAL FILE COPYING:**
Mac Catalyst requires the `CopyNativeLibrariesToMonoBundle` MSBuild target (shown above in project configuration).

**Requirements:**
- macOS 11.0 (Big Sur) or later
- Xcode 14+
- Code signing for distribution

**Notes:**
- **ONLY platform requiring file copying**
- .dylib and .so files copied to MonoBundle automatically
- Runs as native Mac app with iOS codebase
- Supports Mac-specific features (menu bar, keyboard shortcuts)

## Troubleshooting

- **Mac Catalyst libs not found**: Verify CopyNativeLibrariesToMonoBundle target executed
- **Android build failed**: Ensure AndroidDependency project is included
- **iOS provisioning**: Check signing settings in project properties
- **Video not rendering**: Verify platform-specific packages are installed

## Next Steps

- [Windows deployment](../platforms/windows-desktop.md)
- [Android deployment](../platforms/android-maui.md)
- [iOS deployment](../platforms/ios-maui.md)
- [Mac Catalyst deployment](../platforms/maccatalyst-maui.md)
