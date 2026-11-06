---
id: blazor-server
title: Blazor Server Deployment
category: project-type
platforms:
  - Windows
  - Linux
  - macOS
projectTypes:
  - Blazor
sdkTypes:
  - MediaBlocks
tags:
  - web
  - server
  - blazor
  - cross-platform
difficulty: advanced
summary: Deploy VisioForge SDK in Blazor Server applications for real-time video streaming to web browsers.
---

# Blazor Server Deployment

## Overview

Blazor Server enables real-time video streaming to web browsers using SignalR. VisioForge SDK processes video on the server and streams frames to clients.

## Prerequisites

- **.NET 10.0 SDK** or later
- **Web server** (IIS, Kestrel, Nginx)
- Platform-specific requirements:
  - **Linux**: GStreamer runtime
  - **macOS**: Xcode command-line tools and the `macos` workload (`dotnet workload install macos`)

## NuGet Packages

> **Pick the wrapper package for the SDK you licensed.** The snippets below show
> `VisioForge.DotNet.MediaBlocks`, but this guide applies to all four: use
> `VisioForge.DotNet.VideoCapture` for camera, screen and IP-camera recording,
> `VisioForge.DotNet.MediaPlayer` for playback, `VisioForge.DotNet.VideoEdit` for
> timeline editing. Media Blocks is the low-level pipeline layer. The native
> redist packages are the same whichever you choose.

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <!-- Conditional TargetFramework based on build OS -->
    <TargetFramework Condition="$([MSBuild]::IsOsPlatform('MacOS'))">net10.0-macos</TargetFramework>
    <ApplicationId Condition="$([MSBuild]::IsOsPlatform('MacOS'))">com.yourcompany.myblazorapp</ApplicationId>
    <ServerGarbageCollection Condition="$([MSBuild]::IsOsPlatform('MacOS'))">false</ServerGarbageCollection>
    <!-- dotnet run starts a macOS bundle through `open`, which shows no output and returns no exit code. -->
    <RunWithOpen Condition="$([MSBuild]::IsOsPlatform('MacOS'))">false</RunWithOpen>
    <!-- Release builds osx-x64 and osx-arm64, and a web project's static web assets manifest then lands only in a per-RID folder. -->
    <RuntimeIdentifier Condition="$([MSBuild]::IsOsPlatform('MacOS')) and '$(RuntimeIdentifier)' == ''">$(NETCoreSdkRuntimeIdentifier)</RuntimeIdentifier>
    <TargetFrameworks Condition="$([MSBuild]::IsOsPlatform('Windows'))">net10.0-windows</TargetFrameworks>
    <TargetFramework Condition="$([MSBuild]::IsOsPlatform('Linux'))">net10.0</TargetFramework>
    <!-- A hardcoded x64 target is wrong on Apple silicon. -->
    <PlatformTarget Condition="!$([MSBuild]::IsOsPlatform('OSX'))">x64</PlatformTarget>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="VisioForge.DotNet.MediaBlocks" Version="2026.11.6" />
  </ItemGroup>

  <!-- Windows-specific packages -->
  <ItemGroup Condition="$([MSBuild]::IsOsPlatform('Windows'))">
    <PackageReference Include="VisioForge.CrossPlatform.Core.Windows.x64" Version="2026.11.3" />
    <PackageReference Include="VisioForge.CrossPlatform.Libav.Windows.x64.UPX" Version="2026.11.3" />
  </ItemGroup>

  <!-- macOS-specific packages -->
  <ItemGroup Condition="$(TargetFramework.Contains('-macos'))">
    <PackageReference Include="VisioForge.CrossPlatform.Core.macOS" Version="2026.11.5" />
  </ItemGroup>

  <!-- Linux uses system GStreamer -->

  <!-- A macOS .app bundle holds no web files, and the bundle is the content root (see Program.cs). -->
  <Target Name="CopyWebAssetsToBundle" AfterTargets="_CreateAppBundle" BeforeTargets="_CodesignAppBundle" Condition="$([MSBuild]::IsOsPlatform('MacOS'))">
    <ItemGroup>
      <_BundledWebAsset Include="@(StaticWebAsset)" Condition="'%(StaticWebAsset.AssetRole)' == 'Primary' and !$([System.String]::Copy('%(StaticWebAsset.Identity)').Contains('{0}'))">
        <AssetRelPath>%(StaticWebAsset.RelativePath)</AssetRelPath>
      </_BundledWebAsset>
    </ItemGroup>
    <Error Condition="'@(_BundledWebAsset)' == ''" Text="No static web assets found for the macOS app bundle." />
    <!-- Copy only, never prune: a removed asset does not trigger a re-sign, so the seal goes stale. -->
    <Copy SourceFiles="@(_BundledWebAsset)" DestinationFiles="@(_BundledWebAsset->'$(AppBundleDir)/Contents/Resources/wwwroot/$([System.String]::Copy('%(AssetRelPath)').Replace('#[.{fingerprint}]?', ''))')" SkipUnchangedFiles="true" />
  </Target>
