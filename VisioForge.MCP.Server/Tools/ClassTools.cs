using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using VisioForge.MCP.Server.Index;

namespace VisioForge.MCP.Server.Tools;

[McpServerToolType]
public static class ClassTools
{
    private static readonly string[] ProductClasses = ["VideoCaptureCoreX", "MediaPlayerCoreX", "VideoEditCoreX"];

    [McpServerTool(ReadOnly = true, Idempotent = true, OpenWorld = false), Description(
        "List core VisioForge SDK classes. These are the main entry-point classes for building media applications: " +
        "VideoCaptureCoreX (video capture/recording), VideoEditCoreX (video editing), " +
        "MediaPlayerCoreX (media playback), MediaInfoReaderX (media analysis), " +
        "SimplePlayerCoreX (simple playback), and more.")]
    public static string ListSdkClasses(ApiIndexService index)
    {
        var coreClasses = index.GetCoreSdkClasses();
        var sb = new StringBuilder();

        sb.AppendLine("# VisioForge Core SDK Classes\n");
        sb.AppendLine("These are the main entry-point classes for building media applications.\n");

        // The three products lead, the legacy DirectShow cores trail. Alphabetical order put
        // ColorspaceConverterX, MediaInfoReaderX and the legacy MediaPlayerCore ahead of every
        // product class, and an agent takes the top of a list as the starting point (#542).
        var ordered = coreClasses
            .OrderBy(c => Array.IndexOf(ProductClasses, c.Name) is var i && i >= 0 ? i : ProductClasses.Length)
            .ThenBy(c => c.SdkModule?.Contains("Legacy", StringComparison.OrdinalIgnoreCase) == true)
            .ThenBy(c => c.Name, StringComparer.Ordinal);

        foreach (var cls in ordered)
        {
            sb.AppendLine($"## {cls.Name}");
            if (!string.IsNullOrEmpty(cls.SdkModule))
                sb.AppendLine($"**Module:** {cls.SdkModule}");
            sb.AppendLine($"**Namespace:** `{cls.Namespace}`");
            if (!string.IsNullOrEmpty(cls.Summary))
                sb.AppendLine(cls.Summary);
            sb.AppendLine($"Properties: {cls.Properties.Count} | Methods: {cls.Methods.Count} | Events: {cls.Events.Count}");
            sb.AppendLine();
        }

        sb.AppendLine("Use `get_class_info` with a class name to see full details (properties, methods, events).");
        return sb.ToString();
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, OpenWorld = false), Description(
        "Get detailed information about any VisioForge SDK class, including its full list of properties, " +
        "methods, events, constructors, base class, interfaces, and documentation. " +
        "Works for both core SDK classes and media blocks.")]
    public static string GetClassInfo(
        ApiIndexService index,
        [Description("Class name (e.g., 'VideoCaptureCoreX', 'H264EncoderSettings', 'MP4SinkSettings'). Can be short name or fully qualified.")]
        string className)
    {
        var cls = index.GetClassByName(className) ?? index.GetClassByFullName(className);
        if (cls == null)
        {
            // Try common suffixes
            cls = index.GetClassByName(className + "Settings")
                ?? index.GetClassByName(className + "Block")
                ?? index.GetClassByName(className + "X");
        }

        if (cls == null)
        {
            return $"Class '{className}' not found. Use search_api to find classes by keyword.";
        }

        return MediaBlockTools.FormatClassDetails(cls);
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, OpenWorld = false), Description(
        "Get the detailed signature and documentation for a specific method on a class. " +
        "Useful when you need to understand a method's parameters, return type, and behavior.")]
    public static string GetMethodSignature(
        ApiIndexService index,
        [Description("The class name that contains the method")]
        string className,
        [Description("The method name to look up")]
        string methodName)
    {
        var cls = index.GetClassByName(className) ?? index.GetClassByFullName(className);
        if (cls == null)
            return $"Class '{className}' not found.";

        var methods = cls.Methods.Where(m =>
            m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase)).ToList();

        if (methods.Count == 0)
        {
            // Also check constructors
            var ctors = cls.Constructors.Where(c =>
                c.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase) ||
                methodName.Equals("#ctor", StringComparison.OrdinalIgnoreCase) ||
                methodName.Equals("constructor", StringComparison.OrdinalIgnoreCase)).ToList();

            if (ctors.Count == 0)
                return $"Method '{methodName}' not found on class '{className}'. Available methods: {string.Join(", ", cls.Methods.Select(m => m.Name).Distinct().Take(20))}";

            methods = ctors;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# {className}.{methodName}\n");

        foreach (var method in methods)
        {
            var paramStr = string.Join(", ", method.Parameters.Select(p =>
                !string.IsNullOrEmpty(p.Type) ? $"{p.Type} {p.Name}" : p.Name));
            var returnPart = !string.IsNullOrEmpty(method.ReturnType) ? $"{method.ReturnType} " : "";
            var staticPart = method.IsStatic ? "static " : "";
            var asyncPart = method.IsAsync ? "async " : "";

            sb.AppendLine($"```csharp");
            sb.AppendLine($"{staticPart}{asyncPart}{returnPart}{method.Name}({paramStr})");
            sb.AppendLine($"```\n");

            if (!string.IsNullOrEmpty(method.Summary))
                sb.AppendLine($"**Summary:** {method.Summary}\n");

            if (method.Parameters.Count > 0)
            {
                sb.AppendLine("**Parameters:**");
                foreach (var param in method.Parameters)
                {
                    var typePart = !string.IsNullOrEmpty(param.Type) ? $" (`{param.Type}`)" : "";
                    sb.AppendLine($"- `{param.Name}`{typePart}: {param.Summary}");
                }
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(method.Returns))
                sb.AppendLine($"**Returns:** {method.Returns}\n");

            if (!string.IsNullOrEmpty(method.Remarks))
                sb.AppendLine($"**Remarks:** {method.Remarks}\n");

            sb.AppendLine("---");
        }

        return sb.ToString();
    }
}
