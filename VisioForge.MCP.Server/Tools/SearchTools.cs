using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using VisioForge.MCP.Server.Index;

namespace VisioForge.MCP.Server.Tools;

[McpServerToolType]
public static class SearchTools
{
    [McpServerTool(ReadOnly = true, Idempotent = true, OpenWorld = false), Description(
        "Search across the entire VisioForge SDK API — class names, method names, property names, " +
        "event names, and their documentation text. Returns ranked results. " +
        "Use this when you don't know the exact class name, or to find all classes related to a concept " +
        "(e.g., 'RTSP streaming', 'video overlay', 'audio capture').")]
    public static string SearchApi(
        ApiIndexService index,
        [Description("Search query — can be a class name, method name, concept, or description (e.g., 'RTSP', 'video resize', 'audio equalizer', 'H264 encoder settings')")]
        string query,
        [Description("Maximum number of results to return (default: 20, max: 50)")]
        int maxResults = 20)
    {
        if (maxResults > 50) maxResults = 50;
        if (maxResults < 1) maxResults = 1;

        var results = index.Search(query, maxResults);

        if (results.Count == 0)
        {
            return $"No results found for '{query}'. Try different keywords or check spelling.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# Search Results for \"{query}\" ({results.Count} results)\n");

        foreach (var result in results)
        {
            var kindBadge = result.Kind switch
            {
                "media_block" => "[MediaBlock]",
                "core_sdk" => "[CoreSDK]",
                "enum" => "[Enum]",
                "interface" => "[Interface]",
                _ => "[Class]"
            };

            var categoryPart = !string.IsNullOrEmpty(result.Category) ? $" ({result.Category})" : "";
            sb.AppendLine($"- **{result.Name}** {kindBadge}{categoryPart}");
            sb.AppendLine($"  `{result.FullName}`");
            if (!string.IsNullOrEmpty(result.Summary))
            {
                var summary = result.Summary.Length > 150 ? result.Summary[..150] + "..." : result.Summary;
                sb.AppendLine($"  {summary}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Use `get_class_info` or `get_media_block_info` with a class name to see full details.");
        return sb.ToString();
    }
}
