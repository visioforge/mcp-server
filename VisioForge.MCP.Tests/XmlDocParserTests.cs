using VisioForge.MCP.IndexBuilder;
using Xunit;

namespace VisioForge.MCP.Tests;

public class XmlDocParserTests
{
    [Theory]
    [InlineData("System.String", "string")]
    [InlineData("System.Int32", "int")]
    [InlineData("System.Boolean", "bool")]
    [InlineData("System.Double", "double")]
    [InlineData("System.Void", "void")]
    [InlineData("System.Object", "object")]
    [InlineData("System.Byte[]", "byte[]")]
    public void SimplifyTypeName_HandlesCommonTypes(string input, string expected)
    {
        var result = XmlDocParser.SimplifyTypeName(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SimplifyTypeName_HandlesNullable()
    {
        var result = XmlDocParser.SimplifyTypeName("System.Nullable{System.Int32}");
        Assert.Equal("int?", result);
    }

    [Fact]
    public void SimplifyTypeName_HandlesVisioForgeTypes()
    {
        var result = XmlDocParser.SimplifyTypeName("VisioForge.Core.MediaBlocks.Sources.RTSPSourceBlock");
        Assert.Equal("Sources.RTSPSourceBlock", result);
    }

    [Fact]
    public void SimplifyTypeName_HandlesGenerics()
    {
        var result = XmlDocParser.SimplifyTypeName("System.Collections.Generic.List{System.String}");
        Assert.Equal("List<string>", result);
    }

    [Theory]
    [InlineData("M:VisioForge.Core.MyClass.MyMethod(System.String,System.Int32)", "MyMethod", 2)]
    [InlineData("M:VisioForge.Core.MyClass.Simple", "Simple", 0)]
    [InlineData("M:VisioForge.Core.MyClass.#ctor(System.String)", "#ctor", 1)]
    public void ParseMethodKey_ExtractsNameAndParams(string key, string expectedName, int expectedParamCount)
    {
        var (name, paramTypes) = XmlDocParser.ParseMethodKey(key);
        Assert.Equal(expectedName, name);
        Assert.Equal(expectedParamCount, paramTypes.Count);
    }

    [Fact]
    public void ParseMethodKey_HandlesGenericParams()
    {
        var (name, paramTypes) = XmlDocParser.ParseMethodKey(
            "M:VisioForge.Core.MyClass.Do(System.Collections.Generic.List{System.String},System.Int32)");
        Assert.Equal("Do", name);
        Assert.Equal(2, paramTypes.Count);
        Assert.Equal("List<string>", paramTypes[0]);
        Assert.Equal("int", paramTypes[1]);
    }
}
