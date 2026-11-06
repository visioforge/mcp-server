using System.Text.RegularExpressions;

namespace VisioForge.MCP.IndexBuilder;

/// <summary>
/// Combines XML documentation and source analysis to generate the complete API index.
/// </summary>
public class IndexGenerator
{
    private readonly XmlDocParser _xmlParser;
    private readonly SourceAnalyzer _sourceAnalyzer;

    public IndexGenerator(XmlDocParser xmlParser, SourceAnalyzer sourceAnalyzer)
    {
        _xmlParser = xmlParser;
        _sourceAnalyzer = sourceAnalyzer;
    }

    public ApiIndex Generate(string sdkVersion)
    {
        Console.WriteLine("Generating API index...");

        var index = new ApiIndex
        {
            SdkVersion = sdkVersion,
            GeneratedAt = DateTime.UtcNow.ToString("o"),
        };

        var namespaces = new HashSet<string>();
        var mediaBlocksByCategory = new Dictionary<string, List<string>>();

        foreach (var typeKey in _xmlParser.GetTypeKeys())
        {
            var fullName = typeKey[2..]; // Remove "T:" prefix

            // Skip internal/private/compiler-generated types
            if (ShouldSkipType(fullName)) continue;

            var className = ExtractClassName(fullName);
            var ns = ExtractNamespace(fullName);
            namespaces.Add(ns);

            // Determine if it's an enum
            if (IsEnum(fullName))
            {
                var enumInfo = BuildEnumInfo(typeKey, fullName, className, ns);
                if (enumInfo != null)
                    index.Enums.Add(enumInfo);
            }
            else
            {
                var classInfo = BuildClassInfo(typeKey, fullName, className, ns);
                if (classInfo != null)
                {
                    index.Classes.Add(classInfo);

                    // Track media block categories
                    if (classInfo.IsMediaBlock && classInfo.MediaBlockCategory != null)
                    {
                        if (!mediaBlocksByCategory.ContainsKey(classInfo.MediaBlockCategory))
                            mediaBlocksByCategory[classInfo.MediaBlockCategory] = [];
                        mediaBlocksByCategory[classInfo.MediaBlockCategory].Add(classInfo.Name);
                    }
                }
            }
        }

        // Build namespace list
        index.Namespaces = namespaces.OrderBy(n => n).ToList();

        // Build media block categories
        var categoryDescriptions = SourceAnalyzer.GetCategoryDescriptions();
        foreach (var (cat, blocks) in mediaBlocksByCategory.OrderBy(kv => kv.Key))
        {
            categoryDescriptions.TryGetValue(cat, out var desc);
            index.MediaBlockCategories.Add(new MediaBlockCategory
            {
                Name = cat,
                Description = desc ?? cat,
                Blocks = blocks.OrderBy(b => b).ToList()
            });
        }

        Console.WriteLine($"Generated index: {index.Classes.Count} classes, {index.Enums.Count} enums, {index.Namespaces.Count} namespaces, {index.MediaBlockCategories.Count} media block categories");
        return index;
    }

