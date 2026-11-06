using System.Text.Json.Serialization;

namespace VisioForge.MCP.Shared;

/// <summary>
/// Root container for the entire API index.
/// Shared between the IndexBuilder (producer) and Server (consumer).
/// </summary>
public class ApiIndex
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; set; } = "";

    [JsonPropertyName("sdkVersion")]
    public string SdkVersion { get; set; } = "";

    [JsonPropertyName("classes")]
    public List<ClassInfo> Classes { get; set; } = [];

    [JsonPropertyName("enums")]
    public List<EnumInfo> Enums { get; set; } = [];

    [JsonPropertyName("namespaces")]
    public List<string> Namespaces { get; set; } = [];

    [JsonPropertyName("mediaBlockCategories")]
    public List<MediaBlockCategory> MediaBlockCategories { get; set; } = [];

    [JsonPropertyName("deploymentGuides")]
    public List<DeploymentGuide> DeploymentGuides { get; set; } = [];
}

public class ClassInfo
{
    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "class";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("remarks")]
    public string Remarks { get; set; } = "";

    [JsonPropertyName("baseClass")]
    public string? BaseClass { get; set; }

    [JsonPropertyName("interfaces")]
    public List<string> Interfaces { get; set; } = [];

    [JsonPropertyName("isMediaBlock")]
    public bool IsMediaBlock { get; set; }

    [JsonPropertyName("mediaBlockCategory")]
    public string? MediaBlockCategory { get; set; }

    [JsonPropertyName("pads")]
    public List<PadInfo> Pads { get; set; } = [];

    [JsonPropertyName("constructors")]
    public List<MethodInfo> Constructors { get; set; } = [];

    [JsonPropertyName("properties")]
    public List<PropertyInfo> Properties { get; set; } = [];

    [JsonPropertyName("methods")]
    public List<MethodInfo> Methods { get; set; } = [];

    [JsonPropertyName("events")]
    public List<EventInfo> Events { get; set; } = [];

    [JsonPropertyName("isCoreSDK")]
    public bool IsCoreSDK { get; set; }

    [JsonPropertyName("sdkModule")]
    public string? SdkModule { get; set; }
}

public class PropertyInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("accessors")]
    public string Accessors { get; set; } = "get; set;";
}

public class MethodInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("returnType")]
    public string ReturnType { get; set; } = "void";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("remarks")]
    public string Remarks { get; set; } = "";

    [JsonPropertyName("parameters")]
    public List<ParameterInfo> Parameters { get; set; } = [];

    [JsonPropertyName("returns")]
    public string Returns { get; set; } = "";

    [JsonPropertyName("isStatic")]
    public bool IsStatic { get; set; }

    [JsonPropertyName("isAsync")]
    public bool IsAsync { get; set; }
}

public class ParameterInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";
}

public class EventInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("delegateType")]
    public string DelegateType { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";
}

public class PadInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "";

    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = "";
}

public class EnumInfo
{
    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("values")]
    public List<EnumValueInfo> Values { get; set; } = [];
}

public class EnumValueInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("numericValue")]
    public string? NumericValue { get; set; }
}

public class MediaBlockCategory
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("blocks")]
    public List<string> Blocks { get; set; } = [];
}

/// <summary>
/// Deployment guide metadata and content
/// </summary>
public class DeploymentGuide
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("platforms")]
    public List<string> Platforms { get; set; } = [];

    [JsonPropertyName("projectTypes")]
    public List<string> ProjectTypes { get; set; } = [];

    [JsonPropertyName("sdkTypes")]
    public List<string> SdkTypes { get; set; } = [];

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = "intermediate";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("nugetPackages")]
    public List<NuGetPackageReference> NuGetPackages { get; set; } = [];

    [JsonPropertyName("codeSnippets")]
    public List<CodeSnippet> CodeSnippets { get; set; } = [];

    [JsonPropertyName("platformSpecificNotes")]
    public Dictionary<string, string> PlatformSpecificNotes { get; set; } = [];
}

/// <summary>
/// NuGet package reference with conditions
/// </summary>
public class NuGetPackageReference
{
    [JsonPropertyName("packageId")]
    public string PackageId { get; set; } = "";

    [JsonPropertyName("version")]
    // Empty, not a hardcoded version. A default of "2026.2.4" meant a reference whose
    // version was missing came back looking authoritative and eighteen months stale.
    public string Version { get; set; } = "";

    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = "";
}

/// <summary>
/// Code snippet for deployment guides
/// </summary>
public class CodeSnippet
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "xml";

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}
