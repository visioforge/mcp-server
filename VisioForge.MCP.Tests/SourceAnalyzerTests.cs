using VisioForge.MCP.IndexBuilder;
using VisioForge.MCP.Shared;
using Xunit;

namespace VisioForge.MCP.Tests;

public class SourceAnalyzerTests
{
    private static SourceAnalyzer CreateAnalyzerWithSource(params (string RelativePath, string Content)[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), $"test_src_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        foreach (var (relativePath, content) in files)
        {
            var fullPath = Path.Combine(root, relativePath);
            var dir = Path.GetDirectoryName(fullPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(fullPath, content);
        }

        var analyzer = new SourceAnalyzer(root);
        analyzer.Analyze();
        return analyzer;
    }

    [Fact]
    public void GetInheritance_DetectsBaseClassAndInterfaces()
    {
        var analyzer = CreateAnalyzerWithSource(
            ("MyBlock.cs", """
            namespace VisioForge.Core;
            public class MyBlock : MediaBlockBase, IMediaBlock, IDisposable
            {
            }
            """));

        var (baseClass, interfaces) = analyzer.GetInheritance("VisioForge.Core.MyBlock");
        Assert.Equal("MediaBlockBase", baseClass);
        Assert.Contains("IMediaBlock", interfaces);
        Assert.Contains("IDisposable", interfaces);
    }

    [Fact]
    public void GetInheritance_HandlesClassWithoutInheritance()
    {
        var analyzer = CreateAnalyzerWithSource(
            ("SimpleClass.cs", """
            namespace VisioForge.Core;
            public class SimpleClass
            {
                public int Value { get; set; }
            }
            """));

        var (baseClass, interfaces) = analyzer.GetInheritance("VisioForge.Core.SimpleClass");
        // Class without inheritance should still be found (no base, no interfaces)
        // but shouldn't cause errors
        Assert.Null(baseClass);
        Assert.Empty(interfaces);
    }

    [Fact]
    public void GetInheritance_HandlesGenericBaseClass()
    {
        var analyzer = CreateAnalyzerWithSource(
            ("GenericChild.cs", """
            namespace VisioForge.Core;
            public class GenericChild : BaseClass<string, int>, IComparable
            {
            }
            """));

        var (baseClass, interfaces) = analyzer.GetInheritance("VisioForge.Core.GenericChild");
        Assert.Equal("BaseClass<string, int>", baseClass);
        Assert.Contains("IComparable", interfaces);
    }

    [Fact]
    public void GetPads_DetectsPadDefinitions()
    {
        var analyzer = CreateAnalyzerWithSource(
            ("TestBlock.cs", """
            namespace VisioForge.Core;
            public class TestBlock
            {
                public TestBlock()
                {
                    var videoOut = new MediaBlockPad(this, MediaBlockPadDirection.Out, MediaBlockPadMediaType.Video);
                    var audioOut = new MediaBlockPad(this, MediaBlockPadDirection.Out, MediaBlockPadMediaType.Audio);
                    var videoIn = new MediaBlockPad(this, MediaBlockPadDirection.In, MediaBlockPadMediaType.Video);
                }
            }
            """));

        var pads = analyzer.GetPads("VisioForge.Core.TestBlock");
        Assert.NotNull(pads);
        Assert.Equal(3, pads.Count);
        Assert.Contains(pads, p => p.Direction == "Out" && p.MediaType == "Video");
        Assert.Contains(pads, p => p.Direction == "Out" && p.MediaType == "Audio");
        Assert.Contains(pads, p => p.Direction == "In" && p.MediaType == "Video");
    }

    [Fact]
    public void GetPads_ScopedToCorrectClass_InMultiClassFile()
    {
        var analyzer = CreateAnalyzerWithSource(
            ("MultiClass.cs", """
            namespace VisioForge.Core;
            public class BlockA
            {
                public BlockA()
                {
                    var pad = new MediaBlockPad(this, MediaBlockPadDirection.Out, MediaBlockPadMediaType.Video);
                }
            }
            public class BlockB
            {
                public BlockB()
                {
                    var pad = new MediaBlockPad(this, MediaBlockPadDirection.In, MediaBlockPadMediaType.Audio);
                }
            }
            """));

        var padsA = analyzer.GetPads("VisioForge.Core.BlockA");
        Assert.NotNull(padsA);
        Assert.Single(padsA);
        Assert.Equal("Out", padsA[0].Direction);
        Assert.Equal("Video", padsA[0].MediaType);

        var padsB = analyzer.GetPads("VisioForge.Core.BlockB");
        Assert.NotNull(padsB);
        Assert.Single(padsB);
        Assert.Equal("In", padsB[0].Direction);
        Assert.Equal("Audio", padsB[0].MediaType);
    }

    [Fact]
    public void GetMediaBlockCategory_ByDirectory()
    {
        var analyzer = CreateAnalyzerWithSource(
            ("MediaBlocks/Sources/RTSPSourceBlock.cs", """
            namespace VisioForge.Core.MediaBlocks.Sources;
            public class RTSPSourceBlock
            {
            }
            """));

        var category = analyzer.GetMediaBlockCategory("RTSPSourceBlock");
        Assert.Equal("Sources", category);
    }

    [Fact]
    public void GetMediaBlockCategory_ReturnsNullForUnknown()
    {
        var analyzer = CreateAnalyzerWithSource(
            ("SomeClass.cs", """
            namespace VisioForge.Core;
            public class SomeClass { }
            """));

        var category = analyzer.GetMediaBlockCategory("SomeClass");
        Assert.Null(category);
    }

    [Fact]
    public void IsCoreSDK_RecognizesKnownClasses()
    {
        var analyzer = CreateAnalyzerWithSource(
            ("Dummy.cs", "namespace Test; public class Dummy { }"));

        Assert.True(analyzer.IsCoreSDK("VideoCaptureCoreX"));
        Assert.True(analyzer.IsCoreSDK("MediaPlayerCoreX"));
        Assert.True(analyzer.IsCoreSDK("VideoEditCoreX"));
        Assert.False(analyzer.IsCoreSDK("SomeRandomClass"));
    }

    [Fact]
    public void GetSdkModule_ReturnsCorrectModule()
    {
        var analyzer = CreateAnalyzerWithSource(
            ("Dummy.cs", "namespace Test; public class Dummy { }"));

        Assert.Equal("Video Capture SDK X", analyzer.GetSdkModule("VideoCaptureCoreX"));
        Assert.Equal("Media Player SDK X", analyzer.GetSdkModule("MediaPlayerCoreX"));
        Assert.Null(analyzer.GetSdkModule("UnknownClass"));
    }

    [Fact]
    public void GetInheritance_HandlesAbstractSealedPartialModifiers()
    {
        var analyzer = CreateAnalyzerWithSource(
            ("Modifiers.cs", """
            namespace VisioForge.Core;
            public abstract class AbstractBlock : BaseBlock
            {
            }
            public sealed class SealedBlock : BaseBlock
            {
            }
            public partial class PartialBlock : BaseBlock
            {
            }
            """));

        var (abs, _) = analyzer.GetInheritance("VisioForge.Core.AbstractBlock");
        Assert.Equal("BaseBlock", abs);

        var (seal, _) = analyzer.GetInheritance("VisioForge.Core.SealedBlock");
        Assert.Equal("BaseBlock", seal);

        var (part, _) = analyzer.GetInheritance("VisioForge.Core.PartialBlock");
        Assert.Equal("BaseBlock", part);
    }
}