</Project>
```

## Razor Component Example

```razor
@page "/video-player"
@inject IVideoService VideoService
@implements IDisposable

<h3>Video Player</h3>

<div class="video-container">
    @if (!string.IsNullOrEmpty(frameDataUrl))
    {
        <img src="@frameDataUrl" alt="Video Frame" class="video-frame" />
    }
</div>

<div class="controls">
    <button class="btn btn-primary" @onclick="StartVideo">Play</button>
    <button class="btn btn-danger" @onclick="StopVideo">Stop</button>
</div>

@code {
    private string? frameDataUrl;
    private System.Timers.Timer? updateTimer;

    protected override void OnInitialized()
    {
        updateTimer = new System.Timers.Timer(33); // ~30 FPS
        updateTimer.Elapsed += async (sender, e) => await UpdateFrame();
    }

    private async Task StartVideo()
    {
        await VideoService.StartAsync();
        updateTimer?.Start();
    }

    private async Task StopVideo()
    {
        updateTimer?.Stop();
        await VideoService.StopAsync();
        frameDataUrl = null;
    }

    private async Task UpdateFrame()
    {
        var frame = await VideoService.GetCurrentFrameAsync();
        if (frame != null)
        {
            frameDataUrl = $"data:image/jpeg;base64,{Convert.ToBase64String(frame)}";
            await InvokeAsync(StateHasChanged);
        }
    }

    public void Dispose()
    {
        updateTimer?.Dispose();
    }
}
```

## Video Service Implementation

```csharp
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;
using VisioForge.Core.MediaBlocks.Sinks;

public interface IVideoService
{
    Task StartAsync();
    Task StopAsync();
    Task<byte[]?> GetCurrentFrameAsync();
}

public class VideoService : IVideoService, IDisposable
{
    private MediaBlocksPipeline? _pipeline;
    private MemoryStream? _currentFrame;
    private readonly SemaphoreSlim _frameLock = new(1, 1);

    public async Task StartAsync()
    {
        _pipeline = new MediaBlocksPipeline();

        var source = new UniversalSourceBlock("rtsp://camera.example.com/stream");
        var sink = new ImageSinkBlock();

        sink.OnFrame += async (sender, e) =>
        {
            await _frameLock.WaitAsync();
            try
            {
                _currentFrame = new MemoryStream(e.FrameData);
            }
            finally
            {
                _frameLock.Release();
            }
        };

        _pipeline.Connect(source.VideoOutput, sink.Input);
        await _pipeline.StartAsync();
    }

    public async Task StopAsync()
    {
        if (_pipeline != null)
        {
            await _pipeline.StopAsync();
            _pipeline.Dispose();
            _pipeline = null;
        }
    }

