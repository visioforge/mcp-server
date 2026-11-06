using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using VisioForge.MCP.Server.Index;

namespace VisioForge.MCP.Server.Tools;

[McpServerToolType]
public static class DeploymentTools
{
    [McpServerTool(ReadOnly = true, Idempotent = true, OpenWorld = false), Description(
        "List available deployment guides for VisioForge SDK. " +
        "Filter by platform, project type, SDK type, or scenario. " +
        "Returns a list of guides with titles, summaries, and tags.")]
    public static string ListDeploymentGuides(
        ApiIndexService index,
        [Description("Filter by platform (optional). Values: Windows, Linux, macOS, Android, iOS, All")]
        string? platform = null,
        [Description("Filter by project type (optional). Values: WPF, WinForms, MAUI, Avalonia, Console, Uno, Blazor")]
        string? projectType = null,
        [Description("Filter by SDK (optional). Values: MediaBlocks, VideoCapture, MediaPlayer, VideoEdit")]
        string? sdkType = null,
        [Description("Filter by tag (optional). Values: docker, cloud, mobile, desktop, rtsp, streaming")]
        string? tag = null)
    {
        var guides = index.Data.DeploymentGuides.AsEnumerable();

        if (!string.IsNullOrEmpty(platform) && !platform.Equals("All", StringComparison.OrdinalIgnoreCase))
            guides = guides.Where(g => g.Platforms.Contains(platform, StringComparer.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(projectType))
            guides = guides.Where(g => g.ProjectTypes.Contains(projectType, StringComparer.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(sdkType))
            guides = guides.Where(g => g.SdkTypes.Contains(sdkType, StringComparer.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(tag))
            guides = guides.Where(g => g.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));

        var guidesList = guides.ToList();

        if (guidesList.Count == 0)
            return "No deployment guides found matching your filters. Try list_deployment_guides without filters to see all available guides.";

        var sb = new StringBuilder();
        sb.AppendLine("# Available Deployment Guides\n");

        var groupedByCategory = guidesList.GroupBy(g => g.Category);
        foreach (var group in groupedByCategory.OrderBy(g => g.Key))
        {
            sb.AppendLine($"## {char.ToUpper(group.Key[0]) + group.Key[1..]} Guides\n");
            foreach (var guide in group.OrderBy(g => g.Title))
            {
                sb.AppendLine($"### {guide.Title}");
                sb.AppendLine($"**ID:** `{guide.Id}`");
                sb.AppendLine($"**Platforms:** {string.Join(", ", guide.Platforms)}");
                if (guide.ProjectTypes.Count > 0)
                    sb.AppendLine($"**Project Types:** {string.Join(", ", guide.ProjectTypes)}");
                sb.AppendLine($"**Difficulty:** {guide.Difficulty}");
                sb.AppendLine($"**Summary:** {guide.Summary}");
                sb.AppendLine();
            }
        }

        sb.AppendLine($"\n*Total: {guidesList.Count} guide(s)*");
        sb.AppendLine("\nTo view a guide, use: `get_deployment_guide` with the guide ID.");

        return sb.ToString();
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, OpenWorld = false), Description(
        "Get the complete deployment guide for a specific scenario. " +
        "Returns detailed instructions, code snippets, NuGet packages, and platform-specific notes.")]
    public static string GetDeploymentGuide(
        ApiIndexService index,
        [Description("Guide ID from list_deployment_guides (e.g., 'windows-desktop', 'maui-mobile')")]
        string guideId)
    {
        var guide = index.Data.DeploymentGuides.FirstOrDefault(
            g => g.Id.Equals(guideId, StringComparison.OrdinalIgnoreCase));

        if (guide == null)
            return $"Deployment guide not found: '{guideId}'.\n\nUse `list_deployment_guides` to see available guides.";

        return guide.Content;
    }

    // Explicit Name attribute: SDK auto-naming splits "NuGet" into "nu_get",
    // producing the unfortunate "get_nu_get_packages_snippet". The internal
    // string reference in GetPlatformSpecificConfig already says
    // "get_nuget_packages_snippet" — was aspirational, now matches reality.
    [McpServerTool(Name = "get_nuget_packages_snippet", ReadOnly = true, Idempotent = true, OpenWorld = false), Description(
        "Get .csproj snippet with required NuGet packages for a specific deployment scenario. " +
        "Returns XML snippet ready to copy-paste into your project file.")]
    public static string GetNuGetPackagesSnippet(
        ApiIndexService index,
        [Description("Target platform, required. One of: Windows, Linux, macOS, Android, iOS, macCatalyst.")]
        string platform,
        [Description(
            "Project type, required. One of: WPF, WinForms, WinUI, Console, MAUI, Avalonia, Uno, Blazor. "
            + "MAUI, Avalonia and Uno are treated as multi-target, so the native redists are emitted "
            + "under a target-framework condition; the rest get them unconditionally.")]
        string projectType,
        [Description(
            "SDK type, required. VideoCapture for camera/screen/IP-camera recording, " +
            "MediaPlayer for playback, VideoEdit for timeline editing — those are the three " +
            "products. MediaBlocks is the low-level pipeline layer, for custom " +
            "source-to-encoder graphs only; do not pick it for an ordinary capture, " +
            "playback or editing app.")]
        string sdkType,
        [Description("Include optional packages? (e.g., OpenCV, AWS, specialized camera support)")]
        bool includeOptional = false)
    {
        // sdkType lands verbatim in "VisioForge.DotNet.{sdkType}". An agent reading the
        // prose in ServerInstructions can easily pass "Video Capture" or "VideoCaptureX",
        // and the snippet would then say VisioForge.DotNet.Video Capture — NU1101 on the
        // first restore, which is the failure this tool was fixed to stop producing.
        var known = new[] { "VideoCapture", "MediaPlayer", "VideoEdit", "MediaBlocks" };
        var matched = Array.Find(known, k => string.Equals(k, sdkType, StringComparison.OrdinalIgnoreCase));
        if (matched is null)
        {
            return $"Unknown sdkType \"{sdkType}\". Use exactly one of: {string.Join(", ", known)}. "
                 + "VideoCapture records cameras, screens and IP cameras; MediaPlayer plays "
                 + "files and streams; VideoEdit is timeline editing; MediaBlocks is the "
                 + "low-level pipeline layer, for custom source-to-encoder graphs only.";
        }

        sdkType = matched;

        // Same guard for platform. Without it "OSX", "win" or "Android 14" matched no
        // branch below, and the tool returned a syntactically valid csproj carrying the
        // managed wrapper and not one native redist — which fails at runtime with
        // DllNotFoundException rather than at restore, and gives the agent no signal at
        // all that it asked for something the tool did not understand.
        var knownPlatforms = new[] { "Windows", "Linux", "macOS", "Android", "iOS", "macCatalyst" };
        var matchedPlatform = Array.Find(knownPlatforms, p => string.Equals(p, platform, StringComparison.OrdinalIgnoreCase));
        if (matchedPlatform is null)
        {
            return $"Unknown platform \"{platform}\". Use exactly one of: {string.Join(", ", knownPlatforms)}.";
        }

        platform = matchedPlatform;

        // projectType reaches an XML comment below, where "--" is illegal — the same
        // way the optional-packages block used to produce an unparseable csproj. It also
        // selects the multi-target conditions further down, so a typo silently changes
        // the snippet rather than being reported.
        var knownProjectTypes = new[] { "WPF", "WinForms", "WinUI", "Console", "MAUI", "Avalonia", "Uno", "Blazor" };
        var matchedProjectType = Array.Find(knownProjectTypes, p => string.Equals(p, projectType, StringComparison.OrdinalIgnoreCase));
        if (matchedProjectType is null)
        {
            return $"Unknown projectType \"{projectType}\". Use exactly one of: {string.Join(", ", knownProjectTypes)}.";
        }

        projectType = matchedProjectType;

        var sb = new StringBuilder();
        sb.AppendLine($"<!-- VisioForge SDK NuGet Packages -->");
        sb.AppendLine($"<!-- Platform: {platform}, Project Type: {projectType}, SDK: {sdkType} -->\n");

        // Core SDK package
        sb.AppendLine($"<ItemGroup>");
        sb.AppendLine($"  <PackageReference Include=\"VisioForge.DotNet.{sdkType}\" Version=\"{NugetPins.ToNuGetVersion(index.SdkVersion)}\" />");

        // Platform-specific packages
        var platformLower = platform.ToLower();

        // Only a project that really multi-targets needs the redists conditioned; a
        // single-target one is already scoped by the platform argument. This matters
        // because the conditions are not interchangeable:
        //   - "-android", "-ios", "-maccatalyst", "-macos" and "-windows" are real .NET
        //     platform TFM suffixes.
        //   - "-linux" is NOT. `$(TargetFramework.Contains('-linux'))` is never true, so
        //     the Linux redist was never restored and the app died at GStreamer init —
        //     the repo's own linux-ubuntu.md guide uses an unconditional ItemGroup.
        //   - "-windows" is also absent from a plain net8.0 console project, so the
        //     Windows redists went missing there for the same reason.
        var multiTarget = projectType.Equals("MAUI", StringComparison.OrdinalIgnoreCase)
            || projectType.Equals("Avalonia", StringComparison.OrdinalIgnoreCase)
            || projectType.Equals("Uno", StringComparison.OrdinalIgnoreCase);

        if (platformLower == "windows")
        {
            var condition = multiTarget ? " Condition=\"$(TargetFramework.Contains('-windows'))\"" : string.Empty;
            sb.AppendLine($"</ItemGroup>\n");
            sb.AppendLine($"<ItemGroup{condition}>");
            sb.AppendLine($"  <PackageReference Include=\"VisioForge.CrossPlatform.Core.Windows.x64\" Version=\"{NugetPins.Windows}\" />");
            sb.AppendLine($"  <PackageReference Include=\"VisioForge.CrossPlatform.Libav.Windows.x64.UPX\" Version=\"{NugetPins.Windows}\" />");
        }
        else if (platformLower == "linux")
        {
            // IsOsPlatform, not a TFM check — there is no Linux TFM suffix to test.
            var condition = multiTarget ? " Condition=\"$([MSBuild]::IsOsPlatform('Linux'))\"" : string.Empty;
            sb.AppendLine($"</ItemGroup>\n");
            sb.AppendLine($"<ItemGroup{condition}>");
            sb.AppendLine($"  <PackageReference Include=\"VisioForge.CrossPlatform.Core.Linux.x64\" Version=\"{NugetPins.Linux}\" />");
        }
        else if (platformLower == "android")
        {
            sb.AppendLine($"</ItemGroup>\n");
            sb.AppendLine($"<ItemGroup Condition=\"$(TargetFramework.Contains('-android'))\">");
            sb.AppendLine($"  <PackageReference Include=\"VisioForge.CrossPlatform.Core.Android\" Version=\"{NugetPins.Android}\" />");
            sb.AppendLine($"</ItemGroup>\n");
            sb.AppendLine($"<ItemGroup>");
            sb.AppendLine($"  <!-- Android requires Java bindings ProjectReference -->");
            sb.AppendLine($"  <ProjectReference Include=\"..\\AndroidDependency\\{NugetPins.AndroidBindingsProject}\" />");
        }
        else if (platformLower == "ios")
        {
            sb.AppendLine($"</ItemGroup>\n");
            sb.AppendLine($"<ItemGroup Condition=\"$(TargetFramework.Contains('-ios'))\">");
            sb.AppendLine($"  <PackageReference Include=\"VisioForge.CrossPlatform.Core.iOS\" Version=\"{NugetPins.IOS}\" />");
        }
        else if (platformLower == "maccatalyst")
        {
            sb.AppendLine($"</ItemGroup>\n");
            sb.AppendLine($"<ItemGroup Condition=\"$(TargetFramework.Contains('-maccatalyst'))\">");
            sb.AppendLine($"  <PackageReference Include=\"VisioForge.CrossPlatform.Core.macCatalyst\" Version=\"{NugetPins.Apple}\" />");
        }
        else if (platformLower == "macos")
        {
            // Same as Windows and Linux above: a plain net8.0 console or Blazor project
            // on macOS has no "-macos" in its TFM, so the condition would never be true
            // and the redist would never restore.
            var condition = multiTarget ? " Condition=\"$(TargetFramework.Contains('-macos'))\"" : string.Empty;
            sb.AppendLine($"</ItemGroup>\n");
            sb.AppendLine($"<ItemGroup{condition}>");
            sb.AppendLine($"  <PackageReference Include=\"VisioForge.CrossPlatform.Core.macOS\" Version=\"{NugetPins.Apple}\" />");
        }

        sb.AppendLine($"</ItemGroup>");

        // Optional packages
        if (includeOptional)
        {
            // One <!-- --> per line rather than one wrapper around the block: XML
            // comments do not nest and `--` is illegal inside one, so a wrapper whose
            // body carried "<!-- Computer Vision -->" ended at that first inner `-->`
            // and left the rest malformed. Pasting the result gave an XML parse error
            // on the csproj — the same first-restore failure this tool exists to avoid.
            if (platformLower == "windows")
            {
                sb.AppendLine($"\n<!-- Optional packages. Uncomment the lines you need. -->");
                sb.AppendLine($"<ItemGroup Condition=\"$(TargetFramework.Contains('-windows'))\">");
                sb.AppendLine($"  <!-- Computer vision processors -->");
                sb.AppendLine($"  <!-- <PackageReference Include=\"VisioForge.CrossPlatform.OpenCV.Windows.x64\" Version=\"{NugetPins.OpenCV}\" /> -->");
                sb.AppendLine($"  <!-- AWS S3 integration -->");
                sb.AppendLine($"  <!-- <PackageReference Include=\"VisioForge.CrossPlatform.AWS.Windows.x64\" Version=\"{NugetPins.Windows}\" /> -->");
                sb.AppendLine($"  <!-- Industrial cameras -->");
                sb.AppendLine($"  <!-- <PackageReference Include=\"VisioForge.CrossPlatform.Basler.Windows.x64\" Version=\"{NugetPins.Windows}\" /> -->");
                sb.AppendLine($"  <!-- <PackageReference Include=\"VisioForge.CrossPlatform.Spinnaker.Windows.x64\" Version=\"{NugetPins.Windows}\" /> -->");
                sb.AppendLine($"</ItemGroup>");
            }
            else
            {
                sb.AppendLine($"\n<!-- No optional packages are published for {platform}. -->");
            }
        }

        return sb.ToString();
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, OpenWorld = false), Description(
        "Get platform-specific file copying/build configuration code. " +
        "Returns MSBuild targets or post-build scripts for special deployment requirements.")]
    public static string GetPlatformSpecificConfig(
        ApiIndexService index,
        [Description("Platform: Windows, macOS, maccatalyst, Android, iOS, Linux")]
        string platform)
    {
        var platformLower = platform.ToLower();

        if (platformLower == "maccatalyst")
        {
            return @"<!-- Mac Catalyst: Copy native dylib files to MonoBundle directory -->
<Target Name=""CopyNativeLibrariesToMonoBundle"" AfterTargets=""Build"" Condition=""$(TargetFramework.Contains('-maccatalyst'))"">
  <Message Text=""Copying native libraries for Mac Catalyst..."" Importance=""High"" />
  <PropertyGroup>
    <AppBundleDir>$(OutputPath)$(AssemblyName).app</AppBundleDir>
    <MonoBundleDir>$(AppBundleDir)/Contents/MonoBundle</MonoBundleDir>
  </PropertyGroup>
  <MakeDir Directories=""$(MonoBundleDir)"" Condition=""!Exists('$(MonoBundleDir)')"" />
  <Copy SourceFiles=""@(None->'%(FullPath)')"" DestinationFolder=""$(MonoBundleDir)""
        Condition=""'%(Extension)' == '.dylib' Or '%(Extension)' == '.so'"">
    <Output TaskParameter=""CopiedFiles"" ItemName=""CopiedNativeFiles"" />
  </Copy>
  <Message Text=""Native libraries copied successfully"" Importance=""High"" />
</Target>";
        }
        else if (platformLower == "android")
        {
            return @"<!-- Android: AndroidManifest.xml permissions -->
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"">
  <uses-permission android:name=""android.permission.CAMERA"" />
  <uses-permission android:name=""android.permission.RECORD_AUDIO"" />
  <uses-permission android:name=""android.permission.WRITE_EXTERNAL_STORAGE"" />
  <uses-permission android:name=""android.permission.READ_EXTERNAL_STORAGE"" />
  <uses-permission android:name=""android.permission.INTERNET"" />
  <uses-feature android:name=""android.hardware.camera"" android:required=""false"" />
  <uses-feature android:name=""android.hardware.camera.autofocus"" android:required=""false"" />
</manifest>

<!-- Runtime permission request code (C#) -->
<!-- Add to your startup or before using camera/microphone -->
using Microsoft.Maui.ApplicationModel;

// Request camera and microphone permissions at runtime (Android 6.0+)
if (OperatingSystem.IsAndroidVersionAtLeast(23))
{
    var cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
    var micStatus = await Permissions.RequestAsync<Permissions.Microphone>();
    var storageStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();

    if (cameraStatus != PermissionStatus.Granted || micStatus != PermissionStatus.Granted)
    {
        // Handle permission denial
        Console.WriteLine(""Camera or microphone permission denied"");
    }
}";
        }
        else if (platformLower == "ios")
        {
            return @"<!-- iOS: Info.plist permissions -->
<!-- Add these keys to Info.plist -->
<key>NSCameraUsageDescription</key>
<string>This app requires camera access for video recording</string>

<key>NSMicrophoneUsageDescription</key>
<string>This app requires microphone access for audio recording</string>

<key>NSPhotoLibraryUsageDescription</key>
<string>This app requires photo library access to save videos</string>

<key>NSPhotoLibraryAddUsageDescription</key>
<string>This app needs to save captured videos to your photo library</string>";
        }
        else if (platformLower == "macos")
        {
            return @"<!-- macOS: Info.plist permissions and entitlements -->
<!-- Add these keys to Info.plist -->
<key>NSCameraUsageDescription</key>
<string>This app requires camera access for video recording</string>

<key>NSMicrophoneUsageDescription</key>
<string>This app requires microphone access for audio recording</string>

<!-- For sandboxed apps, add to entitlements -->
<key>com.apple.security.device.camera</key>
<true/>
<key>com.apple.security.device.audio-input</key>
<true/>
<key>com.apple.security.files.user-selected.read-write</key>
<true/>";
        }

        return $"No special configuration required for {platform}.\n\nMost platforms work out-of-the-box with just NuGet packages. Use `get_nuget_packages_snippet` to get the required packages.";
    }
}
