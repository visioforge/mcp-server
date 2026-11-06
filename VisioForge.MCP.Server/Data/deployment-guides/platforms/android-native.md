---
id: android-native
title: Android Native Deployment (Xamarin/Native)
category: platform
platforms:
  - Android
projectTypes:
  - Console
  - Native
sdkTypes:
  - MediaBlocks
tags:
  - mobile
  - android
  - native
difficulty: advanced
summary: Deploy VisioForge SDK to Android using native Android project or Xamarin.Android.
---

# Android Native Deployment

## Overview

VisioForge SDK supports Android native development without MAUI, using Xamarin.Android or pure Android projects.

## Prerequisites

- **Visual Studio 2022** or **Android Studio**
- **Android SDK API 34**
- **JDK 11** or later
- **Android NDK** (for native libraries)

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
  <PackageReference Include="VisioForge.CrossPlatform.Core.Android" Version="2026.11.5" />
</ItemGroup>

<ItemGroup>
  <!-- Android Java bindings (required) -->
  <ProjectReference Include="..\AndroidDependency\VisioForge.Core.Android.X10.csproj" />
</ItemGroup>
```

## Project Configuration (Xamarin.Android)

```xml
<PropertyGroup>
  <TargetFramework>net10.0-android</TargetFramework>
  <AndroidMinimumVersion>21</AndroidMinimumVersion>
  <AndroidTargetVersion>34</AndroidTargetVersion>
  <AndroidManifestPlaceholders>
    <item key="applicationId">com.company.appname</item>
  </AndroidManifestPlaceholders>
</PropertyGroup>
```

## AndroidManifest.xml

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android"
          package="com.company.appname">

    <uses-permission android:name="android.permission.CAMERA" />
    <uses-permission android:name="android.permission.RECORD_AUDIO" />
    <uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
    <uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
    <uses-permission android:name="android.permission.INTERNET" />

    <uses-feature android:name="android.hardware.camera" android:required="false" />
    <uses-feature android:name="android.hardware.camera.autofocus" android:required="false" />

    <application android:label="MyVideoApp">
        <activity android:name=".MainActivity" />
    </application>
</manifest>
```

## Runtime Permissions (API 23+)

```csharp
using Android.Content.PM;
using AndroidX.Core.App;
using AndroidX.Core.Content;

private void RequestPermissions()
{
    if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
    {
        string[] permissions = {
            Manifest.Permission.Camera,
            Manifest.Permission.RecordAudio,
            Manifest.Permission.WriteExternalStorage
        };

        ActivityCompat.RequestPermissions(this, permissions, 1);
    }
}

public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
{
    if (requestCode == 1)
    {
        if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
        {
            // Permission granted, start camera
            InitializeCamera();
        }
    }
}
```

## Code Example

```csharp
using Android.App;
using Android.Views;
using VisioForge.Core.MediaBlocks;
using VisioForge.Core.MediaBlocks.Sources;

[Activity(Label = "Video Capture")]
public class MainActivity : Activity
{
    private MediaBlocksPipeline? _pipeline;
    private SurfaceView? _surfaceView;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.Main);

        _surfaceView = FindViewById<SurfaceView>(Resource.Id.SurfaceView);
        RequestPermissions();
    }

    private async void InitializeCamera()
    {
        _pipeline = new MediaBlocksPipeline();
        var camera = new VideoCaptureDeviceSourceBlock(0);
        var renderer = new VideoRendererBlock(_pipeline, _surfaceView);

        _pipeline.Connect(camera.VideoOutput, renderer.Input);
        await _pipeline.StartAsync();
    }

    protected override void OnDestroy()
    {
        _pipeline?.Dispose();
        base.OnDestroy();
    }
}
```

## Deployment

### Build APK

```bash
dotnet build -c Release -f net10.0-android
```

### Build AAB (Google Play)

```bash
dotnet publish -c Release -f net10.0-android -p:AndroidPackageFormat=aab
```

### Install on Device

```bash
adb install -r bin/Release/net10.0-android/MyApp-Signed.apk
```

## Troubleshooting

- **JNI bindings error**: Ensure AndroidDependency project is referenced
- **Camera initialization failed**: Check permissions granted
- **Native library not found**: Verify VisioForge.CrossPlatform.Core.Android installed
- **App crashes on start**: Check min API level (21+)

## Next Steps

- [MAUI Android deployment](android-maui.md)
- [Android performance optimization](../scenarios/)