    private ClassInfo? BuildClassInfo(string typeKey, string fullName, string className, string ns)
    {
        var summary = _xmlParser.GetSummary(typeKey);
        var remarks = _xmlParser.GetRemarks(typeKey);

        var (baseClass, interfaces) = _sourceAnalyzer.GetInheritance(fullName);
        var mediaBlockCategory = _sourceAnalyzer.GetMediaBlockCategory(className);
        var pads = _sourceAnalyzer.GetPads(fullName);
        var isCoreSDK = _sourceAnalyzer.IsCoreSDK(className);
        var sdkModule = _sourceAnalyzer.GetSdkModule(className);

        var isMediaBlock = mediaBlockCategory != null
            || className.EndsWith("Block")
            || (baseClass != null && (baseClass.Contains("MediaBlock") || baseClass.Contains("SourceMediaBlock")));

        var classInfo = new ClassInfo
        {
            FullName = fullName,
            Name = className,
            Namespace = ns,
            Kind = DetermineKind(className),
            Summary = summary,
            Remarks = remarks,
            BaseClass = baseClass,
            Interfaces = interfaces,
            IsMediaBlock = isMediaBlock,
            MediaBlockCategory = mediaBlockCategory,
            Pads = pads ?? [],
            IsCoreSDK = isCoreSDK,
            SdkModule = sdkModule,
        };

        // Build properties
        foreach (var (propKey, propElement) in _xmlParser.GetPropertiesForType(fullName))
        {
            var propName = ExtractMemberName(propKey, fullName);
            if (string.IsNullOrEmpty(propName) || propName.StartsWith("__")) continue;

            var propSummary = _xmlParser.GetSummary(propKey);
            var propValue = _xmlParser.GetValue(propKey);

            classInfo.Properties.Add(new PropertyInfo
            {
                Name = propName,
                Type = InferPropertyType(propSummary),
                Summary = propSummary,
                Value = propValue,
            });
        }

        // Build methods (including constructors)
        foreach (var (methodKey, methodElement) in _xmlParser.GetMethodsForType(fullName))
        {
            var (methodName, paramTypes) = XmlDocParser.ParseMethodKey(methodKey);
            if (string.IsNullOrEmpty(methodName)) continue;

            // Skip compiler-generated or internal methods
            if (methodName.StartsWith("__") || methodName.Contains("$")) continue;

            var methodSummary = _xmlParser.GetSummary(methodKey);
            var methodRemarks = _xmlParser.GetRemarks(methodKey);
            var methodReturns = _xmlParser.GetReturns(methodKey);
            var methodParams = _xmlParser.GetParams(methodKey);

            var parameters = new List<ParameterInfo>();
            var paramNames = methodParams.Keys.ToList();
            var paramSummaries = methodParams.Values.ToList();
            for (int i = 0; i < paramTypes.Count; i++)
            {
                parameters.Add(new ParameterInfo
                {
                    Name = i < paramNames.Count ? paramNames[i] : $"param{i}",
                    Type = paramTypes[i],
                    Summary = i < paramSummaries.Count ? paramSummaries[i] : "",
                });
            }

            var methodInfo = new MethodInfo
            {
                Name = methodName,
                Summary = methodSummary,
                Remarks = methodRemarks,
                Returns = methodReturns,
                Parameters = parameters,
                ReturnType = InferReturnType(methodReturns, methodName),
                IsAsync = methodName.EndsWith("Async") || methodSummary.Contains("async"),
                IsStatic = methodSummary.Contains("static") || methodName == "IsAvailable",
            };

            // Constructors
            if (methodName == "#ctor" || methodName == className)
            {
                methodInfo.Name = className;
                classInfo.Constructors.Add(methodInfo);
            }
            else
            {
                classInfo.Methods.Add(methodInfo);
            }
        }

        // Build events
        foreach (var (eventKey, eventElement) in _xmlParser.GetEventsForType(fullName))
        {
            var eventName = ExtractMemberName(eventKey, fullName);
            if (string.IsNullOrEmpty(eventName)) continue;

            classInfo.Events.Add(new EventInfo
            {
                Name = eventName,
                Summary = _xmlParser.GetSummary(eventKey),
                DelegateType = InferEventType(eventName),
            });
        }

        return classInfo;
    }

    private EnumInfo? BuildEnumInfo(string typeKey, string fullName, string enumName, string ns)
    {
        var enumInfo = new EnumInfo
        {
            FullName = fullName,
            Name = enumName,
            Namespace = ns,
            Summary = _xmlParser.GetSummary(typeKey),
        };

        foreach (var (fieldKey, fieldElement) in _xmlParser.GetFieldsForType(fullName))
        {
            var fieldName = ExtractMemberName(fieldKey, fullName);
            if (string.IsNullOrEmpty(fieldName) || fieldName == "value__") continue;

            enumInfo.Values.Add(new EnumValueInfo
            {
                Name = fieldName,
                Summary = _xmlParser.GetSummary(fieldKey),
            });
        }

        // Only include if it has enum values (to distinguish from regular classes with fields)
        return enumInfo.Values.Count > 0 ? enumInfo : null;
    }

