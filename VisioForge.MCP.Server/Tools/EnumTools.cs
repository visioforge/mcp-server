using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using VisioForge.MCP.Server.Index;

namespace VisioForge.MCP.Server.Tools;

[McpServerToolType]
public static class EnumTools
{
    [McpServerTool(ReadOnly = true, Idempotent = true, OpenWorld = false), Description(
        "Get all values of a VisioForge SDK enum type with descriptions. " +
        "Useful for understanding available options for configuration properties " +
        "(e.g., MediaBlockType, video codecs, audio formats, pixel formats).")]
    public static string GetEnumValues(
        ApiIndexService index,
        [Description("Enum name (e.g., 'MediaBlockType', 'MediaBlockPadMediaType', 'MediaBlockPadDirection'). Can be short name or fully qualified.")]
        string enumName,
        [Description("Optional text filter to show only matching enum values (e.g., 'H264' to find H264-related values)")]
        string? filter = null)
    {
        var enumInfo = index.GetEnumByName(enumName);
        if (enumInfo == null)
        {
            // Try searching
            var results = index.Search(enumName, 5);
            var enumResults = results.Where(r => r.Kind == "enum").ToList();
            if (enumResults.Count > 0)
            {
                var sb2 = new StringBuilder();
                sb2.AppendLine($"Enum '{enumName}' not found. Did you mean one of these?");
                foreach (var r in enumResults)
                    sb2.AppendLine($"- {r.Name} (`{r.FullName}`)");
                return sb2.ToString();
            }
            return $"Enum '{enumName}' not found. Use search_api to find enums by keyword.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# {enumInfo.Name}");
        sb.AppendLine($"**Namespace:** `{enumInfo.Namespace}`");
        if (!string.IsNullOrEmpty(enumInfo.Summary))
            sb.AppendLine($"\n{enumInfo.Summary}");

        var values = enumInfo.Values;
        if (!string.IsNullOrEmpty(filter))
        {
            values = values.Where(v =>
                v.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                v.Summary.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            sb.AppendLine($"\n*Filtered by: \"{filter}\" ({values.Count} of {enumInfo.Values.Count} values)*");
        }

        sb.AppendLine($"\n## Values ({values.Count})\n");

        foreach (var val in values)
        {
            sb.AppendLine($"- **{val.Name}**");
            if (!string.IsNullOrEmpty(val.Summary))
                sb.AppendLine($"  {val.Summary}");
        }

        return sb.ToString();
    }
}
