using VisioForge.MCP.IndexBuilder;
using VisioForge.MCP.Shared;
using Xunit;

namespace VisioForge.MCP.Tests;

public class IndexGeneratorTests
{
    /// <summary>
    /// Builds a minimal IndexGenerator with the given XML member entries and source files.
    /// </summary>
    private static IndexGenerator CreateGenerator(string xmlContent, string? sourceDir = null)
    {
        var xmlParser = new XmlDocParser();

        // Write XML to a temp file and load it
        var xmlPath = Path.GetTempFileName();
        File.WriteAllText(xmlPath, xmlContent);
        xmlParser.Load(xmlPath);
        File.Delete(xmlPath);

        var srcDir = sourceDir ?? Path.Combine(Path.GetTempPath(), $"test_src_{Guid.NewGuid():N}");
        if (!Directory.Exists(srcDir))
            Directory.CreateDirectory(srcDir);

        var sourceAnalyzer = new SourceAnalyzer(srcDir);
        sourceAnalyzer.Analyze();

        return new IndexGenerator(xmlParser, sourceAnalyzer);
    }

    private static string WrapXml(string members) =>
        $"""
        <?xml version="1.0"?>
        <doc>
          <assembly><name>Test</name></assembly>
          <members>
            {members}
          </members>
        </doc>
        """;

    [Fact]
    public void Generate_SkipsCompilerGeneratedTypes()
    {
        var xml = WrapXml("""
            <member name="T:Namespace.NormalClass"><summary>A normal class.</summary></member>
            <member name="T:Namespace.&lt;&gt;c__DisplayClass0_0"><summary>Compiler generated.</summary></member>
            <member name="T:Namespace.&lt;Main&gt;d__0"><summary>Async state machine.</summary></member>
        """);

        var generator = CreateGenerator(xml);
        var index = generator.Generate("1.0");

        Assert.Single(index.Classes);
        Assert.Equal("NormalClass", index.Classes[0].Name);
    }

    [Fact]
    public void Generate_SkipsDoubleUnderscoreTypes()
    {
        var xml = WrapXml("""
            <member name="T:Namespace.GoodClass"><summary>Good.</summary></member>
            <member name="T:Namespace.__InternalType"><summary>Internal.</summary></member>
        """);

        var generator = CreateGenerator(xml);
        var index = generator.Generate("1.0");

        Assert.Single(index.Classes);
        Assert.Equal("GoodClass", index.Classes[0].Name);
    }

    [Fact]
    public void Generate_SkipsInternalNamespaces()
    {
        var xml = WrapXml("""
            <member name="T:VisioForge.Core.GStreamer.Base.InternalClass"><summary>Internal.</summary></member>
            <member name="T:VisioForge.Core.DirectShow.Interfaces.IInternal"><summary>Internal.</summary></member>
            <member name="T:VisioForge.Core.Public.PublicClass"><summary>Public.</summary></member>
        """);

        var generator = CreateGenerator(xml);
        var index = generator.Generate("1.0");

        Assert.Single(index.Classes);
        Assert.Equal("PublicClass", index.Classes[0].Name);
    }

    [Fact]
    public void Generate_DetectsEnumsByValueField()
    {
        var xml = WrapXml("""
            <member name="T:Namespace.MyEnum"><summary>An enum.</summary></member>
            <member name="F:Namespace.MyEnum.value__"><summary></summary></member>
            <member name="F:Namespace.MyEnum.First"><summary>First value.</summary></member>
            <member name="F:Namespace.MyEnum.Second"><summary>Second value.</summary></member>
        """);

        var generator = CreateGenerator(xml);
        var index = generator.Generate("1.0");

        Assert.Empty(index.Classes);
        Assert.Single(index.Enums);
        Assert.Equal("MyEnum", index.Enums[0].Name);
        Assert.Equal(2, index.Enums[0].Values.Count);
    }

    [Fact]
    public void Generate_DetectsInterfacesByNamingConvention()
    {
        var xml = WrapXml("""
            <member name="T:Namespace.IMediaBlock"><summary>Interface.</summary></member>
            <member name="T:Namespace.RegularClass"><summary>Class.</summary></member>
        """);

        var generator = CreateGenerator(xml);
        var index = generator.Generate("1.0");

        var iface = index.Classes.First(c => c.Name == "IMediaBlock");
        Assert.Equal("interface", iface.Kind);

        var cls = index.Classes.First(c => c.Name == "RegularClass");
        Assert.Equal("class", cls.Kind);
    }

    [Fact]
    public void Generate_InfersAsyncReturnType()
    {
        var xml = WrapXml("""
            <member name="T:Namespace.MyClass"><summary>A class.</summary></member>
            <member name="M:Namespace.MyClass.DoAsync"><summary>Async method.</summary></member>
            <member name="M:Namespace.MyClass.DoSync"><summary>Sync method.</summary></member>
        """);

        var generator = CreateGenerator(xml);
        var index = generator.Generate("1.0");

        var cls = index.Classes.First();
        var asyncMethod = cls.Methods.First(m => m.Name == "DoAsync");
        Assert.Equal("Task", asyncMethod.ReturnType);

        var syncMethod = cls.Methods.First(m => m.Name == "DoSync");
        Assert.Equal("void", syncMethod.ReturnType);
    }

    [Fact]
    public void Generate_HandlesMethodsWithParameters()
    {
        var xml = WrapXml("""
            <member name="T:Namespace.MyClass"><summary>A class.</summary></member>
            <member name="M:Namespace.MyClass.Process(System.String,System.Int32)">
                <summary>Processes data.</summary>
                <param name="input">The input string.</param>
                <param name="count">Number of iterations.</param>
                <returns>True if successful.</returns>
            </member>
        """);

        var generator = CreateGenerator(xml);
        var index = generator.Generate("1.0");

        var cls = index.Classes.First();
        var method = cls.Methods.First(m => m.Name == "Process");
        Assert.Equal(2, method.Parameters.Count);
        Assert.Equal("input", method.Parameters[0].Name);
        Assert.Equal("string", method.Parameters[0].Type);
        Assert.Equal("count", method.Parameters[1].Name);
        Assert.Equal("int", method.Parameters[1].Type);
        Assert.Equal("bool", method.ReturnType);
    }

    [Fact]
    public void Generate_HandlesMismatchedParamDocsCount()
    {
        // More param types from signature than <param> docs
        var xml = WrapXml("""
            <member name="T:Namespace.MyClass"><summary>A class.</summary></member>
            <member name="M:Namespace.MyClass.Do(System.String,System.Int32,System.Boolean)">
                <summary>Does stuff.</summary>
                <param name="input">The input.</param>
            </member>
        """);

        var generator = CreateGenerator(xml);
        var index = generator.Generate("1.0");

        var method = index.Classes.First().Methods.First();
        Assert.Equal(3, method.Parameters.Count);
        Assert.Equal("input", method.Parameters[0].Name);
        Assert.Equal("param1", method.Parameters[1].Name);
        Assert.Equal("param2", method.Parameters[2].Name);
    }

    [Fact]
    public void Generate_SetsNamespaces()
    {
        var xml = WrapXml("""
            <member name="T:A.B.Class1"><summary>C1.</summary></member>
            <member name="T:A.C.Class2"><summary>C2.</summary></member>
            <member name="T:A.B.Class3"><summary>C3.</summary></member>
        """);

        var generator = CreateGenerator(xml);
        var index = generator.Generate("1.0");

        Assert.Equal(2, index.Namespaces.Count);
        Assert.Contains("A.B", index.Namespaces);
        Assert.Contains("A.C", index.Namespaces);
    }
}
