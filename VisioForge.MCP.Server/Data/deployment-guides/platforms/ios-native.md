---
id: ios-native
title: iOS Native Deployment (Xamarin/Native)
category: platform
platforms:
  - iOS
projectTypes:
  - Native
sdkTypes:
  - MediaBlocks
tags:
  - mobile
  - ios
  - native
  - apple
difficulty: advanced
summary: Deploy VisioForge SDK to iOS using native iOS project or Xamarin.iOS without MAUI.
---

# iOS Native Deployment

## Overview

VisioForge SDK supports iOS native development without MAUI, using Xamarin.iOS or pure iOS projects.

## Prerequisites

- **macOS** with Xcode 14 or later
- **Visual Studio 2022 for Mac** or **VS Code**
- **.NET 10.0 SDK** or later
- **Apple Developer account** (for device deployment)
- **CocoaPods** (if using native dependencies)

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
  <PackageReference Include="VisioForge.CrossPlatform.Core.iOS" Version="2026.11.5" />
</ItemGroup>
```

## Project Configuration (Xamarin.iOS)

```xml
<PropertyGroup>
  <TargetFramework>net10.0-ios</TargetFramework>
  <SupportedOSPlatformVersion>13.0</SupportedOSPlatformVersion>
  <OutputType>Exe</OutputType>
  <CodesignKey>iPhone Developer</CodesignKey>
</PropertyGroup>
```

## Info.plist Permissions

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDisplayName</key>
    <string>MyVideoApp</string>

    <key>CFBundleIdentifier</key>
    <string>com.company.myvideoapp</string>

    <key>NSCameraUsageDescription</key>
    <string>This app requires camera access for video recording</string>

    <key>NSMicrophoneUsageDescription</key>
    <string>This app requires microphone access for audio recording</string>

    <key>NSPhotoLibraryUsageDescription</key>
    <string>This app requires photo library access to save videos</string>

    <key>NSPhotoLibraryAddUsageDescription</key>
    <string>This app needs to save captured videos to your photo library</string>

    <key>MinimumOSVersion</key>
    <string>13.0</string>
</dict>
</plist>
```

## Code Example (UIKit)

```csharp
using UIKit;
using Foundation;
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;
using VisioForge.Core.MediaBlocks.Sinks;

[Register("ViewController")]
public class ViewController : UIViewController
{
    private MediaBlocksPipeline? _pipeline;
    private UIView? _videoView;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        // Create video view
        _videoView = new UIView(View.Bounds);
        View.AddSubview(_videoView);

        // Request camera permission
        AVFoundation.AVCaptureDevice.RequestAccessForMediaType(
            AVFoundation.AVAuthorizationMediaType.Video,
            (granted) => {
                if (granted)
                {
                    InvokeOnMainThread(InitializeCamera);
                }
            });
    }

    private async void InitializeCamera()
    {
        _pipeline = new MediaBlocksPipeline();

        // Use front camera (index 1) or back camera (index 0)
        var camera = new VideoCaptureDeviceSourceBlock(1);
        var renderer = new VideoRendererBlock(_pipeline, _videoView);

        _pipeline.Connect(camera.VideoOutput, renderer.Input);
        await _pipeline.StartAsync();
    }

    public override void ViewWillDisappear(bool animated)
    {
        base.ViewWillDisappear(animated);
        _pipeline?.StopAsync().Wait();
        _pipeline?.Dispose();
    }
}
```

## Deployment

### Build for iOS Simulator

```bash
dotnet build -c Debug -f net10.0-ios
```

### Build for Physical Device

```bash
dotnet publish -c Release -f net10.0-ios \
  /p:ArchiveOnBuild=true \
  /p:CodesignKey="iPhone Developer" \
  /p:CodesignProvision="YourProvisioningProfile"
```

### Deploy via Xcode

1. Open `.csproj` in Visual Studio for Mac
2. Select physical device
3. Click Run
4. App will be code signed and deployed

## Code Signing

### Development Certificate

1. Open Xcode → Preferences → Accounts
2. Add Apple ID
3. Manage Certificates → + → iOS Development
4. Download provisioning profile

### Distribution Certificate

For App Store or TestFlight:

1. Create Distribution Certificate in Apple Developer Portal
2. Create App Store provisioning profile
3. Build with Release configuration

```bash
dotnet publish -c Release -f net10.0-ios \
  /p:CodesignKey="iPhone Distribution" \
  /p:CodesignProvision="YourAppStoreProfile"
```

## Troubleshooting

- **Code signing failed**: Verify certificate and provisioning profile installed
- **Camera not working**: Check Info.plist has NSCameraUsageDescription
- **App crashes on launch**: Verify minimum iOS version (13.0+)
- **Linker errors**: Add `--linkskip=VisioForge` to Additional mtouch arguments

## TestFlight Distribution

1. Archive build with Release configuration
2. Upload IPA to App Store Connect
3. Submit to TestFlight
4. Add internal/external testers
5. Distribute

## Next Steps

- [MAUI iOS deployment](ios-maui.md)
- [Mac Catalyst](maccatalyst-maui.md)
- [SwiftUI integration](https://docs.microsoft.com/xamarin/ios)
