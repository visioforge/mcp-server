using VisioForge.MCP.Server.Index;
using VisioForge.MCP.Server.Tools;
using VisioForge.MCP.Shared;
using ApiIndexData = VisioForge.MCP.Shared.ApiIndex;
using Xunit;

namespace VisioForge.MCP.Tests;

/// <summary>
/// Integration tests for all MCP tools — verifies output format, error handling, and edge cases.
/// </summary>
public class ToolTests
{
    private static ApiIndexService CreateTestIndex()
    {
        var service = new ApiIndexService();

        var data = new ApiIndexData
        {
            Version = "1.0",
            SdkVersion = "2026.1.0.0",
            GeneratedAt = "2025-01-01",
            Namespaces = ["VisioForge.Core.MediaBlocks.Sources", "VisioForge.Core.MediaBlocks.Sinks", "VisioForge.Core.VideoCaptureX"],
            Classes =
            [
                new ClassInfo
                {
                    FullName = "VisioForge.Core.MediaBlocks.Sources.RTSPSourceBlock",
                    Name = "RTSPSourceBlock",
                    Namespace = "VisioForge.Core.MediaBlocks.Sources",
                    IsMediaBlock = true,
                    MediaBlockCategory = "Sources",
                    Summary = "RTSP network camera source block for live streaming",
                    Pads =
                    [
                        new PadInfo { Name = "VideoOutput", Direction = "Out", MediaType = "Video" },
                        new PadInfo { Name = "AudioOutput", Direction = "Out", MediaType = "Audio" },
                    ],
                    Properties =
                    [
                        new PropertyInfo { Name = "Settings", Type = "RTSPSourceSettings", Summary = "Gets or sets the RTSP source settings" },
                    ],
                    Methods =
                    [
                        new MethodInfo { Name = "Build", ReturnType = "bool", Summary = "Builds the media block" },
                        new MethodInfo { Name = "IsAvailable", ReturnType = "bool", Summary = "Checks if RTSP source is available", IsStatic = true },
                    ],
                    Events =
                    [
                        new EventInfo { Name = "OnError", DelegateType = "EventHandler<ErrorEventArgs>", Summary = "Raised on error" },
                    ],
                    Constructors =
                    [
                        new MethodInfo
                        {
                            Name = "RTSPSourceBlock",
                            Summary = "Creates a new RTSP source block",
                            Parameters = [new ParameterInfo { Name = "settings", Type = "RTSPSourceSettings", Summary = "RTSP settings" }],
                        },
                    ],
                },
                new ClassInfo
                {
                    FullName = "VisioForge.Core.MediaBlocks.Sinks.MP4SinkBlock",
                    Name = "MP4SinkBlock",
                    Namespace = "VisioForge.Core.MediaBlocks.Sinks",
                    IsMediaBlock = true,
                    MediaBlockCategory = "Sinks",
                    Summary = "MP4 file output sink block",
                },
                new ClassInfo
                {
                    FullName = "VisioForge.Core.VideoCaptureX.VideoCaptureCoreX",
                    Name = "VideoCaptureCoreX",
                    Namespace = "VisioForge.Core.VideoCaptureX",
                    IsCoreSDK = true,
                    SdkModule = "Video Capture SDK X",
                    Summary = "Core class for video capture and recording",
                    Properties =
                    [
                        new PropertyInfo { Name = "Video_Source", Summary = "Gets or sets the video source" },
                    ],
                    Methods =
                    [
                        new MethodInfo { Name = "StartAsync", ReturnType = "Task", IsAsync = true, Summary = "Starts video capture" },
                        new MethodInfo { Name = "StopAsync", ReturnType = "Task", IsAsync = true, Summary = "Stops video capture" },
                    ],
                    Events =
                    [
                        new EventInfo { Name = "OnError", Summary = "Raised when an error occurs" },
                    ],
                },
            ],
            Enums =
            [
                new EnumInfo
                {
                    FullName = "VisioForge.Core.MediaBlocks.MediaBlockPadMediaType",
                    Name = "MediaBlockPadMediaType",
                    Namespace = "VisioForge.Core.MediaBlocks",
                    Summary = "Media type for block pads",
                    Values =
                    [
                        new EnumValueInfo { Name = "Video", Summary = "Video media content" },
                        new EnumValueInfo { Name = "Audio", Summary = "Audio media content" },
                        new EnumValueInfo { Name = "Subtitle", Summary = "Subtitle data" },
                    ],
                },
            ],
            MediaBlockCategories =
            [
                new MediaBlockCategory { Name = "Sources", Description = "Input source blocks", Blocks = ["RTSPSourceBlock"] },
                new MediaBlockCategory { Name = "Sinks", Description = "Output sink blocks", Blocks = ["MP4SinkBlock"] },
            ],
        };

        var dataField = typeof(ApiIndexService).GetField("_data",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        dataField!.SetValue(service, data);

        var buildLookups = typeof(ApiIndexService).GetMethod("BuildLookups",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        buildLookups!.Invoke(service, null);

        var buildSearch = typeof(ApiIndexService).GetMethod("BuildSearchIndex",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        buildSearch!.Invoke(service, null);

        return service;
    }

    // === MediaBlockTools ===

    [Fact]
    public void ListMediaBlocks_NoCategory_ShowsAllCategories()
    {
        var index = CreateTestIndex();
        var result = MediaBlockTools.ListMediaBlocks(index);

        Assert.Contains("Sources", result);
        Assert.Contains("Sinks", result);
        Assert.Contains("RTSPSourceBlock", result);
        Assert.Contains("MP4SinkBlock", result);
    }

    [Fact]
    public void ListMediaBlocks_WithCategory_FiltersCorrectly()
    {
        var index = CreateTestIndex();
        var result = MediaBlockTools.ListMediaBlocks(index, "Sources");

        Assert.Contains("RTSPSourceBlock", result);
        Assert.DoesNotContain("MP4SinkBlock", result);
    }

    [Fact]
    public void ListMediaBlocks_InvalidCategory_ShowsAvailable()
    {
        var index = CreateTestIndex();
        var result = MediaBlockTools.ListMediaBlocks(index, "NonExistent");

        Assert.Contains("No media blocks found", result);
        Assert.Contains("Available categories", result);
    }

    [Fact]
    public void GetMediaBlockInfo_ValidName_ReturnsFullDetails()
    {
        var index = CreateTestIndex();
        var result = MediaBlockTools.GetMediaBlockInfo(index, "RTSPSourceBlock");

        Assert.Contains("RTSPSourceBlock", result);
        Assert.Contains("Pads", result);
        Assert.Contains("VideoOutput", result);
        Assert.Contains("Properties", result);
        Assert.Contains("Settings", result);
        Assert.Contains("Methods", result);
        Assert.Contains("Build", result);
        Assert.Contains("Events", result);
        Assert.Contains("OnError", result);
        Assert.Contains("Constructors", result);
    }

    [Fact]
    public void GetMediaBlockInfo_WithoutBlockSuffix_StillFinds()
    {
        var index = CreateTestIndex();
        var result = MediaBlockTools.GetMediaBlockInfo(index, "RTSPSource");

        Assert.Contains("RTSPSourceBlock", result);
    }

    [Fact]
    public void GetMediaBlockInfo_NotFound_ReturnsHelpfulMessage()
    {
        var index = CreateTestIndex();
        var result = MediaBlockTools.GetMediaBlockInfo(index, "NonExistentBlock");

        Assert.Contains("not found", result);
        Assert.Contains("list_media_blocks", result);
    }

    // === ClassTools ===

    [Fact]
    public void ListSdkClasses_ReturnsCoreSdkClasses()
    {
        var index = CreateTestIndex();
        var result = ClassTools.ListSdkClasses(index);

        Assert.Contains("VideoCaptureCoreX", result);
        Assert.Contains("Video Capture SDK X", result);
        Assert.Contains("get_class_info", result);
    }

    [Fact]
    public void ListSdkClasses_ProductsFirst_LegacyLast()
    {
        var index = CreateTestIndex();
        index.Data.Classes.AddRange(
        [
            new ClassInfo { FullName = "VisioForge.Core.MediaPlayer.MediaPlayerCore", Name = "MediaPlayerCore", Namespace = "VisioForge.Core.MediaPlayer", IsCoreSDK = true, SdkModule = "Media Player SDK (Legacy)" },
            new ClassInfo { FullName = "VisioForge.Core.FastImageProcessingX.ColorspaceConverterX", Name = "ColorspaceConverterX", Namespace = "VisioForge.Core.FastImageProcessingX", IsCoreSDK = true, SdkModule = "Fast Image Processing SDK X" },
            new ClassInfo { FullName = "VisioForge.Core.MediaPlayerX.MediaPlayerCoreX", Name = "MediaPlayerCoreX", Namespace = "VisioForge.Core.MediaPlayerX", IsCoreSDK = true, SdkModule = "Media Player SDK X" },
        ]);

        var result = ClassTools.ListSdkClasses(index);
        var headings = result.Split('\n').Where(l => l.StartsWith("## ")).Select(l => l[3..].Trim()).ToList();

        Assert.Equal(["VideoCaptureCoreX", "MediaPlayerCoreX", "ColorspaceConverterX", "MediaPlayerCore"], headings);
    }

    [Fact]
    public void GetClassInfo_ValidName_ReturnsDetails()
    {
        var index = CreateTestIndex();
        var result = ClassTools.GetClassInfo(index, "VideoCaptureCoreX");

        Assert.Contains("VideoCaptureCoreX", result);
        Assert.Contains("Video_Source", result);
        Assert.Contains("StartAsync", result);
        Assert.Contains("StopAsync", result);
    }

    [Fact]
    public void GetClassInfo_NotFound_ReturnsMessage()
    {
        var index = CreateTestIndex();
        var result = ClassTools.GetClassInfo(index, "DoesNotExist");

        Assert.Contains("not found", result);
    }

    [Fact]
    public void GetMethodSignature_ValidMethod_ReturnsSignature()
    {
        var index = CreateTestIndex();
        var result = ClassTools.GetMethodSignature(index, "VideoCaptureCoreX", "StartAsync");

        Assert.Contains("StartAsync", result);
        Assert.Contains("Starts video capture", result);
    }

    [Fact]
    public void GetMethodSignature_InvalidMethod_ListsAvailable()
    {
        var index = CreateTestIndex();
        var result = ClassTools.GetMethodSignature(index, "VideoCaptureCoreX", "NonExistent");

        Assert.Contains("not found", result);
        Assert.Contains("Available methods", result);
    }

    // === SearchTools ===

    [Fact]
    public void SearchApi_FindsByClassName()
    {
        var index = CreateTestIndex();
        var result = SearchTools.SearchApi(index, "RTSP");

        Assert.Contains("RTSPSourceBlock", result);
        Assert.Contains("[MediaBlock]", result);
    }

    [Fact]
    public void SearchApi_FindsByDescription()
    {
        var index = CreateTestIndex();
        var result = SearchTools.SearchApi(index, "video capture");

        Assert.Contains("VideoCaptureCoreX", result);
    }

    [Fact]
    public void SearchApi_NoResults_ReturnsMessage()
    {
        var index = CreateTestIndex();
        var result = SearchTools.SearchApi(index, "xyznonexistent123");

        Assert.Contains("No results found", result);
    }

    [Fact]
    public void SearchApi_RespectsMaxResults()
    {
        var index = CreateTestIndex();
        var result = SearchTools.SearchApi(index, "block", maxResults: 1);

        // Should contain at most one entry
        var lines = result.Split('\n').Where(l => l.StartsWith("- **")).ToList();
        Assert.True(lines.Count <= 1);
    }

    // === EnumTools ===

    [Fact]
    public void GetEnumValues_ValidEnum_ReturnsValues()
    {
        var index = CreateTestIndex();
        var result = EnumTools.GetEnumValues(index, "MediaBlockPadMediaType");

        Assert.Contains("Video", result);
        Assert.Contains("Audio", result);
        Assert.Contains("Subtitle", result);
    }

    [Fact]
    public void GetEnumValues_WithFilter_FiltersValues()
    {
        var index = CreateTestIndex();
        var result = EnumTools.GetEnumValues(index, "MediaBlockPadMediaType", "Audio");

        Assert.Contains("Audio", result);
        Assert.Contains("Filtered by", result);
    }

    [Fact]
    public void GetEnumValues_NotFound_SuggestsAlternatives()
    {
        var index = CreateTestIndex();
        var result = EnumTools.GetEnumValues(index, "NonExistentEnum");

        Assert.Contains("not found", result);
    }

    // === NamespaceTools ===

    [Fact]
    public void ListNamespaces_NoParent_ShowsTopLevel()
    {
        var index = CreateTestIndex();
        var result = NamespaceTools.ListNamespaces(index);

        Assert.Contains("VisioForge.Core", result);
    }

    [Fact]
    public void ListNamespaces_WithParent_ShowsChildrenAndClasses()
    {
        var index = CreateTestIndex();
        var result = NamespaceTools.ListNamespaces(index, "VisioForge.Core.MediaBlocks.Sources");

        Assert.Contains("RTSPSourceBlock", result);
    }

    // === ExampleTools ===

    [Fact]
    public void GetCodeExample_ValidScenario_ReturnsCode()
    {
        var index = CreateTestIndex();
        var result = ExampleTools.GetCodeExample(index, "webcam-capture");

        Assert.Contains("VideoCaptureCoreX", result);
        Assert.Contains("StartAsync", result);
    }

    [Fact]
    public void GetCodeExample_InvalidScenario_ListsAvailable()
    {
        var index = CreateTestIndex();
        var result = ExampleTools.GetCodeExample(index, "nonexistent-scenario");

        Assert.Contains("Unknown scenario", result);
        Assert.Contains("Available examples", result);
    }

    [Fact]
    public void GetPipelineTemplate_ValidTemplate_ReturnsGraph()
    {
        var index = CreateTestIndex();
        var result = ExampleTools.GetPipelineTemplate(index, "rtsp-to-mp4");

        Assert.Contains("RTSPSourceBlock", result);
        Assert.Contains("MP4SinkBlock", result);
        Assert.Contains("Pipeline Graph", result);
    }

    [Fact]
    public void GetPipelineTemplate_InvalidTemplate_ListsAvailable()
    {
        var index = CreateTestIndex();
        var result = ExampleTools.GetPipelineTemplate(index, "nonexistent-template");

        Assert.Contains("Unknown template", result);
        Assert.Contains("Available pipeline templates", result);
    }

    // === Edge cases ===

    [Fact]
    public void AllTools_HandleEmptyIndex_Gracefully()
    {
        var emptyIndex = new ApiIndexService();
        // Empty index should not throw; just return empty/not-found messages

        var r1 = MediaBlockTools.ListMediaBlocks(emptyIndex);
        Assert.NotNull(r1);

        var r2 = MediaBlockTools.GetMediaBlockInfo(emptyIndex, "Anything");
        Assert.Contains("not found", r2);

        var r3 = ClassTools.ListSdkClasses(emptyIndex);
        Assert.NotNull(r3);

        var r4 = SearchTools.SearchApi(emptyIndex, "test");
        Assert.Contains("No results", r4);

        var r5 = EnumTools.GetEnumValues(emptyIndex, "test");
        Assert.Contains("not found", r5);

        var r6 = NamespaceTools.ListNamespaces(emptyIndex);
        Assert.NotNull(r6);
    }

    [Fact]
    public void GetNuGetPackagesSnippet_RejectsUnknownSdkTypeAndPlatform()
    {
        var index = CreateTestIndex();

        // sdkType is interpolated straight into "VisioForge.DotNet.{sdkType}". An agent
        // reading "Video Capture SDK .Net" in the prose and passing "Video Capture" would
        // otherwise get VisioForge.DotNet.Video Capture and NU1101 on the first restore.
        var badSdk = DeploymentTools.GetNuGetPackagesSnippet(index, "Windows", "WPF", "Video Capture");
        Assert.Contains("Unknown sdkType", badSdk);
        Assert.DoesNotContain("PackageReference", badSdk);

        // An unrecognised platform used to fall through every branch and return a csproj
        // with the managed wrapper and not one native redist — a DllNotFoundException at
        // runtime instead of an error the agent can act on.
        var badPlatform = DeploymentTools.GetNuGetPackagesSnippet(index, "OSX", "Console", "VideoCapture");
        Assert.Contains("Unknown platform", badPlatform);
        Assert.DoesNotContain("PackageReference", badPlatform);

        // projectType is interpolated into an XML comment, where "--" is illegal, and it
        // also selects the multi-target conditions below.
        var badProject = DeploymentTools.GetNuGetPackagesSnippet(index, "Windows", "Console -- .NET 8", "VideoCapture");
        Assert.Contains("Unknown projectType", badProject);
        Assert.DoesNotContain("<!--", badProject);

        // All three are case-insensitive, and a good call still produces a snippet.
        var ok = DeploymentTools.GetNuGetPackagesSnippet(index, "windows", "WPF", "videocapture");
        Assert.Contains("VisioForge.DotNet.VideoCapture", ok);
        Assert.Contains("VisioForge.CrossPlatform.Core.Windows.x64", ok);
    }

    [Fact]
    public void GetNuGetPackagesSnippet_ConditionsRedistsOnlyWhereTheConditionCanBeTrue()
    {
        var index = CreateTestIndex();

        // "-linux" is not a .NET platform TFM suffix, so a TargetFramework.Contains
        // check on it is never true and the redist would never restore — the app then
        // dies at GStreamer init. A single-target project needs no condition at all:
        // the caller already said which platform this is for.
        var linuxConsole = DeploymentTools.GetNuGetPackagesSnippet(index, "Linux", "Console", "VideoCapture");
        Assert.Contains("VisioForge.CrossPlatform.Core.Linux.x64", linuxConsole);
        Assert.DoesNotContain("-linux", linuxConsole);

        // Same for a plain net8.0 Windows console project: no "-windows" in its TFM.
        var winConsole = DeploymentTools.GetNuGetPackagesSnippet(index, "Windows", "Console", "VideoCapture");
        Assert.Contains("VisioForge.CrossPlatform.Core.Windows.x64", winConsole);
        Assert.DoesNotContain("TargetFramework.Contains('-windows')", winConsole);

        // A genuinely multi-targeted project does need the guard, and for Linux it has
        // to be IsOsPlatform rather than a TFM test.
        var avalonia = DeploymentTools.GetNuGetPackagesSnippet(index, "Windows", "Avalonia", "VideoCapture");
        Assert.Contains("TargetFramework.Contains('-windows')", avalonia);
        var avaloniaLinux = DeploymentTools.GetNuGetPackagesSnippet(index, "Linux", "Avalonia", "VideoCapture");
        Assert.Contains("IsOsPlatform('Linux')", avaloniaLinux);

        // macOS has a real "-macos" suffix, but a plain net8.0 console or Blazor project
        // on macOS does not carry it either — same trap as Windows.
        var macConsole = DeploymentTools.GetNuGetPackagesSnippet(index, "macOS", "Console", "VideoCapture");
        Assert.Contains("VisioForge.CrossPlatform.Core.macOS", macConsole);
        Assert.DoesNotContain("TargetFramework.Contains('-macos')", macConsole);
        var macMaui = DeploymentTools.GetNuGetPackagesSnippet(index, "macOS", "MAUI", "VideoCapture");
        Assert.Contains("TargetFramework.Contains('-macos')", macMaui);

        // The mobile suffixes are real and stay conditioned either way.
        var android = DeploymentTools.GetNuGetPackagesSnippet(index, "Android", "MAUI", "VideoCapture");
        Assert.Contains("TargetFramework.Contains('-android')", android);
    }

    [Fact]
    public void GetNuGetPackagesSnippet_OptionalBlockIsWellFormedXml()
    {
        // XML comments do not nest: wrapping the optional block in <!-- --> while its
        // body carried "<!-- Computer Vision -->" ended the wrapper early and left the
        // rest unparseable, so the tool's own copy-paste output broke the csproj.
        var snippet = DeploymentTools.GetNuGetPackagesSnippet(
            CreateTestIndex(), "Windows", "WPF", "VideoCapture", includeOptional: true);

        var doc = new System.Xml.XmlDocument();
        doc.LoadXml("<Project>" + snippet + "</Project>");   // throws if malformed

        Assert.Contains("OpenCV", snippet);
    }
}
