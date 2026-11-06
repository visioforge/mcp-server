using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using VisioForge.MCP.Server.Index;

namespace VisioForge.MCP.Server.Tools;

[McpServerToolType]
public static class ExampleTools
{
    [McpServerTool(ReadOnly = true, Idempotent = true, OpenWorld = false), Description(
        "Get a code example for a common VisioForge SDK scenario. " +
        "Returns complete, working C# code snippets that demonstrate how to use the SDK " +
        "for tasks like video capture, RTSP streaming, media playback, and more.")]
    public static string GetCodeExample(
        ApiIndexService index,
        [Description("Scenario name. Available: " +
            "'webcam-capture' (capture from webcam to MP4), " +
            "'rtsp-playback' (play RTSP stream), " +
            "'rtsp-to-mp4' (record RTSP to MP4 file), " +
            "'webcam-to-rtmp' (stream webcam to RTMP/YouTube/Facebook), " +
            "'file-playback' (play a media file), " +
            "'video-editing' (basic video editing), " +
            "'screen-capture' (capture screen to file), " +
            "'media-info' (read media file information), " +
            "'pipeline-basic' (build a basic media block pipeline), " +
            "'virtual-source' (generate test video/audio), " +
            "'hls-streaming' (stream to HLS), " +
            "'audio-capture' (capture audio only)")]
        string scenario)
    {
        return GetExample(scenario);
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, OpenWorld = false), Description(
        "Get a media block pipeline template for a specific use case. " +
        "Returns the list of required blocks and how to connect them, " +
        "along with C# code to build the pipeline.")]
    public static string GetPipelineTemplate(
        ApiIndexService index,
        [Description("Use case description. Available templates: " +
            "'rtsp-to-mp4' (record network camera to file), " +
            "'webcam-preview' (show webcam on screen), " +
            "'webcam-to-rtmp' (stream webcam live), " +
            "'file-transcode' (convert video file format), " +
            "'screen-to-hls' (screen capture to HLS stream), " +
            "'multi-source-mix' (mix multiple video sources), " +
            "'audio-only-capture' (record audio to file), " +
            "'ndi-output' (stream via NDI protocol)")]
        string useCase)
    {
        return GetTemplate(useCase);
    }

    private static string GetExample(string scenario)
    {
        return scenario.ToLower().Replace(" ", "-") switch
        {
            "webcam-capture" => @"# Webcam Capture to MP4

Capture video from a webcam and save to an MP4 file using VideoCaptureCoreX.

```csharp
using VisioForge.Core;
using VisioForge.Core.VideoCaptureX;
using VisioForge.Core.Types.X.VideoCapture;
using VisioForge.Core.Types.X.Output;
using VisioForge.Core.Types.X.Sources;

// Initialize the SDK once, before any core or pipeline is constructed.
// On MAUI use `await VisioForgeX.InitSDKAsync();` instead.
VisioForgeX.InitSDK();

// Create the capture core
await using var capture = new VideoCaptureCoreX();

// A purchased licence is applied on the core, before StartAsync:
// await capture.SetLicenseCertificateAsync(File.ReadAllBytes(""visioforge.vflicense""));

// Configure video source (first available camera)
var videoSources = await DeviceEnumerator.Shared.VideoSourcesAsync();
if (videoSources.Length > 0)
{
    capture.Video_Source = new VideoCaptureDeviceSourceSettings(videoSources[0]);
}

// Configure audio source (first available microphone)
var audioSources = await DeviceEnumerator.Shared.AudioSourcesAsync();
if (audioSources.Length > 0)
{
    capture.Audio_Source = new AudioCaptureDeviceSourceSettings(audioSources[0]);
}

// Configure MP4 output. Left to itself MP4Output uses the software encoder
// (OpenH264) on Windows and Linux — the hardware branches in its constructor are
// commented out. For NVENC / QSV / Media Foundation / VideoToolbox, pass the
// platform default explicitly:
//   new MP4Output(""output.mp4"", H264EncoderBlock.GetDefaultSettings())
var mp4Output = new MP4Output(""output.mp4"");
capture.Outputs_Add(mp4Output, true);

// Set the video view for preview (platform-specific)
// capture.Video_View = videoView;

// Start capture
await capture.StartAsync();

// ... recording ...

// Stop capture
await capture.StopAsync();
```

## Key Classes Used
- `VideoCaptureCoreX` — Main capture engine
- `VideoCaptureDeviceSourceSettings` — Camera source configuration
- `AudioCaptureDeviceSourceSettings` — Microphone source configuration
- `MP4Output` — MP4 file output; its `Video` property IS the encoder settings object
- `H264EncoderBlock.GetDefaultSettings()` — picks the best H.264 encoder for the platform
",
            "rtsp-playback" => @"# RTSP Stream Playback

Play an RTSP network camera stream using MediaPlayerCoreX.

```csharp
using VisioForge.Core;
using VisioForge.Core.MediaPlayerX;

// Initialize the SDK once, before any core or pipeline is constructed.
// On MAUI use `await VisioForgeX.InitSDKAsync();` instead.
VisioForgeX.InitSDK();

// Create the player
await using var player = new MediaPlayerCoreX();

// Set the video view for display
// player.Video_View = videoView;

// Open an RTSP stream
await player.OpenAsync(new Uri(""rtsp://camera-ip:554/stream""));

// Start playback
await player.PlayAsync();

// ... playing ...

// Stop playback
await player.StopAsync();
```

## For low-latency RTSP playback, use the Media Blocks pipeline:

```csharp
using VisioForge.Core;
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;
using VisioForge.Core.MediaBlocks.VideoRendering;
using VisioForge.Core.MediaBlocks.AudioRendering;
using VisioForge.Core.Types.X.Sources;

var pipeline = new MediaBlocksPipeline();

// RTSP source. RTSPSourceSettings has no public constructor taking a Uri —
// build it with the async factory, which also performs ONVIF resolution.
var rtspSettings = await RTSPSourceSettings.CreateAsync(
    new Uri(""rtsp://camera-ip:554/stream""),
    login: null,
    password: null,
    audioEnabled: true);
var rtspSource = new RTSPSourceBlock(rtspSettings);

// Video renderer
var videoRenderer = new VideoRendererBlock(pipeline, videoView);

// Audio renderer
var audioRenderer = new AudioRendererBlock();

// Connect: RTSP → Video Renderer + Audio Renderer
pipeline.Connect(rtspSource.VideoOutput, videoRenderer.Input);
pipeline.Connect(rtspSource.AudioOutput, audioRenderer.Input);

// Start
await pipeline.StartAsync();
```
",
            "rtsp-to-mp4" => @"# Record RTSP Stream to MP4

Record a network camera RTSP stream to an MP4 file using media blocks.

```csharp
using VisioForge.Core;
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;
using VisioForge.Core.MediaBlocks.VideoEncoders;
using VisioForge.Core.MediaBlocks.AudioEncoders;
using VisioForge.Core.MediaBlocks.Sinks;
using VisioForge.Core.Types.X.Sources;
using VisioForge.Core.Types.X.Sinks;

// Initialize the SDK once, before any core or pipeline is constructed.
// On MAUI use `await VisioForgeX.InitSDKAsync();` instead.
VisioForgeX.InitSDK();

var pipeline = new MediaBlocksPipeline();

// RTSP source — the settings ctor is private, use the async factory.
var rtspSettings = await RTSPSourceSettings.CreateAsync(
    new Uri(""rtsp://camera-ip:554/stream""),
    login: null,
    password: null,
    audioEnabled: true);
var rtspSource = new RTSPSourceBlock(rtspSettings);

// H.264 encoder. GetDefaultSettings() picks the best available encoder for the
// platform (NVENC / QSV / Media Foundation / VideoToolbox / OpenH264).
var videoEncoder = new H264EncoderBlock(H264EncoderBlock.GetDefaultSettings());

// AAC audio encoder
var audioEncoder = new AACEncoderBlock(AACEncoderBlock.GetDefaultSettings());

// MP4 file sink
var mp4Sink = new MP4SinkBlock(new MP4SinkSettings(""recording.mp4""));

// Connect the pipeline
pipeline.Connect(rtspSource.VideoOutput, videoEncoder.Input);
pipeline.Connect(videoEncoder.Output, mp4Sink.CreateNewInput(MediaBlockPadMediaType.Video));

pipeline.Connect(rtspSource.AudioOutput, audioEncoder.Input);
pipeline.Connect(audioEncoder.Output, mp4Sink.CreateNewInput(MediaBlockPadMediaType.Audio));

// Start recording
await pipeline.StartAsync();

// ... recording ...

// Stop recording
await pipeline.StopAsync();
```
",
            "webcam-to-rtmp" => @"# Stream Webcam to RTMP (YouTube/Facebook Live)

Stream webcam video to an RTMP server using media blocks.

```csharp
using VisioForge.Core;
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;
using VisioForge.Core.MediaBlocks.VideoEncoders;
using VisioForge.Core.MediaBlocks.AudioEncoders;
using VisioForge.Core.MediaBlocks.Sinks;
using VisioForge.Core.Types.X.Sources;
using VisioForge.Core.Types.X.Sinks;

// Initialize the SDK once, before any core or pipeline is constructed.
// On MAUI use `await VisioForgeX.InitSDKAsync();` instead.
VisioForgeX.InitSDK();

var pipeline = new MediaBlocksPipeline();

// System video source (first webcam). The blocks take the same device settings
// objects the VideoCaptureCoreX engine uses.
var cameras = await DeviceEnumerator.Shared.VideoSourcesAsync();
var mics = await DeviceEnumerator.Shared.AudioSourcesAsync();
if (cameras.Length == 0 || mics.Length == 0)
{
    return; // no capture hardware on this machine
}

var videoSource = new SystemVideoSourceBlock(
    new VideoCaptureDeviceSourceSettings(cameras[0]));

// System audio source (first microphone)
var audioSource = new SystemAudioSourceBlock(
    new AudioCaptureDeviceSourceSettings(mics[0]));

// H.264 encoder. GetDefaultSettings() returns IH264EncoderSettings, which has no
// Bitrate — the property lives on the concrete per-platform types, so set it there:
//   var sw = new OpenH264EncoderSettings { Bitrate = 4000 };  // 4 Mbps, software
//   var videoEncoder = new H264EncoderBlock(sw);
var videoEncoder = new H264EncoderBlock(H264EncoderBlock.GetDefaultSettings());

// AAC audio encoder
var audioEncoder = new AACEncoderBlock(AACEncoderBlock.GetDefaultSettings());

// RTMP sink (YouTube Live example). RTMPSinkSettings has no string ctor —
// the destination is the Location property.
var rtmpSink = new RTMPSinkBlock(new RTMPSinkSettings
{
    Location = ""rtmp://a.rtmp.youtube.com/live2/your-stream-key"",
});

// Connect
pipeline.Connect(videoSource.Output, videoEncoder.Input);
pipeline.Connect(videoEncoder.Output, rtmpSink.CreateNewInput(MediaBlockPadMediaType.Video));

pipeline.Connect(audioSource.Output, audioEncoder.Input);
pipeline.Connect(audioEncoder.Output, rtmpSink.CreateNewInput(MediaBlockPadMediaType.Audio));

// Start streaming
await pipeline.StartAsync();
```
",
            "file-playback" => @"# Media File Playback

Play a video/audio file using MediaPlayerCoreX.

```csharp
using VisioForge.Core;
using VisioForge.Core.MediaPlayerX;

// Initialize the SDK once, before any core or pipeline is constructed.
// On MAUI use `await VisioForgeX.InitSDKAsync();` instead.
VisioForgeX.InitSDK();

await using var player = new MediaPlayerCoreX();

// Set video view for display
// player.Video_View = videoView;

// Open file. OpenAsync also has a string overload for a plain path.
await player.OpenAsync(new Uri(""file:///path/to/video.mp4""));

// Play
await player.PlayAsync();

// Seek to position
await player.Position_SetAsync(TimeSpan.FromSeconds(30));

// Pause
await player.PauseAsync();

// Resume
await player.ResumeAsync();

// Get duration and position
var duration = await player.DurationAsync();
var position = await player.Position_GetAsync();

// Stop
await player.StopAsync();
```
",
            "video-editing" => @"# Basic Video Editing

Edit video files using VideoEditCoreX — trim, combine, add effects.

```csharp
using VisioForge.Core;
using VisioForge.Core.VideoEditX;
using VisioForge.Core.Types.X.VideoEdit;
using VisioForge.Core.Types.X.Output;

// Initialize the SDK once, before any core or pipeline is constructed.
// On MAUI use `await VisioForgeX.InitSDKAsync();` instead.
VisioForgeX.InitSDK();

// VideoEditCoreX is IDisposable, not IAsyncDisposable — plain `using`, unlike
// VideoCaptureCoreX and MediaPlayerCoreX above.
using var editor = new VideoEditCoreX();

// Append clips to the timeline. The (start, stop) overload trims the source:
// this takes the first 10 seconds of clip1 and seconds 10-20 of clip2.
editor.Input_AddVideoFile(""clip1.mp4"", TimeSpan.Zero, TimeSpan.FromSeconds(10));
editor.Input_AddVideoFile(""clip2.mp4"", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));

// Configure output. Output_Format is a property, and MP4Output picks a
// platform-appropriate H.264 encoder unless one is passed to its constructor.
editor.Output_Format = new MP4Output(""edited_output.mp4"");

// Progress tracking
editor.OnProgress += (sender, args) =>
{
    Console.WriteLine($""Progress: {args.Progress}%"");
};

editor.OnStop += (sender, args) =>
{
    Console.WriteLine(""Editing complete!"");
};

// Start editing. Start() is synchronous and returns success; the render itself runs
// in the background and reports through OnProgress / OnStop above — so something has
// to wait for OnStop, or a console app exits before a single frame is written.
var finished = new TaskCompletionSource<bool>();
editor.OnStop += (sender, args) => finished.TrySetResult(true);

if (!editor.Start())
{
    Console.WriteLine(""Failed to start rendering."");
    return;
}

await finished.Task;
```
",
            "screen-capture" => @"# Screen Capture

Capture screen content to a file using VideoCaptureCoreX.

```csharp
using VisioForge.Core;
using VisioForge.Core.VideoCaptureX;
using VisioForge.Core.Types.X.VideoCapture;
using VisioForge.Core.Types.X.Output;
using VisioForge.Core.Types.X.Sources;
using VisioForge.Core.Types;

// Initialize the SDK once, before any core or pipeline is constructed.
// On MAUI use `await VisioForgeX.InitSDKAsync();` instead.
VisioForgeX.InitSDK();

await using var capture = new VideoCaptureCoreX();

// Configure the screen source. There is no single ScreenSourceSettings type — each
// platform has its own implementation of IScreenCaptureSourceSettings, and they are
// compiled per target framework, so this is #if and not a runtime check: the Windows
// types simply do not exist on a plain net8.0 build.
// Rectangle is VisioForge.Core.Types.Rect(left, top, right, bottom), not
// System.Drawing.Rectangle, so the last two values are edges and not a size.
#if WINDOWS
// ScreenCaptureDX9SourceSettings and ScreenCaptureGDISourceSettings are the
// alternatives on older Windows / remote-desktop sessions.
capture.Video_Source = new ScreenCaptureD3D11SourceSettings
{
    FrameRate = new VideoFrameRate(30),
    Rectangle = new Rect(0, 0, 1920, 1080),
    CaptureCursor = true,
};
#elif MACCATALYST || MACOS
capture.Video_Source = new ScreenCaptureMacOSSourceSettings
{
    FrameRate = new VideoFrameRate(30),
};
#elif ANDROID
// Android and iOS keep their settings in their own namespaces, so these are
// fully qualified rather than adding per-platform using directives above.
//
// Android screen capture cannot start without a MediaProjection, which only comes
// from the user accepting the system consent dialog: launch
// MediaProjectionManager.CreateScreenCaptureIntent() with StartActivityForResult,
// then build the projection in OnActivityResult. Width/Height/Dpi have no defaults
// either — read them from the display metrics. See the Screen Capture demo under
// _DEMOS/Media Blocks SDK/Android/.
capture.Video_Source = new VisioForge.Core.Types.X.Android.Sources.AndroidScreenCaptureSourceSettings
{
    MediaProjection = mediaProjection,   // from OnActivityResult, not optional
    FrameRate = new VideoFrameRate(30),
    Width = screenWidth,
    Height = screenHeight,
    Dpi = screenDpi,
};
#elif IOS
capture.Video_Source = new VisioForge.Core.Types.X.Apple.Sources.IOSScreenSourceSettings();
#else
// Linux — and also any project whose target framework carries no platform suffix,
// since none of the symbols above are defined for a plain net8.0 build. Target
// net8.0-windows or net8.0-macos to reach those arms.
capture.Video_Source = new ScreenCaptureXDisplaySourceSettings
{
    FrameRate = new VideoFrameRate(30),
};
#endif

// Configure MP4 output
var mp4Output = new MP4Output(""screen_recording.mp4"");
capture.Outputs_Add(mp4Output, true);

// Start capture
await capture.StartAsync();

// ... recording screen ...

// Stop
await capture.StopAsync();
```
",
            "media-info" => @"# Read Media File Information

Extract metadata from media files using MediaInfoReaderX.

```csharp
using VisioForge.Core;
using VisioForge.Core.MediaInfoReaderX;

// Initialize the SDK once, before any core or pipeline is constructed.
// On MAUI use `await VisioForgeX.InitSDKAsync();` instead.
VisioForgeX.InitSDK();

// Create the reader
var reader = new MediaInfoReaderX();

// Read file info. OpenAsync returns success/failure; the data is on Info.
if (!await reader.OpenAsync(""video.mp4""))
{
    return;
}

var info = reader.Info;

// Access video stream info
foreach (var videoStream in info.VideoStreams)
{
    Console.WriteLine($""Video: {videoStream.Codec}, {videoStream.Width}x{videoStream.Height}"");
    Console.WriteLine($""  Frame Rate: {videoStream.FrameRate} fps"");
    Console.WriteLine($""  Bitrate: {videoStream.Bitrate} bps"");
    Console.WriteLine($""  Duration: {videoStream.Duration}"");
}

// Access audio stream info
foreach (var audioStream in info.AudioStreams)
{
    Console.WriteLine($""Audio: {audioStream.Codec}"");
    Console.WriteLine($""  Sample Rate: {audioStream.SampleRate} Hz"");
    Console.WriteLine($""  Channels: {audioStream.Channels}"");
    Console.WriteLine($""  Bitrate: {audioStream.Bitrate} bps"");
}

// General info
Console.WriteLine($""Container: {info.Container.Format}"");
Console.WriteLine($""Duration: {info.Container.Duration}"");
```
",
            "pipeline-basic" => @"# Basic Media Block Pipeline

Build a custom media processing pipeline using media blocks.

```csharp
using VisioForge.Core;
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;
using VisioForge.Core.MediaBlocks.VideoProcessing;
using VisioForge.Core.MediaBlocks.VideoEncoders;
using VisioForge.Core.MediaBlocks.Sinks;
using VisioForge.Core.Types.X.Sources;
using VisioForge.Core.Types.X.Sinks;

// Initialize the SDK once, before any core or pipeline is constructed.
// On MAUI use `await VisioForgeX.InitSDKAsync();` instead.
VisioForgeX.InitSDK();

// Create the pipeline
var pipeline = new MediaBlocksPipeline();

// 1. Source block. The filename constructor of UniversalSourceSettings is
// private — build it with the async factory.
var sourceSettings = await UniversalSourceSettings.CreateAsync(""input.mp4"");
var source = new UniversalSourceBlock(sourceSettings);

// 2. Processing block (optional - resize video)
var resizer = new VideoResizeBlock(1280, 720);

// 3. Encoder block
var encoder = new H264EncoderBlock(H264EncoderBlock.GetDefaultSettings());

// 4. Sink block (output)
var sink = new MP4SinkBlock(new MP4SinkSettings(""output.mp4""));

// Connect blocks: Source → Resize → Encode → Sink
pipeline.Connect(source.VideoOutput, resizer.Input);
pipeline.Connect(resizer.Output, encoder.Input);
pipeline.Connect(encoder.Output, sink.CreateNewInput(MediaBlockPadMediaType.Video));

// Pipeline events
pipeline.OnError += (s, e) => Console.WriteLine($""Error: {e.Message}"");

// Start the pipeline
await pipeline.StartAsync();

// Wait for completion or stop manually
await pipeline.StopAsync();
```

## Pipeline Architecture
Media blocks connect via **pads** (input/output connection points):
- Each block has typed pads (Video, Audio, Subtitle, Metadata)
- Connect output pads to input pads of compatible types
- The pipeline manages the data flow between blocks
",
            "virtual-source" => @"# Virtual Video/Audio Source

Generate test patterns and tone audio for testing.

```csharp
using VisioForge.Core;
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;
using VisioForge.Core.MediaBlocks.VideoRendering;
using VisioForge.Core.MediaBlocks.AudioRendering;
using VisioForge.Core.Types.X.Sources;
using VisioForge.Core.Types;

// Initialize the SDK once, before any core or pipeline is constructed.
// On MAUI use `await VisioForgeX.InitSDKAsync();` instead.
VisioForgeX.InitSDK();

var pipeline = new MediaBlocksPipeline();

// Virtual video source (test pattern)
var videoSource = new VirtualVideoSourceBlock(new VirtualVideoSourceSettings
{
    Width = 1920,
    Height = 1080,
    FrameRate = new VideoFrameRate(30),
    Pattern = VirtualVideoSourcePattern.SMPTE, // or Snow, Ball, Gradient, etc.
});

// Virtual audio source (tone)
var audioSource = new VirtualAudioSourceBlock(new VirtualAudioSourceSettings
{
    SampleRate = 44100,
    Channels = 2,
});

// Renderers
var videoRenderer = new VideoRendererBlock(pipeline, videoView);
var audioRenderer = new AudioRendererBlock();

// Connect
pipeline.Connect(videoSource.Output, videoRenderer.Input);
pipeline.Connect(audioSource.Output, audioRenderer.Input);

await pipeline.StartAsync();
```
",
            "hls-streaming" => @"# HLS Streaming

Stream video as HLS (HTTP Live Streaming) segments.

```csharp
using VisioForge.Core;
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;
using VisioForge.Core.MediaBlocks.VideoEncoders;
using VisioForge.Core.MediaBlocks.AudioEncoders;
using VisioForge.Core.MediaBlocks.Sinks;
using VisioForge.Core.Types.X.Sources;
using VisioForge.Core.Types.X.Sinks;

// Initialize the SDK once, before any core or pipeline is constructed.
// On MAUI use `await VisioForgeX.InitSDKAsync();` instead.
VisioForgeX.InitSDK();

var pipeline = new MediaBlocksPipeline();

// Source (webcam or any other source)
var cameras = await DeviceEnumerator.Shared.VideoSourcesAsync();
var mics = await DeviceEnumerator.Shared.AudioSourcesAsync();
if (cameras.Length == 0 || mics.Length == 0)
{
    return; // no capture hardware on this machine
}

var videoSource = new SystemVideoSourceBlock(
    new VideoCaptureDeviceSourceSettings(cameras[0]));
var audioSource = new SystemAudioSourceBlock(
    new AudioCaptureDeviceSourceSettings(mics[0]));

// Encoders. To pin a bitrate, build a concrete encoder instead of the default —
// Bitrate is not on the IH264EncoderSettings interface:
//   new H264EncoderBlock(new OpenH264EncoderSettings { Bitrate = 2500 })
var videoEncoder = new H264EncoderBlock(H264EncoderBlock.GetDefaultSettings());
var audioEncoder = new AACEncoderBlock(AACEncoderBlock.GetDefaultSettings());

// HLS sink. TargetDuration is a TimeSpan and MaxFiles is a uint.
var hlsSink = new HLSSinkBlock(new HLSSinkSettings
{
    Location = ""/var/www/html/stream/segment_%05d.ts"",
    PlaylistLocation = ""/var/www/html/stream/playlist.m3u8"",
    TargetDuration = TimeSpan.FromSeconds(6),
    MaxFiles = 10,
    PlaylistLength = 5,
});

// Connect
pipeline.Connect(videoSource.Output, videoEncoder.Input);
pipeline.Connect(videoEncoder.Output, hlsSink.CreateNewInput(MediaBlockPadMediaType.Video));
pipeline.Connect(audioSource.Output, audioEncoder.Input);
pipeline.Connect(audioEncoder.Output, hlsSink.CreateNewInput(MediaBlockPadMediaType.Audio));

await pipeline.StartAsync();
```
",
            "audio-capture" => @"# Audio-Only Capture

Capture audio from a microphone and save to a file.

```csharp
using VisioForge.Core;
using VisioForge.Core.VideoCaptureX;
using VisioForge.Core.Types.X.Output;
using VisioForge.Core.Types.X.Sources;

// Initialize the SDK once, before any core or pipeline is constructed.
// On MAUI use `await VisioForgeX.InitSDKAsync();` instead.
VisioForgeX.InitSDK();

await using var capture = new VideoCaptureCoreX();

// Configure audio source only
var audioSources = await DeviceEnumerator.Shared.AudioSourcesAsync();
if (audioSources.Length > 0)
{
    capture.Audio_Source = new AudioCaptureDeviceSourceSettings(audioSources[0]);
}

// Disable video
capture.Video_Source = null;

// WAV output (uncompressed)
var wavOutput = new WAVOutput(""recording.wav"");
capture.Outputs_Add(wavOutput, true);

// Or MP3 output
// var mp3Output = new MP3Output(""recording.mp3"");
// capture.Outputs_Add(mp3Output, true);

await capture.StartAsync();
// ... recording ...
await capture.StopAsync();
```
",
            _ => $@"Unknown scenario: '{scenario}'.

Available examples:
- webcam-capture — Capture from webcam to MP4
- rtsp-playback — Play RTSP stream
- rtsp-to-mp4 — Record RTSP to MP4 file
- webcam-to-rtmp — Stream webcam to RTMP/YouTube
- file-playback — Play a media file
- video-editing — Basic video editing
- screen-capture — Capture screen to file
- media-info — Read media file information
- pipeline-basic — Build a basic media block pipeline
- virtual-source — Generate test video/audio
- hls-streaming — Stream to HLS
- audio-capture — Capture audio only

Use `get_code_example` with one of these scenario names."
        };
    }

    private static string GetTemplate(string useCase)
    {
        return useCase.ToLower().Replace(" ", "-") switch
        {
            "rtsp-to-mp4" => @"# Pipeline Template: RTSP Camera → MP4 File

## Required Blocks
1. `RTSPSourceBlock` — Connects to RTSP camera
2. `H264EncoderBlock` — Encodes video (or pass-through)
3. `AACEncoderBlock` — Encodes audio
4. `MP4SinkBlock` — Writes MP4 file

## Pipeline Graph
```
RTSPSourceBlock ──VideoOutput──→ H264EncoderBlock ──Output──→ MP4SinkBlock (video input)
                ──AudioOutput──→ AACEncoderBlock  ──Output──→ MP4SinkBlock (audio input)
```

## Settings Classes Needed
- `RTSPSourceSettings` — URI, credentials, transport (TCP/UDP). Build with
  `RTSPSourceSettings.CreateAsync(...)`; the Uri constructor is private.
- `H264EncoderBlock.GetDefaultSettings()` — returns the best `IH264EncoderSettings`
  for the platform (NVENC / QSV / MF / VideoToolbox / OpenH264). There is no
  `X264EncoderSettings` type.
- `AACEncoderBlock.GetDefaultSettings()` — returns an `IAACEncoderSettings`
  (MF / VO / AVENC / Apple, per platform). There is no plain `AACEncoderSettings`.
- `MP4SinkSettings` — filename, fragment mode
",
            "webcam-preview" => @"# Pipeline Template: Webcam → Screen Preview

## Required Blocks
1. `SystemVideoSourceBlock` — Captures from webcam
2. `VideoRendererBlock` — Displays on screen

## Pipeline Graph
```
SystemVideoSourceBlock ──Output──→ VideoRendererBlock
```

## For webcam + microphone:
```
SystemVideoSourceBlock ──Output──→ VideoRendererBlock
SystemAudioSourceBlock ──Output──→ AudioRendererBlock
```

## Settings Classes
- `VideoCaptureDeviceSourceSettings` — built from a `VideoCaptureDeviceInfo`
  returned by `DeviceEnumerator.Shared.VideoSourcesAsync()`; carries format and
  frame rate. `SystemVideoSourceBlock` takes this type.
- `AudioCaptureDeviceSourceSettings` — the audio equivalent, from
  `DeviceEnumerator.Shared.AudioSourcesAsync()`.
",
            "webcam-to-rtmp" => @"# Pipeline Template: Webcam → RTMP Live Stream

## Required Blocks
1. `SystemVideoSourceBlock` — Webcam input
2. `SystemAudioSourceBlock` — Microphone input
3. `H264EncoderBlock` — Video encoding (H.264 required for RTMP)
4. `AACEncoderBlock` — Audio encoding (AAC required for RTMP)
5. `RTMPSinkBlock` — RTMP output

## Pipeline Graph
```
SystemVideoSourceBlock ──Output──→ H264EncoderBlock ──Output──→ RTMPSinkBlock (video)
SystemAudioSourceBlock ──Output──→ AACEncoderBlock  ──Output──→ RTMPSinkBlock (audio)
```

## RTMP URLs
- YouTube: `rtmp://a.rtmp.youtube.com/live2/{stream-key}`
- Facebook: `rtmps://live-api-s.facebook.com:443/rtmp/{stream-key}`
- Twitch: `rtmp://live.twitch.tv/app/{stream-key}`
- Custom: `rtmp://your-server:1935/live/stream`
",
            "file-transcode" => @"# Pipeline Template: File Transcode (Convert Format)

## Required Blocks
1. `UniversalSourceBlock` — Reads and decodes input file
2. `H264EncoderBlock` / `HEVCEncoderBlock` / `VPXEncoderBlock` — Re-encode video
3. `AACEncoderBlock` / `OPUSEncoderBlock` — Re-encode audio
4. `MP4SinkBlock` / `MKVSinkBlock` / `WebMSinkBlock` — Output container

## Example: AVI → MP4
```
UniversalSourceBlock ──VideoOutput──→ H264EncoderBlock ──Output──→ MP4SinkBlock (video)
                     ──AudioOutput──→ AACEncoderBlock  ──Output──→ MP4SinkBlock (audio)
```

## Example: MP4 → WebM
```
UniversalSourceBlock ──VideoOutput──→ VPXEncoderBlock   ──Output──→ WebMSinkBlock (video)
                     ──AudioOutput──→ VorbisEncoderBlock ──Output──→ WebMSinkBlock (audio)
```
",
            "screen-to-hls" => @"# Pipeline Template: Screen Capture → HLS Stream

## Required Blocks
1. `ScreenSourceBlock` — Captures screen
2. `H264EncoderBlock` — Encodes to H.264
3. `AACEncoderBlock` — Encodes audio (optional)
4. `HLSSinkBlock` — Writes HLS segments

## Pipeline Graph
```
ScreenSourceBlock ──Output──→ H264EncoderBlock ──Output──→ HLSSinkBlock (video)
```

## HLS Output Files
- Segments: `/path/segment00001.ts`, `segment00002.ts`, ...
- Playlist: `/path/playlist.m3u8`
- Serve via any HTTP server (nginx, Apache)
",
            "multi-source-mix" => @"# Pipeline Template: Multi-Source Video Mixing

## Required Blocks
1. Multiple source blocks (webcam, files, virtual)
2. `VideoMixerSourceBlock` — Composites video sources
3. Encoder + Sink for output

## Pipeline Graph
```
SystemVideoSourceBlock ──Output──┐
VirtualVideoSourceBlock ──Output──┤──→ VideoMixerSourceBlock ──Output──→ Encoder → Sink
RTSPSourceBlock ──VideoOutput──┘
```

## Mixer Features
- Position each source (x, y coordinates)
- Set size for each source
- Set Z-order (layering)
- Alpha blending / transparency
",
            "audio-only-capture" => @"# Pipeline Template: Audio-Only Capture

## For WAV (uncompressed):
```
SystemAudioSourceBlock ──Output──→ WAVSinkBlock
```

## For MP3:
```
SystemAudioSourceBlock ──Output──→ MP3EncoderBlock ──Output──→ FileSinkBlock
```

## For AAC in MP4:
```
SystemAudioSourceBlock ──Output──→ AACEncoderBlock ──Output──→ MP4SinkBlock (audio)
```

## For FLAC (lossless):
```
SystemAudioSourceBlock ──Output──→ FLACEncoderBlock ──Output──→ FileSinkBlock
```
",
            "ndi-output" => @"# Pipeline Template: NDI Output

## Required Blocks
1. Source block (any)
2. `NDISinkBlock` — Sends video/audio via NDI protocol

## Pipeline Graph
```
SystemVideoSourceBlock ──Output──→ NDISinkBlock (video)
SystemAudioSourceBlock ──Output──→ NDISinkBlock (audio)
```

## NDI is a zero-configuration protocol for LAN video transmission.
- Low latency
- No IP configuration needed (auto-discovery)
- Used in broadcast/live production
",
            _ => $@"Unknown template: '{useCase}'.

Available pipeline templates:
- rtsp-to-mp4 — Record network camera to file
- webcam-preview — Show webcam on screen
- webcam-to-rtmp — Stream webcam live
- file-transcode — Convert video file format
- screen-to-hls — Screen capture to HLS stream
- multi-source-mix — Mix multiple video sources
- audio-only-capture — Record audio to file
- ndi-output — Stream via NDI protocol

Use `get_pipeline_template` with one of these names."
        };
    }
}
