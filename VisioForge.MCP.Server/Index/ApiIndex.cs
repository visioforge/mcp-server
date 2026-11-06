// Model classes (ClassInfo, PropertyInfo, MethodInfo, etc.) have been moved to VisioForge.MCP.Shared.
// ApiIndex is the shared root type, used as "ApiIndexData" alias here for backwards compatibility.
global using VisioForge.MCP.Shared;
global using ApiIndexData = VisioForge.MCP.Shared.ApiIndex;

namespace VisioForge.MCP.Server.Index;

public class SearchResult
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? Category { get; set; }
    public int Score { get; set; }
}
