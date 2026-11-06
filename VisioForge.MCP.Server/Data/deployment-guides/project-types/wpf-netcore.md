---
id: wpf-netcore
title: WPF .NET Core/8+ Deployment
category: project-type
platforms:
  - Windows
projectTypes:
  - WPF
sdkTypes:
  - MediaBlocks
  - VideoCapture
  - MediaPlayer
  - VideoEdit
tags:
  - desktop
  - windows
  - wpf
difficulty: beginner
summary: Deploy VisioForge SDK in WPF applications using .NET 8+ (formerly .NET Core) for modern Windows desktop apps.
---

# WPF .NET Core/8+ Deployment

## Overview

WPF on .NET 8+ provides modern, high-performance Windows desktop applications. VisioForge SDK fully supports WPF for video capture, playback, editing, and streaming.

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
    <UseWPF>true</UseWPF>
    <PlatformTarget>x64</PlatformTarget>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="VisioForge.DotNet.MediaBlocks" Version="2026.11.6" />
    <PackageReference Include="VisioForge.CrossPlatform.Core.Windows.x64" Version="2026.11.3" />
    <PackageReference Include="VisioForge.CrossPlatform.Libav.Windows.x64.UPX" Version="2026.11.3" />
  </ItemGroup>

  <!-- Optional: OpenCV for computer vision features -->
  <ItemGroup Condition="'$(Configuration)' == 'Release'">
    <PackageReference Include="VisioForge.CrossPlatform.OpenCV.Windows.x64" Version="2026.11.3" />
  </ItemGroup>
</Project>
```

## XAML Integration

```xml
<Window x:Class="MyApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:vf="clr-namespace:VisioForge.Core.UI.WPF;assembly=VisioForge.Core.UI.WPF"
        Title="Video Player" Height="600" Width="800">
    <Grid>
        <vf:VideoView x:Name="VideoView" />
        <StackPanel VerticalAlignment="Bottom" Orientation="Horizontal">
            <Button Content="Play" Click="PlayButton_Click" Margin="5" />
            <Button Content="Stop" Click="StopButton_Click" Margin="5" />
        </StackPanel>
    </Grid>
</Window>
```

## Code Example

```csharp
using System.Windows;
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;
using VisioForge.Core.MediaBlocks.VideoRendering;

namespace MyApp
{
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
            _pipeline.Connect(source.AudioOutput, renderer.AudioInput);

            await _pipeline.StartAsync();
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pipeline != null)
            {
                await _pipeline.StopAsync();
                _pipeline.Dispose();
                _pipeline = null;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _pipeline?.Dispose();
            base.OnClosed(e);
        }
    }
}
```

## Deployment

### Self-Contained Deployment

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Output: `bin\Release\net10.0-windows\win-x64\publish\MyApp.exe`

### Framework-Dependent Deployment

```bash
dotnet publish -c Release -r win-x64
```

Requires .NET 10 Runtime installed on target machine.

### MSIX Package (Windows Store)

```xml
<PropertyGroup>
  <GenerateAppxPackageOnBuild>true</GenerateAppxPackageOnBuild>
  <AppxPackageSigningEnabled>true</AppxPackageSigningEnabled>
</PropertyGroup>
```

## Project Configuration Notes

- **PlatformTarget**: Set to `x64` (required for VisioForge native libraries)
- **UseWPF**: Must be `true` to enable WPF support
- **OutputType**: `WinExe` for GUI applications
- **Nullable**: Recommended to enable for modern C# practices

## Migrating from .NET Framework

If migrating from WPF .NET Framework 4.x:

1. Change `TargetFramework` from `net472` to `net10.0-windows`
2. Remove `packages.config` and add PackageReference items
3. Update NuGet package versions to 2026.11.6
4. No code changes required - VisioForge API is identical

## Troubleshooting

- **Video not rendering**: Ensure VideoView control is properly bound
- **Native DLL not found**: Verify platform is set to x64, not AnyCPU
- **Codec errors**: Install VisioForge.CrossPlatform.Libav package
- **Performance issues**: Enable hardware acceleration in encoder settings

## Performance Tips

- Use GPU-accelerated rendering when available
- Enable hardware video decoding (H264, HEVC)
- Set `VideoView.RenderMode` to appropriate value for your use case
- Dispose pipelines promptly to free resources

## Next Steps

- [Windows deployment guide](../platforms/windows-desktop.md)
- [WPF .NET Framework](wpf-netfx.md)
- [WinForms deployment](winforms-netcore.md)
