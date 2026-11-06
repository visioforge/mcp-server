using System.Text.Json;
using VisioForge.MCP.IndexBuilder;

// VisioForge MCP Index Builder
// Parses XML documentation and C# source files to create a searchable API index.
//
// Usage: dotnet run -- <xml-doc-path> <source-root-path> <output-path> [sdk-version] [deployment-guides-path]
// Example: dotnet run -- ../VisioForge.Core.xml ../VisioForge.Core/ ./api-index.json 2026.8.16.0 ../VisioForge.MCP.Server/Data/deployment-guides

if (args.Length < 3)
{
    Console.WriteLine("VisioForge MCP Index Builder");
    Console.WriteLine("Usage: VisioForge.MCP.IndexBuilder <xml-doc-path> <source-root-path> <output-path> [sdk-version] [deployment-guides-path]");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  xml-doc-path          Path to VisioForge.Core.xml documentation file");
    Console.WriteLine("  source-root-path      Path to VisioForge.Core source directory");
    Console.WriteLine("  output-path           Path for the output api-index.json file");
    Console.WriteLine("  sdk-version           Optional SDK version string (default: from assembly)");
    Console.WriteLine("  deployment-guides-path Path to the deployment guides directory. Optional only in the");
    Console.WriteLine("                        sense that it has a CWD-relative default, which resolves only");
    Console.WriteLine("                        when run from the IndexBuilder directory. Pass it explicitly:");
    Console.WriteLine("                        ../VisioForge.MCP.Server/Data/deployment-guides");
    return 1;
}

var xmlDocPath = Path.GetFullPath(args[0]);
var sourceRootPath = Path.GetFullPath(args[1]);
var outputPath = Path.GetFullPath(args[2]);
var sdkVersion = args.Length > 3 ? args[3] : "unknown";
// The default is CWD-relative and only correct when invoked from the IndexBuilder
// directory. It stays for the documented `dotnet run` form, but a wrong guess is now
// a hard failure in DeploymentGuideParser rather than an index quietly built with zero
// deployment guides — which is how production came to answer "not found" for all 15.
var deploymentGuidesPath = args.Length > 4 ? Path.GetFullPath(args[4]) : Path.GetFullPath("../VisioForge.MCP.Server/Data/deployment-guides");

Console.WriteLine("=== VisioForge MCP Index Builder ===");
Console.WriteLine($"XML Docs:         {xmlDocPath}");
Console.WriteLine($"Source Root:      {sourceRootPath}");
Console.WriteLine($"Output:           {outputPath}");
Console.WriteLine($"SDK Version:      {sdkVersion}");
Console.WriteLine($"Deployment Guides: {deploymentGuidesPath}");
Console.WriteLine();

// Validate inputs
if (!File.Exists(xmlDocPath))
{
    Console.Error.WriteLine($"Error: XML documentation file not found: {xmlDocPath}");
    return 1;
}

if (!Directory.Exists(sourceRootPath))
{
    Console.Error.WriteLine($"Error: Source root directory not found: {sourceRootPath}");
    return 1;
}

try
{
    // Step 1: Parse XML documentation
    var xmlParser = new XmlDocParser();
    xmlParser.Load(xmlDocPath);

    // Step 2: Analyze source files
    var sourceAnalyzer = new SourceAnalyzer(sourceRootPath);
    sourceAnalyzer.Analyze();

    // Step 3: Generate unified index
    var generator = new IndexGenerator(xmlParser, sourceAnalyzer);
    var index = generator.Generate(sdkVersion);

    // Step 3.5: Parse deployment guides
    var deploymentParser = new DeploymentGuideParser(deploymentGuidesPath);
    index.DeploymentGuides = deploymentParser.ParseAll();

    // Step 4: Write output
    var outputDir = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
    {
        Directory.CreateDirectory(outputDir);
    }

    var options = new JsonSerializerOptions
    {
        WriteIndented = false, // Compact for production
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
    };

    var json = JsonSerializer.Serialize(index, options);
    File.WriteAllText(outputPath, json);

    var fileSize = new FileInfo(outputPath).Length;
    Console.WriteLine();
    Console.WriteLine($"=== Index built successfully ===");
    Console.WriteLine($"Output: {outputPath} ({fileSize / 1024.0 / 1024.0:F2} MB)");
    Console.WriteLine($"Classes: {index.Classes.Count}");
    Console.WriteLine($"Enums: {index.Enums.Count}");
    Console.WriteLine($"Namespaces: {index.Namespaces.Count}");
    Console.WriteLine($"Media Block Categories: {index.MediaBlockCategories.Count}");
    Console.WriteLine($"Deployment Guides: {index.DeploymentGuides.Count}");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fatal error: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    return 1;
}
