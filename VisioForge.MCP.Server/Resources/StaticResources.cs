using System.ComponentModel;
using ModelContextProtocol.Server;

namespace VisioForge.MCP.Server.Resources;

public static class StaticResources
{
    public const string GettingStarted = @"# Getting Started with VisioForge .NET SDK

## Overview
VisioForge .NET SDK provides comprehensive media processing capabilities for .NET applications,
including video capture, editing, playback, and streaming across Windows, Linux, macOS, iOS, and Android.

## Two API Approaches

### 1. Core SDK Classes (High-Level)
Best for standard scenarios — video capture, playback, editing.

| Class | Purpose |
|-------|---------|
| `VideoCaptureCoreX` | Camera capture, screen recording, streaming |
| `VideoEditCoreX` | Video editing, trimming, merging, transcoding |
| `MediaPlayerCoreX` | Media file and stream playback |
| `MediaInfoReaderX` | Media file analysis and metadata extraction |
| `SimplePlayerCoreX` | Lightweight media playback |

### 2. Media Blocks Pipeline (Low-Level)
Best for custom pipelines — full control over media processing graph.

```csharp
// Create pipeline
var pipeline = new MediaBlocksPipeline();

// Add blocks (source → processing → encoder → sink)
var source = new RTSPSourceBlock(settings);
var encoder = new H264EncoderBlock(encoderSettings);
var sink = new MP4SinkBlock(sinkSettings);

// Connect blocks via pads
pipeline.Connect(source.VideoOutput, encoder.Input);
pipeline.Connect(encoder.Output, sink.CreateNewInput(MediaBlockPadMediaType.Video));

// Run
await pipeline.StartAsync();
```

## NuGet Packages
```xml
<PackageReference Include=""VisioForge.DotNet.Core"" Version=""*"" />
```

## Platform Support
- **Windows**: Full feature set (DirectShow + GStreamer backends)
- **Linux**: GStreamer backend (x64, ARM64)
- **macOS**: GStreamer backend
- **iOS**: GStreamer backend (limited features)
- **Android**: GStreamer backend (limited features)

## Quick Links
- Use `list_media_blocks` to explore available processing blocks
- Use `list_sdk_classes` to see core SDK classes
- Use `get_code_example` for working code snippets
- Use `search_api` to find classes by keyword
";

    public const string Architecture = @"# VisioForge SDK Architecture

## Layered Architecture

```
┌─────────────────────────────────────────────────┐
│          Your Application (.NET)                 │
├─────────────────────────────────────────────────┤
│   Core SDK Classes (VideoCaptureCoreX, etc.)    │  ← High-level API
├─────────────────────────────────────────────────┤
│         Media Blocks Pipeline                    │  ← Block-based processing
├─────────────────────────────────────────────────┤
│      GStreamer / DirectShow Backend              │  ← Native media engine
├─────────────────────────────────────────────────┤
│    Hardware (GPU, Camera, Audio Device)          │
└─────────────────────────────────────────────────┘
```

## Media Blocks Architecture

Media blocks are the fundamental building units. Each block:
- Has **input pads** (receive data) and **output pads** (send data)
- Has typed pads: Video, Audio, Subtitle, Metadata
- Is connected in a directed acyclic graph (pipeline)
- Wraps a GStreamer element

### Block Categories
- **Sources**: Camera, file, RTSP, RTMP, screen, virtual, NDI
- **Processing**: Resize, crop, rotate, effects, overlays, color conversion
- **Encoders**: H.264, HEVC, VP9, AV1, AAC, MP3, Opus, FLAC
- **Decoders**: Matching decoders for all encoder formats
- **Sinks**: MP4, MKV, WebM, RTMP, HLS, DASH, NDI, file

### Pipeline Connection Pattern
```
Source.VideoOutput → Processing.Input
Processing.Output → Encoder.Input
Encoder.Output → Sink.CreateNewInput(Video)
```

## Core SDK Classes

The high-level SDK classes internally build media block pipelines:
- `VideoCaptureCoreX` → Source + Encoder + Sink blocks
- `VideoEditCoreX` → Source + Timeline + Effects + Encoder + Sink
- `MediaPlayerCoreX` → Source + Decoder + Renderer blocks

## Event-Driven Architecture
- `OnError` — Error occurred
- `OnStart` — Processing started
- `OnStop` — Processing stopped
- `OnProgress` — Progress update (editing)
- `OnVideoFrameBuffer` — Raw video frame access
- `OnAudioFrameBuffer` — Raw audio frame access
";

