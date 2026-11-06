---
id: windows-desktop
title: Windows Desktop Application Deployment (WPF/WinForms)
category: platform
platforms:
  - Windows
projectTypes:
  - WPF
  - WinForms
  - Console
sdkTypes:
  - MediaBlocks
  - VideoCapture
  - MediaPlayer
  - VideoEdit
tags:
  - desktop
  - windows
  - nuget
difficulty: beginner
summary: Complete guide for deploying VisioForge SDK in Windows desktop applications using WPF, WinForms, or Console with .NET Core/.NET Framework.
---

# Windows Desktop Application Deployment

## Overview

Deploying VisioForge SDK to Windows desktop applications (WPF, WinForms, or Console) is straightforward using NuGet packages. This guide covers both .NET (.NET 6-10) and .NET Framework (4.7.2+) deployment.

## Prerequisites

- **Visual Studio 2022** or later (or JetBrains Rider)
- **.NET 10.0 SDK** or later (for .NET apps)
- **.NET Framework 4.7.2+** Developer Pack (for .NET Framework apps)
- **Windows 10/11** (x64 architecture)

## Quick Start

### Step 1: Create Your Project

```bash
# WPF (.NET)
dotnet new wpf -n MyVideoApp

# WinForms (.NET)
dotnet new winforms -n MyVideoApp

# Console (.NET)
dotnet new console -n MyVideoApp
```

### Step 2: Add NuGet Packages

Add the required VisioForge packages to your `.csproj` file:

```xml
<ItemGroup>
  <!-- Core SDK (choose one based on your needs) -->
  <PackageReference Include="VisioForge.DotNet.MediaBlocks" Version="2026.11.6" />

  <!-- Windows platform binaries -->
  <PackageReference Include="VisioForge.CrossPlatform.Core.Windows.x64" Version="2026.11.3" />

  <!-- FFmpeg/Libav codecs (compressed version) -->
  <PackageReference Include="VisioForge.CrossPlatform.Libav.Windows.x64.UPX" Version="2026.11.3" />
</ItemGroup>
```

### Step 3: Configure Project Settings

Ensure your project targets x64 architecture:

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType> <!-- or Exe for console -->
  <TargetFramework>net10.0-windows</TargetFramework>
  <PlatformTarget>x64</PlatformTarget>
  <UseWPF>true</UseWPF> <!-- For WPF apps -->
  <!-- <UseWindowsForms>true</UseWindowsForms> --> <!-- For WinForms apps -->
</PropertyGroup>
```

### Step 4: Build and Run

```bash
dotnet build
dotnet run
```

## NuGet Package Details

> **Pick the wrapper package for the SDK you licensed.** The snippets below show
> `VisioForge.DotNet.MediaBlocks`, but this guide applies to all four: use
> `VisioForge.DotNet.VideoCapture` for camera, screen and IP-camera recording,
> `VisioForge.DotNet.MediaPlayer` for playback, `VisioForge.DotNet.VideoEdit` for
> timeline editing. Media Blocks is the low-level pipeline layer. The native
> redist packages are the same whichever you choose.

### Core SDK Packages

Choose the SDK package based on your use case:

| Package | Version | Use Case |
|---------|---------|----------|
| `VisioForge.DotNet.MediaBlocks` | 2026.11.6 | Modern pipeline-based processing |
| `VisioForge.DotNet.VideoCapture` | 2026.11.6 | Video capture and encoding |
| `VisioForge.DotNet.MediaPlayer` | 2026.11.6 | Media playback |
| `VisioForge.DotNet.VideoEdit` | 2026.11.6 | Video editing and conversion |

### Required Platform Packages

```xml
<!-- Required for Windows x64 -->
<PackageReference Include="VisioForge.CrossPlatform.Core.Windows.x64" Version="2026.11.3" />

<!-- FFmpeg/Libav codecs (compressed) -->
<PackageReference Include="VisioForge.CrossPlatform.Libav.Windows.x64.UPX" Version="2026.11.3" />
```

### Optional Packages

```xml
<!-- Optional Packages -->
<ItemGroup>
  <!-- Computer Vision (OpenCV) -->
  <PackageReference Include="VisioForge.CrossPlatform.OpenCV.Windows.x64" Version="2026.11.3" />

  <!-- AWS S3 Integration -->
  <PackageReference Include="VisioForge.CrossPlatform.AWS.Windows.x64" Version="2026.11.3" />

  <!-- Industrial Camera Support -->
  <PackageReference Include="VisioForge.CrossPlatform.Basler.Windows.x64" Version="2026.11.3" />
  <PackageReference Include="VisioForge.CrossPlatform.Spinnaker.Windows.x64" Version="2026.11.3" />
  <PackageReference Include="VisioForge.CrossPlatform.AlliedVision.Windows.x64" Version="2026.11.3" />