    public async Task<byte[]?> GetCurrentFrameAsync()
    {
        await _frameLock.WaitAsync();
        try
        {
            return _currentFrame?.ToArray();
        }
        finally
        {
            _frameLock.Release();
        }
    }

    public void Dispose()
    {
        _pipeline?.Dispose();
        _frameLock.Dispose();
    }
}
```

## Program.cs Configuration

```csharp
#if __MACOS__
// A macOS exe has an AppKit context with no run loop; continuations posted to it never run.
System.Threading.SynchronizationContext.SetSynchronizationContext(null);

// The bundle root holds no web files; the static assets are copied into Contents/Resources.
var appRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "Resources"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = appRoot });
#else
var builder = WebApplication.CreateBuilder(args);
#endif

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<IVideoService, VideoService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
```

## Deployment

### Windows (IIS)

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

Configure IIS application pool for out-of-process hosting.

### Linux (Ubuntu with Nginx)

```bash
dotnet publish -c Release -r linux-x64 --self-contained
```

Nginx reverse proxy configuration:

```nginx
server {
    listen 80;
    server_name example.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

### macOS (app bundle)

`dotnet publish` writes a `.app` bundle for the host CPU. Publish one architecture at a time — the project snippet pins a single `RuntimeIdentifier` because a universal binary breaks the static web assets manifest:

```bash
# Apple silicon
dotnet publish -c Release -r osx-arm64

# Intel
dotnet publish -c Release -r osx-x64
```

Run the server straight from the bundle:

```bash
# The RID you published: osx-arm64 or osx-x64
RID=osx-arm64
./bin/Release/net10.0-macos/$RID/publish/MyBlazorApp.app/Contents/MacOS/MyBlazorApp
```

Two things are specific to a Web SDK project on macOS:

- **The bundle is the content root.** `Program.cs` points `ContentRootPath` at `Contents/Resources`, and the `CopyWebAssetsToBundle` target in the project file puts `wwwroot` there. Without both, the app starts and every static file returns 404.
- **`RunWithOpen` is `false`.** With the default, `dotnet run` launches the bundle through `open`, which prints nothing and returns no exit code, so a startup failure looks like silence.

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble

# Install GStreamer for Linux
RUN apt-get update && apt-get install -y \
    gstreamer1.0-tools \
    gstreamer1.0-plugins-base \
    gstreamer1.0-plugins-good \
    gstreamer1.0-plugins-bad \
    gstreamer1.0-plugins-ugly \
    gstreamer1.0-libav \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY bin/Release/net10.0/linux-x64/publish/ .

EXPOSE 80
ENTRYPOINT ["dotnet", "MyBlazorApp.dll"]
```

## Performance Considerations

- **Frame rate**: Limit to 15-30 FPS for web streaming
- **Image compression**: Use JPEG with quality 70-85 for balance
- **SignalR**: Configure message size limits for large frames
- **Memory**: Dispose frames promptly to avoid memory leaks
- **Concurrency**: Use separate pipeline per user session if needed

## appsettings.json

```json
{
  "Kestrel": {
    "Limits": {
      "MaxRequestBodySize": 52428800
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

## Troubleshooting

- **SignalR disconnects**: Increase SignalR timeout limits
- **High CPU usage**: Reduce frame rate or resolution
- **Memory growth**: Ensure proper disposal of frame buffers
- **RTSP connection fails**: Check firewall and network settings
- **Linux GStreamer errors**: Install all required plugin packages

## Use Cases

- **IP camera monitoring**: Display multiple RTSP streams in browser
- **Video transcoding dashboard**: Monitor encoding jobs in real-time
- **Security systems**: Web-based surveillance interfaces
- **Remote video inspection**: Industrial inspection via web

## Next Steps

- [Linux deployment](../platforms/linux-ubuntu.md)
- [Windows deployment](../platforms/windows-desktop.md)
- [macOS deployment](../platforms/macos-desktop.md)
- [Console deployment](console-cross.md)
