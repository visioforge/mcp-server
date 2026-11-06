using VisioForge.MCP.Server.Index;
using VisioForge.MCP.Shared;
using ApiIndexData = VisioForge.MCP.Shared.ApiIndex;
using Xunit;

namespace VisioForge.MCP.Tests;

public class ApiIndexServiceTests
{
    private static ApiIndexService CreateTestIndex()
    {
        var service = new ApiIndexService();

        // Build a minimal test index manually
        var data = new ApiIndexData
        {
            Version = "1.0",
            SdkVersion = "test",
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
                        new PropertyInfo { Name = "Audio_Source", Summary = "Gets or sets the audio source" },
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

        // Use reflection to set _data since Load() requires a file
        var dataField = typeof(ApiIndexService).GetField("_data",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        dataField!.SetValue(service, data);

        // Call BuildLookups and BuildSearchIndex via reflection
        var buildLookups = typeof(ApiIndexService).GetMethod("BuildLookups",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        buildLookups!.Invoke(service, null);

        var buildSearch = typeof(ApiIndexService).GetMethod("BuildSearchIndex",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        buildSearch!.Invoke(service, null);

        return service;
    }

    [Fact]
    public void GetClassByName_FindsClass()
    {
        var index = CreateTestIndex();
        var cls = index.GetClassByName("RTSPSourceBlock");
        Assert.NotNull(cls);
        Assert.Equal("RTSPSourceBlock", cls.Name);
        Assert.True(cls.IsMediaBlock);
    }

    [Fact]
    public void GetClassByName_CaseInsensitive()
    {
        var index = CreateTestIndex();
        var cls = index.GetClassByName("rtspsourceblock");
        Assert.NotNull(cls);
    }

    [Fact]
    public void GetClassByFullName_FindsClass()
    {
        var index = CreateTestIndex();
        var cls = index.GetClassByFullName("VisioForge.Core.MediaBlocks.Sources.RTSPSourceBlock");
        Assert.NotNull(cls);
    }

    [Fact]
    public void GetMediaBlocks_ReturnsAllBlocks()
    {
        var index = CreateTestIndex();
        var blocks = index.GetMediaBlocks();
        Assert.Equal(2, blocks.Count);
    }

    [Fact]
    public void GetMediaBlocks_FiltersByCategory()
    {
        var index = CreateTestIndex();
        var sources = index.GetMediaBlocks("Sources");
        Assert.Single(sources);
        Assert.Equal("RTSPSourceBlock", sources[0].Name);
    }

    [Fact]
    public void GetCoreSdkClasses_ReturnsCoreClasses()
    {
        var index = CreateTestIndex();
        var core = index.GetCoreSdkClasses();
        Assert.Single(core);
        Assert.Equal("VideoCaptureCoreX", core[0].Name);
    }

    [Fact]
    public void GetEnumByName_FindsEnum()
    {
        var index = CreateTestIndex();
        var e = index.GetEnumByName("MediaBlockPadMediaType");
        Assert.NotNull(e);
        Assert.Equal(3, e.Values.Count);
    }

    [Fact]
    public void Search_FindsByClassName()
    {
        var index = CreateTestIndex();
        var results = index.Search("RTSP");
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Name == "RTSPSourceBlock");
    }

    [Fact]
    public void Search_FindsByDescription()
    {
        var index = CreateTestIndex();
        var results = index.Search("video capture recording");
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Name == "VideoCaptureCoreX");
    }

    [Fact]
    public void Search_FindsEnums()
    {
        var index = CreateTestIndex();
        var results = index.Search("media type pad");
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Kind == "enum");
    }

    [Fact]
    public void GetChildNamespaces_ReturnsChildren()
    {
        var index = CreateTestIndex();
        var children = index.GetChildNamespaces("VisioForge.Core.MediaBlocks");
        Assert.Contains("VisioForge.Core.MediaBlocks.Sources", children);
        Assert.Contains("VisioForge.Core.MediaBlocks.Sinks", children);
    }

    [Fact]
    public void GetClassesByNamespace_ReturnsClasses()
    {
        var index = CreateTestIndex();
        var classes = index.GetClassesByNamespace("VisioForge.Core.MediaBlocks.Sources");
        Assert.Single(classes);
    }
}
