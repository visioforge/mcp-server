using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using VisioForge.MCP.Server.Index;

namespace VisioForge.MCP.Server.Tools;

[McpServerToolType]
public static class NamespaceTools
{
    [McpServerTool(ReadOnly = true, Idempotent = true, OpenWorld = false), Description(
        "Browse VisioForge SDK namespaces hierarchically. " +
        "Shows child namespaces and classes within a given namespace. " +
        "Start with 'VisioForge.Core' or leave empty to see top-level namespaces.")]
    public static string ListNamespaces(
        ApiIndexService index,
        [Description("Parent namespace to browse (e.g., 'VisioForge.Core.MediaBlocks'). Leave empty for top-level.")]
        string? parentNamespace = null)
    {
        var sb = new StringBuilder();

        if (string.IsNullOrEmpty(parentNamespace))
        {
            // Show top-level namespaces
            sb.AppendLine("# VisioForge SDK Namespaces\n");
            var topLevel = index.Data.Namespaces
                .Select(n =>
                {
                    var parts = n.Split('.');
                    return parts.Length >= 2 ? string.Join(".", parts[..2]) : n;
                })
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            foreach (var ns in topLevel)
            {
                // Count only direct descendants (not prefix matches like VisioForge.CoreX matching VisioForge.Core)
                var childCount = index.Data.Namespaces.Count(n =>
                    n.StartsWith(ns + ".", StringComparison.Ordinal) || n == ns);
                sb.AppendLine($"- `{ns}` ({childCount} sub-namespaces)");
            }
        }
        else
        {
            sb.AppendLine($"# Namespace: {parentNamespace}\n");

            // Show child namespaces
            var children = index.GetChildNamespaces(parentNamespace);
            if (children.Count > 0)
            {
                sb.AppendLine("## Child Namespaces");
                foreach (var child in children)
                {
                    sb.AppendLine($"- `{child}`");
                }
                sb.AppendLine();
            }

            // Show classes in this namespace
            var classes = index.GetClassesByNamespace(parentNamespace);
            if (classes.Count > 0)
            {
                sb.AppendLine($"## Classes ({classes.Count})");
                foreach (var cls in classes.OrderBy(c => c.Name))
                {
                    var kind = cls.IsMediaBlock ? " [MediaBlock]" : (cls.IsCoreSDK ? " [CoreSDK]" : "");
                    var summary = !string.IsNullOrEmpty(cls.Summary) && cls.Summary.Length > 80
                        ? cls.Summary[..80] + "..."
                        : cls.Summary;
                    sb.AppendLine($"- **{cls.Name}**{kind} — {summary}");
                }
            }

            if (children.Count == 0 && classes.Count == 0)
            {
                sb.AppendLine("No child namespaces or classes found.");
            }
        }

        return sb.ToString();
    }
}