</ItemGroup>
```

## Deployment Methods

### Method 1: Framework-Dependent Deployment (Recommended)

Requires .NET Runtime installed on target machine. Smaller deployment size.

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

**Output**: `bin\Release\net10.0-windows\win-x64\publish\`

**Deployment**: Copy entire publish folder to target machine. User needs .NET 10 Runtime.

### Method 2: Self-Contained Deployment

Includes .NET runtime. Larger but doesn't require .NET installation.

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

**Output**: Includes all DLLs and runtime (~120-150 MB)

### Method 3: Single-File Executable

Bundles everything into one .exe file:

```xml
<PropertyGroup>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
</PropertyGroup>
```

```bash
dotnet publish -c Release
```

**Note**: VisioForge native DLLs are extracted to temp directory at runtime.

## Platform-Specific Configuration

### Windows-Specific Features

- **DirectShow Backend**: Available on Windows only for legacy capture devices
- **Hardware Encoding**: Support for NVIDIA NVENC, Intel QuickSync, AMD VCE
- **DeckLink**: Blackmagic DeckLink capture card support

### Architecture Requirements

- **x64 Required**: VisioForge SDK requires 64-bit Windows
- **x86 Support**: Limited codec support, not recommended for production

### Application Manifest

For Windows 10/11 compatibility, add this manifest:

```xml
<PropertyGroup>
  <ApplicationManifest>app.manifest</ApplicationManifest>
</PropertyGroup>
```

`app.manifest`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <!-- Windows 10/11 -->
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/>
    </application>
  </compatibility>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges>
        <requestedExecutionLevel level="asInvoker" uiAccess="false"/>
      </requestedPrivileges>
    </security>
  </trustInfo>
</assembly>
```

## Code Example

### Media Blocks SDK - Simple Video Player

```csharp
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;
using VisioForge.Core.MediaBlocks.VideoRendering;
using VisioForge.Core.Types.X.VideoCapture;

public partial class MainWindow : Window
{
    private MediaBlocksPipeline? _pipeline;

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        // Create pipeline
        _pipeline = new MediaBlocksPipeline();

        // Create source (file, RTSP, HTTP, etc.)
        var source = new UniversalSourceBlock("video.mp4");

        // Create video renderer
        var renderer = new VideoRendererBlock(_pipeline, videoView);

        // Connect blocks
        _pipeline.Connect(source.VideoOutput, renderer.Input);

        // Start playback
        await _pipeline.StartAsync();
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pipeline != null)
        {
            await _pipeline.StopAsync();
            await _pipeline.DisposeAsync();
            _pipeline = null;
        }
    }
}
```

## Troubleshooting

### Issue: Missing DLLs

**Symptom**: Application crashes with "DLL not found" error

**Solution**:
- Ensure all NuGet packages are restored: `dotnet restore`
- Check platform target is x64: `<PlatformTarget>x64</PlatformTarget>`
- Rebuild in Release configuration

### Issue: GStreamer Initialization Failed

**Symptom**: `GStreamer initialization failed` exception

**Solution**:
- Verify `VisioForge.CrossPlatform.Core.Windows.x64` package is installed
- Check Windows Event Viewer for detailed error messages
- Ensure x64 architecture (not AnyCPU or x86)

### Issue: Codec Not Available

**Symptom**: "Codec H.264 not available" error

**Solution**:
- Install `VisioForge.CrossPlatform.Libav.Windows.x64.UPX` package
- For additional codecs, install full `Libav.Windows.x64` (non-UPX) package

### Issue: Antivirus False Positive

**Symptom**: Antivirus blocks VisioForge DLLs

**Solution**:
- Add exclusion for your application folder
- Consider code signing your application for distribution
- Use digital signatures from trusted certificate authority

## Performance Optimization

### Hardware Acceleration

Enable hardware encoding/decoding when available:

```csharp
var encoderSettings = new H264EncoderSettings
{
    HardwareAcceleration = true // NVIDIA/Intel/AMD GPU
};
```

### Memory Management

Dispose pipelines properly to free resources:

```csharp
await using (var pipeline = new MediaBlocksPipeline())
{
    // Use pipeline
} // Automatically disposed
```

## Distribution Checklist

- [ ] Build in Release configuration
- [ ] Test on clean machine without Visual Studio
- [ ] Verify x64 architecture
- [ ] Include Visual C++ Redistributable if needed
- [ ] Test with Windows Defender enabled
- [ ] Consider code signing for production deployment
- [ ] Include license file and SDK documentation

## Next Steps

- **MAUI Desktop**: For cross-platform WPF-style apps, see [MAUI deployment guide](maui-desktop.md)
- **Advanced Features**: Explore [Media Blocks scenarios](../scenarios/)
- **Video Capture**: See [Video Capture SDK guide](../../videocapture/deployment.md)

## Support

- **Documentation**: https://www.visioforge.com/help
- **Examples**: https://github.com/visioforge/.Net-SDK-s-samples/tree/master/Media%20Blocks%20SDK
- **Forum**: https://www.visioforge.com/support