    private bool IsEnum(string fullName)
    {
        // Heuristic: check if it has F: members but no P: or M: (besides .ctor) members
        var fields = _xmlParser.GetFieldsForType(fullName).ToList();
        if (fields.Count == 0) return false;

        var properties = _xmlParser.GetPropertiesForType(fullName).Any();
        var methods = _xmlParser.GetMethodsForType(fullName)
            .Where(m => !m.Key.Contains("#ctor"))
            .Any();

        // Enum types have fields only (plus value__), no properties or methods
        // Also check if it has value__ field which is the enum backing field
        var hasValueField = fields.Any(f => f.Key.EndsWith(".value__"));
        if (hasValueField) return true;

        // Fallback: types with only fields and no methods/properties are likely enums
        return fields.Count >= 2 && !properties && !methods;
    }

    private static bool ShouldSkipType(string fullName)
    {
        // Skip compiler-generated types (e.g., <>c__DisplayClass0_0, <Main>d__0)
        if (fullName.Contains('<')) return true;

        // Skip double-underscore internal/auto-generated types
        if (fullName.Contains("__")) return true;

        // Skip some very internal namespaces
        if (fullName.StartsWith("VisioForge.Core.GStreamer.Base.")) return true;
        if (fullName.StartsWith("VisioForge.Core.DirectShow.Interfaces.")) return true;

        return false;
    }

    private static string ExtractClassName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;
    }

    private static string ExtractNamespace(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName[..lastDot] : "";
    }

    private static string ExtractMemberName(string key, string typeFullName)
    {
        // Key format: P:Namespace.Class.Property or M:Namespace.Class.Method(...)
        var rest = key[2..]; // Strip prefix (P:, M:, E:, F:)

        // Remove parameters for methods
        var parenIdx = rest.IndexOf('(');
        if (parenIdx >= 0) rest = rest[..parenIdx];

        var typePrefix = typeFullName + ".";
        if (rest.StartsWith(typePrefix))
            return rest[typePrefix.Length..];

        // Fallback: get last segment
        var lastDot = rest.LastIndexOf('.');
        return lastDot >= 0 ? rest[(lastDot + 1)..] : rest;
    }

    private static string DetermineKind(string className)
    {
        if (className.StartsWith("I") && className.Length > 1 && char.IsUpper(className[1]))
            return "interface";
        return "class";
    }

    private static string InferPropertyType(string summary)
    {
        // Simple heuristic from documentation text
        var lower = summary.ToLower();
        if (lower.Contains("gets or sets a value indicating") || lower.Contains("boolean"))
            return "bool";
        if (lower.Contains("gets or sets the name") || lower.Contains("gets or sets the path") ||
            lower.Contains("file name") || lower.Contains("url"))
            return "string";
        return "";
    }

    private static string InferReturnType(string returns, string methodName)
    {
        // Check method name first — async methods return Task regardless of docs
        if (methodName.EndsWith("Async")) return "Task";

        if (string.IsNullOrEmpty(returns)) return "void";

        var lower = returns.ToLower();
        if (lower.Contains("true if") || lower.Contains("boolean")) return "bool";
        if (lower.Contains("task")) return "Task";

        return "";
    }

    private static string InferEventType(string eventName)
    {
        if (eventName.Contains("Error")) return "EventHandler<ErrorEventArgs>";
        if (eventName.Contains("Progress")) return "EventHandler<ProgressEventArgs>";
        if (eventName.Contains("Status")) return "EventHandler<StatusEventArgs>";
        return "EventHandler";
    }
}