    public const string PlatformMatrix = @"# Platform Compatibility Matrix

## Supported Platforms

| Feature | Windows | Linux | macOS | iOS | Android |
|---------|---------|-------|-------|-----|---------|
| Video Capture (Camera) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Screen Capture | ✅ | ✅ | ✅ | ❌ | ❌ |
| Video Playback | ✅ | ✅ | ✅ | ✅ | ✅ |
| Video Editing | ✅ | ✅ | ✅ | ✅ | ✅ |
| RTSP Source | ✅ | ✅ | ✅ | ✅ | ✅ |
| RTMP Streaming | ✅ | ✅ | ✅ | ✅ | ✅ |
| HLS Streaming | ✅ | ✅ | ✅ | ✅ | ✅ |
| NDI | ✅ | ✅ | ✅ | ❌ | ❌ |
| DeckLink | ✅ | ✅ | ✅ | ❌ | ❌ |
| H.264 HW Encoding | ✅ NVENC/QSV | ✅ NVENC/VA-API | ✅ VideoToolbox | ✅ | ✅ |
| HEVC HW Encoding | ✅ NVENC/QSV | ✅ NVENC/VA-API | ✅ VideoToolbox | ✅ | ✅ |
| OpenGL Rendering | ✅ | ✅ | ✅ | ✅ | ✅ |
| DirectShow Backend | ✅ | ❌ | ❌ | ❌ | ❌ |
| GStreamer Backend | ✅ | ✅ | ✅ | ✅ | ✅ |

## .NET Target Frameworks

| Framework | Status |
|-----------|--------|
| .NET 9.0 | ✅ Current |
| .NET 8.0 | ✅ LTS |
| .NET 7.0 | ✅ |
| .NET 6.0 | ✅ LTS |
| .NET Framework 4.7.2 | ✅ Windows only |
| .NET MAUI | ✅ Cross-platform UI |
| Avalonia | ✅ Cross-platform UI |
| Uno Platform | ✅ Cross-platform UI |
| WinUI 3 | ✅ Windows only |

## GStreamer Dependencies (Linux/macOS)
Install GStreamer before using the SDK:
```bash
# Ubuntu/Debian
sudo apt install libgstreamer1.0-dev gstreamer1.0-plugins-base gstreamer1.0-plugins-good gstreamer1.0-plugins-bad gstreamer1.0-plugins-ugly gstreamer1.0-libav

# macOS
brew install gstreamer gst-plugins-base gst-plugins-good gst-plugins-bad gst-plugins-ugly gst-libav
```
";

    public const string MigrationGuide = @"# Migration Guide: Legacy → Modern API (X-suffix)

## Why Migrate?
The modern X-suffix APIs (VideoCaptureCoreX, etc.) provide:
- Cross-platform support (Windows, Linux, macOS, iOS, Android)
- GStreamer backend (more codec support, better performance)
- Async/await pattern throughout
- Media blocks pipeline for custom processing

## Class Mapping

| Legacy (Windows only) | Modern (Cross-platform) |
|-----------------------|------------------------|
| `VideoCaptureCore` | `VideoCaptureCoreX` |
| `VideoEditCore` | `VideoEditCoreX` |
| `MediaPlayerCore` | `MediaPlayerCoreX` |

## Key API Differences

### Starting/Stopping
```csharp
// Legacy
videoCapture.Start();
videoCapture.Stop();

// Modern
await capture.StartAsync();
await capture.StopAsync();
```

### Source Configuration
```csharp
// Legacy
videoCapture.Video_CaptureDevice = ""Camera Name"";
videoCapture.Audio_CaptureDevice = ""Microphone Name"";

// Modern
capture.Video_Source = new VideoCaptureDeviceSourceSettings(device);
capture.Audio_Source = new AudioCaptureDeviceSourceSettings(device);
```

### Output Configuration
```csharp
// Legacy
videoCapture.Output_Format = new MP4Output();
videoCapture.Output_Filename = ""output.mp4"";

// Modern
var output = new MP4Output(""output.mp4"");
output.Video.Encoder = new H264EncoderSettings();
capture.Outputs_Add(output, true);
```

## Media Blocks (New)
The modern API also exposes the media blocks pipeline for custom processing.
Use `get_code_example` with 'pipeline-basic' to see how to build custom pipelines.
";
}
