using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using VisioForge.MCP.Server.Index;

namespace VisioForge.MCP.Server.Tools;

[McpServerToolType]
public static class MediaBlockTools
{
    [McpServerTool(ReadOnly = true, Idempotent = true, OpenWorld = false), Description(
        "List available VisioForge media blocks, optionally filtered by category. " +
        "Media blocks are the building blocks of media processing pipelines. " +
        "Categories include: Sources, Sinks, VideoEncoders, AudioEncoders, VideoDecoders, " +
        "VideoProcessing, AudioProcessing, AudioRendering, VideoRendering, Demuxers, Parsers, " +
        "OpenGL, OpenCV, Nvidia, Decklink, AWS, RTSPServer, Bridge, Special, Outputs.")]
    public static string ListMediaBlocks(
        ApiIndexService index,
        [Description("Optional category to filter by (e.g., 'Sources', 'Sinks', 'VideoEncoders'). Leave empty to list all categories with their blocks.")]
        string? category = null)
    {
        var sb = new StringBuilder();

        if (string.IsNullOrEmpty(category))
        {
            // List all categories with block counts
            sb.AppendLine("# VisioForge Media Block Categories\n");
            foreach (var cat in index.Data.MediaBlockCategories)
            {
                sb.AppendLine($"## {cat.Name} ({cat.Blocks.Count} blocks)");
                sb.AppendLine(cat.Description);
                sb.AppendLine($"Blocks: {string.Join(", ", cat.Blocks)}");
                sb.AppendLine();
            }

            var totalBlocks = index.Data.MediaBlockCategories.Sum(c => c.Blocks.Count);
            sb.AppendLine($"**Total: {totalBlocks} media blocks across {index.Data.MediaBlockCategories.Count} categories**");
        }
        else
        {
            var blocks = index.GetMediaBlocks(category);
            if (blocks.Count == 0)
            {
                sb.AppendLine($"No media blocks found in category '{category}'.");
                sb.AppendLine($"Available categories: {string.Join(", ", index.Data.MediaBlockCategories.Select(c => c.Name))}");
                return sb.ToString();
            }

            sb.AppendLine($"# Media Blocks: {category} ({blocks.Count} blocks)\n");
            foreach (var block in blocks.OrderBy(b => b.Name))
            {
                sb.AppendLine($"## {block.Name}");
                if (!string.IsNullOrEmpty(block.Summary))
                    sb.AppendLine(block.Summary);

                // Show pads
                if (block.Pads.Count > 0)
                {
                    var inputs = block.Pads.Where(p => p.Direction == "In").ToList();
                    var outputs = block.Pads.Where(p => p.Direction == "Out").ToList();
                    if (inputs.Count > 0)
                        sb.AppendLine($"  Inputs: {string.Join(", ", inputs.Select(p => $"{p.Name} ({p.MediaType})"))}");
                    if (outputs.Count > 0)
                        sb.AppendLine($"  Outputs: {string.Join(", ", outputs.Select(p => $"{p.Name} ({p.MediaType})"))}");
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    [McpServerTool(ReadOnly = true, Idempotent = true, OpenWorld = false), Description(
        "Get detailed information about a specific media block including its properties, methods, events, " +
        "input/output pads, constructor parameters, and documentation. " +
        "Use this to understand how to configure and use a specific media block in a pipeline.")]
    public static string GetMediaBlockInfo(
        ApiIndexService index,
        [Description("Name of the media block (e.g., 'H264EncoderBlock', 'RTSPSourceBlock', 'MP4SinkBlock'). Can be the short name or full qualified name.")]
        string blockName)
    {
        var cls = index.GetClassByName(blockName) ?? index.GetClassByFullName(blockName);
        if (cls == null)
        {
            // Try with "Block" suffix
            cls = index.GetClassByName(blockName + "Block");
        }

        if (cls == null)
        {
            return $"Media block '{blockName}' not found. Use list_media_blocks to see available blocks, or search_api to find blocks by keyword.";
        }

        return FormatClassDetails(cls);
    }

    internal static string FormatClassDetails(ClassInfo cls)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# {cls.Name}");
        sb.AppendLine($"**Namespace:** `{cls.Namespace}`");
        sb.AppendLine($"**Full Name:** `{cls.FullName}`");

        if (cls.BaseClass != null)
            sb.AppendLine($"**Base Class:** `{cls.BaseClass}`");
        if (cls.Interfaces.Count > 0)
            sb.AppendLine($"**Implements:** {string.Join(", ", cls.Interfaces.Select(i => $"`{i}`"))}");
        if (cls.IsMediaBlock && cls.MediaBlockCategory != null)
            sb.AppendLine($"**Category:** {cls.MediaBlockCategory}");
        if (cls.IsCoreSDK && cls.SdkModule != null)
            sb.AppendLine($"**SDK Module:** {cls.SdkModule}");

        sb.AppendLine();

        if (!string.IsNullOrEmpty(cls.Summary))
        {
            sb.AppendLine("## Summary");
            sb.AppendLine(cls.Summary);
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(cls.Remarks))
        {
            sb.AppendLine("## Remarks");
            sb.AppendLine(cls.Remarks);
            sb.AppendLine();
        }

        // Pads
        if (cls.Pads.Count > 0)
        {
            sb.AppendLine("## Pads (Connection Points)");
            foreach (var pad in cls.Pads)
            {
                sb.AppendLine($"- **{pad.Name}** — Direction: {pad.Direction}, MediaType: {pad.MediaType}");
            }
            sb.AppendLine();
        }

        // Constructors
        if (cls.Constructors.Count > 0)
        {
            sb.AppendLine("## Constructors");
            foreach (var ctor in cls.Constructors)
            {
                var paramStr = string.Join(", ", ctor.Parameters.Select(p => $"{p.Type} {p.Name}"));
                sb.AppendLine($"- `{cls.Name}({paramStr})`");
                if (!string.IsNullOrEmpty(ctor.Summary))
                    sb.AppendLine($"  {ctor.Summary}");
                foreach (var param in ctor.Parameters)
                {
                    if (!string.IsNullOrEmpty(param.Summary))
                        sb.AppendLine($"  - `{param.Name}`: {param.Summary}");
                }
            }
            sb.AppendLine();
        }

        // Properties
        if (cls.Properties.Count > 0)
        {
            sb.AppendLine("## Properties");
            foreach (var prop in cls.Properties.OrderBy(p => p.Name))
            {
                var typePart = !string.IsNullOrEmpty(prop.Type) ? $" : `{prop.Type}`" : "";
                sb.AppendLine($"- **{prop.Name}**{typePart}");
                if (!string.IsNullOrEmpty(prop.Summary))
                    sb.AppendLine($"  {prop.Summary}");
            }
            sb.AppendLine();
        }

        // Methods
        if (cls.Methods.Count > 0)
        {
            sb.AppendLine("## Methods");
            foreach (var method in cls.Methods.OrderBy(m => m.Name))
            {
                var paramStr = string.Join(", ", method.Parameters.Select(p =>
                    !string.IsNullOrEmpty(p.Type) ? $"{p.Type} {p.Name}" : p.Name));
                var returnPart = !string.IsNullOrEmpty(method.ReturnType) ? $"{method.ReturnType} " : "";
                var staticPart = method.IsStatic ? "static " : "";
                var asyncPart = method.IsAsync ? "async " : "";
                sb.AppendLine($"- `{staticPart}{asyncPart}{returnPart}{method.Name}({paramStr})`");
                if (!string.IsNullOrEmpty(method.Summary))
                    sb.AppendLine($"  {method.Summary}");
                if (!string.IsNullOrEmpty(method.Returns))
                    sb.AppendLine($"  Returns: {method.Returns}");
            }
            sb.AppendLine();
        }

        // Events
        if (cls.Events.Count > 0)
        {
            sb.AppendLine("## Events");
            foreach (var evt in cls.Events.OrderBy(e => e.Name))
            {
                var typePart = !string.IsNullOrEmpty(evt.DelegateType) ? $" : `{evt.DelegateType}`" : "";
                sb.AppendLine($"- **{evt.Name}**{typePart}");
                if (!string.IsNullOrEmpty(evt.Summary))
                    sb.AppendLine($"  {evt.Summary}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
