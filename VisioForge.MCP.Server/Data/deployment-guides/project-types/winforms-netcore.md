---
id: winforms-netcore
title: WinForms .NET Core/8+ Deployment
category: project-type
platforms:
  - Windows
projectTypes:
  - WinForms
sdkTypes:
  - MediaBlocks
  - VideoCapture
  - MediaPlayer
  - VideoEdit
tags:
  - desktop
  - windows
  - winforms
difficulty: beginner
summary: Deploy VisioForge SDK in Windows Forms applications using .NET 8+ for modern Windows desktop development.
---

# WinForms .NET Core/8+ Deployment

## Overview

Windows Forms on .NET 8+ provides a proven, efficient framework for Windows desktop applications. VisioForge SDK fully supports WinForms for all video operations.

## Prerequisites

- **Visual Studio 2022** or **VS Code**
- **.NET 10.0 SDK** or later
- **Windows 10** or later

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
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <PlatformTarget>x64</PlatformTarget>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="VisioForge.DotNet.MediaBlocks" Version="2026.11.6" />
    <PackageReference Include="VisioForge.CrossPlatform.Core.Windows.x64" Version="2026.11.3" />
    <PackageReference Include="VisioForge.CrossPlatform.Libav.Windows.x64.UPX" Version="2026.11.3" />
  </ItemGroup>
</Project>
```

## Form Designer Integration

Add VideoView control to your form via code (VisioForge controls are not in Visual Studio toolbox):

```csharp
using System.Windows.Forms;
using VisioForge.Core.UI.WinForms;

public partial class MainForm : Form
{
    private VideoView videoView;

    public MainForm()
    {
        InitializeComponent();
        InitializeVideoView();
    }

    private void InitializeVideoView()
    {
        videoView = new VideoView
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black
        };

        panel1.Controls.Add(videoView);
    }
}
```

## Code Example

```csharp
using System.Windows.Forms;
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;
using VisioForge.Core.MediaBlocks.VideoRendering;
using VisioForge.Core.MediaBlocks.Sinks;

namespace VideoApp
{
    public partial class MainForm : Form
    {
        private MediaBlocksPipeline? _pipeline;
        private VideoView? _videoView;

        public MainForm()
        {
            InitializeComponent();
            InitializeVideoView();
        }

        private void InitializeVideoView()
        {
            _videoView = new VideoView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };
            panelVideo.Controls.Add(_videoView);
        }

        private async void btnPlay_Click(object sender, EventArgs e)
        {
            _pipeline = new MediaBlocksPipeline();

            var source = new UniversalSourceBlock("video.mp4");
            var renderer = new VideoRendererBlock(_pipeline, _videoView);

            _pipeline.Connect(source.VideoOutput, renderer.Input);
            _pipeline.Connect(source.AudioOutput, renderer.AudioInput);

            await _pipeline.StartAsync();
        }

        private async void btnCapture_Click(object sender, EventArgs e)
        {
            _pipeline = new MediaBlocksPipeline();

            var camera = new VideoCaptureDeviceSourceBlock(0);
            var renderer = new VideoRendererBlock(_pipeline, _videoView);
            var sink = new MP4SinkBlock("recording.mp4", new H264VideoEncoderSettings());

            _pipeline.Connect(camera.VideoOutput, renderer.Input);
            _pipeline.Connect(camera.VideoOutput, sink.VideoInput);
            _pipeline.Connect(camera.AudioOutput, sink.AudioInput);

            await _pipeline.StartAsync();
        }

        private async void btnStop_Click(object sender, EventArgs e)
        {
            if (_pipeline != null)
            {
                await _pipeline.StopAsync();
                _pipeline.Dispose();
                _pipeline = null;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _pipeline?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
```

## Deployment

### Self-Contained Deployment

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

### Framework-Dependent Deployment

```bash
dotnet publish -c Release -r win-x64
```

### Installer Creation

Use **Inno Setup**, **WiX**, or **NSIS** to create installers:

```iss
[Setup]
AppName=My Video App
AppVersion=1.0
DefaultDirName={pf}\MyVideoApp
OutputDir=.\Installer
OutputBaseFilename=MyVideoApp-Setup

[Files]
Source: "bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs
```

## Project Configuration

- **UseWindowsForms**: Must be `true` to enable WinForms
- **PlatformTarget**: Set to `x64` (required for VisioForge)
- **OutputType**: `WinExe` to hide console window
- **ApplicationManifest**: For DPI awareness and compatibility settings

## DPI Awareness

Add to app.manifest for high-DPI support:

```xml
<application xmlns="urn:schemas-microsoft-com:asm.v3">
  <windowsSettings>
    <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/PM</dpiAware>
    <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
  </windowsSettings>
</application>
```

## Migrating from .NET Framework

1. Change `TargetFramework` from `net472` to `net10.0-windows`
2. Remove `packages.config`, add PackageReference items
3. Update NuGet packages to 2026.11.6
4. No API changes - VisioForge code remains identical

## Troubleshooting

- **VideoView not visible**: Ensure control is added to form's Controls collection
- **Black screen**: Check if pipeline is started and connected properly
- **Native DLL errors**: Verify x64 platform target (not AnyCPU)
- **Form flicker**: Enable DoubleBuffered on parent containers

## Threading Best Practices

```csharp
// Always invoke UI updates on UI thread
private void Pipeline_OnFrame(object? sender, FrameEventArgs e)
{
    if (InvokeRequired)
    {
        Invoke(new Action(() => UpdateUI()));
    }
    else
    {
        UpdateUI();
    }
}
```

## Next Steps

- [Windows deployment guide](../platforms/windows-desktop.md)
- [WPF deployment](wpf-netcore.md)
- [Console deployment](console-cross.md)
