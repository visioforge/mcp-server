using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace VisioForge.MCP.IndexBuilder;

/// <summary>
/// Parses .NET XML documentation files and extracts structured API metadata.
/// </summary>
public class XmlDocParser
{
    private readonly Dictionary<string, XElement> _members = new();

    public void Load(string xmlPath)
    {
        Console.WriteLine($"Loading XML documentation from: {xmlPath}");
        var doc = XDocument.Load(xmlPath);
        var members = doc.Root?.Element("members")?.Elements("member") ?? [];

        foreach (var member in members)
        {
            var name = member.Attribute("name")?.Value;
            if (!string.IsNullOrEmpty(name))
            {
                _members[name] = member;
            }
        }

        Console.WriteLine($"Loaded {_members.Count} documented members.");
    }

    /// <summary>
    /// Get all type-level member keys (T: prefix).
    /// </summary>
    public IEnumerable<string> GetTypeKeys()
    {
        return _members.Keys.Where(k => k.StartsWith("T:"));
    }

    /// <summary>
    /// Get all member keys for properties of a given type.
    /// </summary>
    public IEnumerable<(string Key, XElement Element)> GetPropertiesForType(string typeFullName)
    {
        var prefix = $"P:{typeFullName}.";
        return _members
            .Where(kv => kv.Key.StartsWith(prefix))
            .Select(kv => (kv.Key, kv.Value));
    }

    /// <summary>
    /// Get all member keys for methods of a given type.
    /// </summary>
    public IEnumerable<(string Key, XElement Element)> GetMethodsForType(string typeFullName)
    {
        var prefix = $"M:{typeFullName}.";
        return _members
            .Where(kv => kv.Key.StartsWith(prefix))
            .Select(kv => (kv.Key, kv.Value));
    }

    /// <summary>
    /// Get all member keys for events of a given type.
    /// </summary>
    public IEnumerable<(string Key, XElement Element)> GetEventsForType(string typeFullName)
    {
        var prefix = $"E:{typeFullName}.";
        return _members
            .Where(kv => kv.Key.StartsWith(prefix))
            .Select(kv => (kv.Key, kv.Value));
    }

    /// <summary>
    /// Get all member keys for fields of a given type (including enum values).
    /// </summary>
    public IEnumerable<(string Key, XElement Element)> GetFieldsForType(string typeFullName)
    {
        var prefix = $"F:{typeFullName}.";
        return _members
            .Where(kv => kv.Key.StartsWith(prefix))
            .Select(kv => (kv.Key, kv.Value));
    }

    public string GetSummary(string memberKey)
    {
        if (_members.TryGetValue(memberKey, out var element))
        {
            return CleanXmlText(element.Element("summary"));
        }
        return "";
    }

    public string GetRemarks(string memberKey)
    {
        if (_members.TryGetValue(memberKey, out var element))
        {
            return CleanXmlText(element.Element("remarks"));
        }
        return "";
    }

    public string GetValue(string memberKey)
    {
        if (_members.TryGetValue(memberKey, out var element))
        {
            return CleanXmlText(element.Element("value"));
        }
        return "";
    }

    public string GetReturns(string memberKey)
    {
        if (_members.TryGetValue(memberKey, out var element))
        {
            return CleanXmlText(element.Element("returns"));
        }
        return "";
    }

    public Dictionary<string, string> GetParams(string memberKey)
    {
        var result = new Dictionary<string, string>();
        if (_members.TryGetValue(memberKey, out var element))
        {
            foreach (var param in element.Elements("param"))
            {
                var name = param.Attribute("name")?.Value ?? "";
                if (!string.IsNullOrEmpty(name))
                {
                    result[name] = CleanXmlText(param);
                }
            }
        }
        return result;
    }

    public XElement? GetMember(string key)
    {
        _members.TryGetValue(key, out var element);
        return element;
    }

    /// <summary>
    /// Extracts clean text from XML documentation elements, resolving cref references.
    /// Walks child nodes so that self-closing &lt;see cref="..."/&gt; tags are replaced
    /// with the short type name instead of being silently dropped.
    /// </summary>
    private static string CleanXmlText(XElement? element)
    {
        if (element == null) return "";

        var sb = new System.Text.StringBuilder();

        foreach (var node in element.Nodes())
        {
            if (node is XText textNode)
            {
                sb.Append(textNode.Value);
            }
            else if (node is XElement el)
            {
                if (el.Name.LocalName is "see" or "seealso" or "paramref" or "typeparamref")
                {
                    // Resolve cref or name attribute to a short readable name
                    var cref = el.Attribute("cref")?.Value
                            ?? el.Attribute("name")?.Value
                            ?? "";

                    if (cref.Length > 2 && cref[1] == ':')
                    {
                        cref = cref[2..]; // Remove T:, M:, P: prefix
                    }

                    var lastDot = cref.LastIndexOf('.');
                    if (lastDot >= 0) cref = cref[(lastDot + 1)..];

                    // Use inner text if present, otherwise the resolved cref name
                    var inner = el.Value;
                    sb.Append(!string.IsNullOrEmpty(inner) ? inner : cref);
                }
                else
                {
                    // For any other nested element, just take its text content
                    sb.Append(el.Value);
                }
            }
        }

        // Clean up whitespace
        var text = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        return text;
    }

    /// <summary>
    /// Parse a method signature from the XML doc member key.
    /// e.g., M:Namespace.Class.Method(System.String,System.Int32)
    /// </summary>
    public static (string MethodName, List<string> ParamTypes) ParseMethodKey(string key)
    {
        // Remove M: prefix
        var sig = key.StartsWith("M:") ? key[2..] : key;

        var parenStart = sig.IndexOf('(');
        string methodFullName;
        var paramTypes = new List<string>();

        if (parenStart >= 0)
        {
            methodFullName = sig[..parenStart];
            var paramStr = sig[(parenStart + 1)..].TrimEnd(')');
            if (!string.IsNullOrEmpty(paramStr))
            {
                paramTypes = ParseParameterTypes(paramStr);
            }
        }
        else
        {
            methodFullName = sig;
        }

        // Extract just the method name (after last dot, but be careful with nested types)
        var lastDot = methodFullName.LastIndexOf('.');
        var methodName = lastDot >= 0 ? methodFullName[(lastDot + 1)..] : methodFullName;

        return (methodName, paramTypes);
    }

    /// <summary>
    /// Parse parameter types from a comma-separated string, handling nested generics.
    /// </summary>
    private static List<string> ParseParameterTypes(string paramStr)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < paramStr.Length; i++)
        {
            char c = paramStr[i];
            if (c == '{' || c == '<') depth++;
            else if (c == '}' || c == '>') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(SimplifyTypeName(paramStr[start..i].Trim()));
                start = i + 1;
            }
        }

        if (start < paramStr.Length)
        {
            result.Add(SimplifyTypeName(paramStr[start..].Trim()));
        }

        return result;
    }

    /// <summary>
    /// Simplify a fully-qualified type name to a short, readable form.
    /// </summary>
    public static string SimplifyTypeName(string fullTypeName)
    {
        // Handle common system types
        var mapping = new Dictionary<string, string>
        {
            ["System.String"] = "string",
            ["System.Int32"] = "int",
            ["System.Int64"] = "long",
            ["System.Boolean"] = "bool",
            ["System.Double"] = "double",
            ["System.Single"] = "float",
            ["System.Decimal"] = "decimal",
            ["System.Byte"] = "byte",
            ["System.Char"] = "char",
            ["System.Object"] = "object",
            ["System.Void"] = "void",
            ["System.UInt32"] = "uint",
            ["System.UInt64"] = "ulong",
            ["System.Int16"] = "short",
            ["System.UInt16"] = "ushort",
            ["System.Byte[]"] = "byte[]",
            ["System.SByte"] = "sbyte",
        };

        if (mapping.TryGetValue(fullTypeName, out var simple))
            return simple;

        // Handle Nullable
        if (fullTypeName.StartsWith("System.Nullable{"))
        {
            // Validate format: must end with }
            if (!fullTypeName.EndsWith('}') || fullTypeName.Length <= 17)
                return SimplifyLastPart(fullTypeName); // Malformed, return as-is

            var inner = fullTypeName[16..^1]; // Remove System.Nullable{ and }
            return SimplifyTypeName(inner) + "?";
        }

        // Handle generics with { } notation
        if (fullTypeName.Contains('{'))
        {
            // Validate format: must end with } and have balanced braces
            if (!fullTypeName.EndsWith('}'))
                return SimplifyLastPart(fullTypeName); // Malformed, return simplified name

            var openIndex = fullTypeName.IndexOf('{');
            var baseType = fullTypeName[..openIndex];
            var innerPart = fullTypeName[(openIndex + 1)..^1];

            // Verify braces are balanced in innerPart
            int braceDepth = 0;
            foreach (char c in innerPart)
            {
                if (c == '{') braceDepth++;
                else if (c == '}') braceDepth--;
                if (braceDepth < 0) return SimplifyLastPart(fullTypeName); // Malformed
            }
            if (braceDepth != 0) return SimplifyLastPart(fullTypeName); // Malformed

            var innerTypes = ParseParameterTypes(innerPart);
            var simplifiedInner = string.Join(", ", innerTypes.Select(SimplifyTypeName));
            return $"{SimplifyLastPart(baseType)}<{simplifiedInner}>";
        }

        return SimplifyLastPart(fullTypeName);
    }

    private static string SimplifyLastPart(string fullName)
    {
        // Keep VisioForge types with last two segments for context
        if (fullName.StartsWith("VisioForge."))
        {
            var parts = fullName.Split('.');
            return parts.Length >= 2 ? string.Join(".", parts[^2..]) : parts[^1];
        }

        // For other types, just the last segment
        var lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;
    }
}
